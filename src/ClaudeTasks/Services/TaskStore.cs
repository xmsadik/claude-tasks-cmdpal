using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClaudeTasks.Core;

namespace ClaudeTasks.Services;

/// <summary>
/// Single owner of the extension's live state: the discovered local Claude projects and their
/// parsed todo.md files. Owned by the CommandsProvider and shared by the Dock band and every
/// page. Nothing here ever blocks or throws to the caller - the scan loop runs on a background
/// task and swallows everything, mirroring ClaudeUsage's UsageStore.
/// </summary>
internal sealed partial class TaskStore : IDisposable
{
    private readonly SettingsManager _settings;
    private readonly object _restartLock = new();

    // Only ever mutated from inside a scan, and a scan is guarded so at most one runs at a
    // time - no lock needed around reads/writes of this dictionary.
    private readonly Dictionary<string, CachedTodo> _cache = new(StringComparer.OrdinalIgnoreCase);

    // The last successfully-read ~/.claude.json project list. Only ever mutated from inside a
    // scan (same guarantee as _cache above) - kept so a transient read/parse failure falls back
    // to it instead of silently dropping every registered project for that tick.
    private IReadOnlyList<string> _lastRegistered = [];

    private int _scanInProgress;
    private int _rescanRequested;
    private CancellationTokenSource? _timerCts;

    private volatile TaskStoreSnapshot _snapshot = TaskStoreSnapshot.Initial;

    public TaskStore(SettingsManager settings)
    {
        _settings = settings;

        // Interval/roots changes restart the scan loop (which scans immediately); "Show
        // completed" is view-only, so it just re-renders the current snapshot with no rescan.
        _settings.RefreshIntervalChanged += (_, _) => RestartTimer();
        _settings.ShowCompletedChanged += (_, _) => RaiseChanged();

        RestartTimer();
    }

    /// <summary>Raised whenever the snapshot changes (a scan completed, successfully or not).</summary>
    public event EventHandler? Changed;

    public TaskStoreSnapshot Snapshot => _snapshot;

    /// <summary>Triggers an immediate scan. No throttle - guarded only against overlap.</summary>
    public void RefreshNow() => _ = ScanAsync();

    public void Dispose()
    {
        lock (_restartLock)
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();
            _timerCts = null;
        }
    }

    private void RestartTimer()
    {
        lock (_restartLock)
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();

            var cts = new CancellationTokenSource();
            _timerCts = cts;
            _ = RunTimerLoopAsync(_settings.RefreshInterval, cts.Token);
        }
    }

    private async Task RunTimerLoopAsync(TimeSpan interval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            await ScanOnceAsync().ConfigureAwait(false);

            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await ScanOnceAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the interval/roots change or the store is disposed.
        }

        async Task ScanOnceAsync()
        {
            try
            {
                await ScanAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // A bad tick (e.g. something escaping ScanAsync despite its own guards) must
                // never end the loop - only cancellation does that.
            }
        }
    }

    private async Task ScanAsync()
    {
        if (Interlocked.CompareExchange(ref _scanInProgress, 1, 0) != 0)
        {
            // A scan is already in flight. Flag it so the in-flight scan runs once more after it
            // finishes - otherwise a settings change (interval/roots) that lands mid-scan would
            // be silently dropped until the next timer tick.
            Interlocked.Exchange(ref _rescanRequested, 1);
            return;
        }

        var raiseChanged = true;
        try
        {
            raiseChanged = await Task.Run(ScanCore).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // An escaped exception would end the timer loop and freeze the band/pages on
            // stale data forever, so any surprise becomes a visible (but non-blanking) error.
            var previous = _snapshot;
            _snapshot = previous with { LastScan = DateTimeOffset.UtcNow, Error = "Scan failed; showing previous data." };
            raiseChanged = true;
        }
        finally
        {
            Interlocked.Exchange(ref _scanInProgress, 0);
        }

        if (raiseChanged)
        {
            RaiseChanged();
        }

        if (Interlocked.Exchange(ref _rescanRequested, 0) == 1)
        {
            await ScanAsync().ConfigureAwait(false);
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // A throwing handler must not break the scan loop.
        }
    }

    /// <summary>Returns whether the change is meaningful enough to raise <see cref="Changed"/> for (the snapshot itself is always swapped).</summary>
    private bool ScanCore()
    {
        var claudeJsonPath = ClaudePaths.ClaudeJsonPath();
        var roots = ProjectDiscovery.ParseRoots(_settings.ExtraScanRoots);

        var registered = SafeReadRegistered(claudeJsonPath);
        if (registered is null)
        {
            // Transient .claude.json read/parse failure (e.g. Claude Code was mid-write this
            // tick): keep the last registered-project list instead of dropping every project.
            registered = _lastRegistered;
        }
        else
        {
            _lastRegistered = registered;
        }

        var discovered = SafeDiscover(registered, roots);

        var currentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var snapshots = new List<ProjectSnapshot>();

        foreach (var location in discovered)
        {
            currentPaths.Add(location.Path);

            var snapshot = ScanOneProject(location);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        foreach (var staleKey in _cache.Keys.Where(k => !currentPaths.Contains(k)).ToList())
        {
            _cache.Remove(staleKey);
        }

        var sorted = SortProjects(snapshots);
        var active = DetermineActive(sorted);

        var totalDone = 0;
        var totalTasks = 0;
        var projectsWithTasks = 0;
        foreach (var project in sorted)
        {
            if (project.Total > 0)
            {
                totalDone += project.Done;
                totalTasks += project.Total;
                projectsWithTasks++;
            }
        }

        var previous = _snapshot;
        var next = new TaskStoreSnapshot(
            sorted,
            active,
            totalDone,
            totalTasks,
            totalTasks - totalDone,
            projectsWithTasks,
            DateTimeOffset.UtcNow,
            null);

        // The snapshot is always swapped (so the flyout's "updated HH:mm" stays current), but
        // Changed is only raised when something a viewer would actually notice changed - always
        // on the first scan and whenever a previous scan's error is cleared.
        var meaningfulChange = !previous.HasScanned || previous.Error is not null || HasMeaningfulChange(previous, next);

        _snapshot = next;
        return meaningfulChange;
    }

    private static bool HasMeaningfulChange(TaskStoreSnapshot previous, TaskStoreSnapshot next)
    {
        if (previous.Projects.Count != next.Projects.Count ||
            previous.ProjectsWithTasks != next.ProjectsWithTasks ||
            !string.Equals(previous.Active?.Path, next.Active?.Path, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var i = 0; i < previous.Projects.Count; i++)
        {
            var a = previous.Projects[i];
            var b = next.Projects[i];
            if (!string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase) ||
                a.Done != b.Done ||
                a.Total != b.Total ||
                a.LastWriteTime != b.LastWriteTime)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Stats todo.md and only re-reads/re-parses it if mtime or length changed since the last scan.</summary>
    private ProjectSnapshot? ScanOneProject(ProjectLocation location)
    {
        _cache.TryGetValue(location.Path, out var cached);

        DateTime writeUtc;
        long length;
        try
        {
            var info = new System.IO.FileInfo(location.TodoPath);
            if (!info.Exists)
            {
                return null; // Vanished since discovery ran a moment ago.
            }

            writeUtc = info.LastWriteTimeUtc;
            length = info.Length;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            return cached is null ? null : new ProjectSnapshot(location, cached.Document, new DateTimeOffset(cached.LastWriteUtc, TimeSpan.Zero));
        }

        if (cached is not null && cached.LastWriteUtc == writeUtc && cached.Length == length)
        {
            return new ProjectSnapshot(location, cached.Document, new DateTimeOffset(writeUtc, TimeSpan.Zero));
        }

        var text = TodoFileReader.Read(location.TodoPath);
        if (text is null)
        {
            // Transient read failure (e.g. OneDrive hydrating): keep the last good parse.
            return cached is null ? null : new ProjectSnapshot(location, cached.Document, new DateTimeOffset(cached.LastWriteUtc, TimeSpan.Zero));
        }

        var document = TodoParser.Parse(text);
        _cache[location.Path] = new CachedTodo(writeUtc, length, document);
        return new ProjectSnapshot(location, document, new DateTimeOffset(writeUtc, TimeSpan.Zero));
    }

    private static IReadOnlyList<string>? SafeReadRegistered(string claudeJsonPath)
    {
        try
        {
            return ProjectDiscovery.ReadRegisteredProjects(claudeJsonPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IReadOnlyList<ProjectLocation> SafeDiscover(IReadOnlyList<string> registeredPaths, IReadOnlyList<string> roots)
    {
        try
        {
            return ProjectDiscovery.Discover(registeredPaths, roots);
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Newest todo.md first; fully-completed projects sink to the bottom regardless of recency.</summary>
    private static List<ProjectSnapshot> SortProjects(List<ProjectSnapshot> projects) =>
        projects
            .OrderBy(p => IsComplete(p) ? 1 : 0)
            .ThenByDescending(p => p.LastWriteTime)
            .ToList();

    private static bool IsComplete(ProjectSnapshot project) => project.Total > 0 && project.Done == project.Total;

    private static ProjectSnapshot? DetermineActive(List<ProjectSnapshot> projects)
    {
        if (projects.Count == 0)
        {
            return null;
        }

        string? activeCwd;
        try
        {
            activeCwd = ActiveProjectDetector.FindActiveCwd(ClaudePaths.ConfigDir());
        }
        catch (Exception)
        {
            activeCwd = null;
        }

        if (activeCwd is not null)
        {
            var match = projects.FirstOrDefault(p => string.Equals(p.Path, activeCwd, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return projects.OrderByDescending(p => p.LastWriteTime).First();
    }

    private sealed record CachedTodo(DateTime LastWriteUtc, long Length, TodoDocument Document);
}

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Immutable point-in-time view of every discovered project. <see cref="Error"/> is set only
/// when the most recent scan itself blew up (not when discovery merely found nothing) -
/// <see cref="Projects"/>/<see cref="Active"/> still hold the last good data in that case.
/// </summary>
internal sealed record TaskStoreSnapshot(
    IReadOnlyList<ProjectSnapshot> Projects,
    ProjectSnapshot? Active,
    int TotalDone,
    int TotalTasks,
    int OpenCount,
    int ProjectsWithTasks,
    DateTimeOffset LastScan,
    string? Error)
{
    public static readonly TaskStoreSnapshot Initial = new([], null, 0, 0, 0, 0, DateTimeOffset.MinValue, null);

    /// <summary>False until the first scan (successful or not) has completed.</summary>
    public bool HasScanned => LastScan != DateTimeOffset.MinValue;
}

#pragma warning restore SA1402 // File may only contain a single type
