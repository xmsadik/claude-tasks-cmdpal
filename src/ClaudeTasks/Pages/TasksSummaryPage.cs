using System;
using System.Globalization;
using System.Linq;
using System.Text;
using ClaudeTasks.Core;
using ClaudeTasks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeTasks;

/// <summary>
/// The Dock flyout: a markdown table of all projects plus the open tasks of the active
/// project, live-updated from <see cref="TaskStore.Changed"/>. "Browse all tasks" is a
/// <see cref="CommandContextItem"/> wrapping a <see cref="ProjectsPage"/> - navigating from
/// the flyout into the full palette was verified working during the Phase 1 spike.
/// </summary>
internal sealed partial class TasksSummaryPage : ContentPage, IDisposable
{
    private const int MaxOpenTasksShown = 20;

    private readonly TaskStore _store;
    private readonly SettingsManager _settings;

    public TasksSummaryPage(TaskStore store, SettingsManager settingsManager, ProjectsPage projectsPage)
    {
        _store = store;
        _settings = settingsManager;

        Id = "ClaudeTasks.page.summary";
        Name = "Claude Tasks";
        Title = "Claude Tasks";
        Icon = new IconInfo("📋");

        Commands =
        [
            new CommandContextItem(projectsPage) { Title = "Browse all tasks" },
            new CommandContextItem(new OpenPathCommand("Open todo.md", "📄", () => _store.Snapshot.Active?.Location.TodoPath)),
            new CommandContextItem(new OpenPathCommand("Open folder", "📁", () => _store.Snapshot.Active?.Path)),
            new CommandContextItem(new RefreshCommand(_store)),
            new CommandContextItem(settingsManager.Settings.SettingsPage),
        ];

        _store.Changed += OnStoreChanged;
    }

    public void Dispose() => _store.Changed -= OnStoreChanged;

    private void OnStoreChanged(object? sender, EventArgs e) => RaiseItemsChanged();

    public override IContent[] GetContent() =>
    [
        new MarkdownContent(BuildMarkdown()),
    ];

    private string BuildMarkdown()
    {
        var snapshot = _store.Snapshot;
        var now = DateTimeOffset.UtcNow;
        var sb = new StringBuilder();

        sb.AppendLine("# Claude Tasks");
        sb.AppendLine();

        if (snapshot.Projects.Count == 0)
        {
            sb.AppendLine(snapshot.HasScanned ? "_No projects found._" : "_Scanning…_");

            if (snapshot.Error is not null)
            {
                sb.AppendLine();
                sb.AppendLine(CultureInfo.InvariantCulture, $"_{ProgressFormat.EscapeMarkdown(snapshot.Error)}_");
            }

            sb.AppendLine();
            sb.AppendLine("Projects are found via `~/.claude.json` and the **Extra scan roots** setting - "
                + "open a Claude Code session in a project with a `tasks/todo.md`, or add a folder to scan in Settings.");
            return sb.ToString();
        }

        var updated = snapshot.HasScanned ? snapshot.LastScan.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) : "--:--";
        var percentSuffix = snapshot.ProjectsWithTasks == 0
            ? string.Empty
            : " · " + ProgressFormat.Invariant(ProgressFormat.PercentDone(snapshot.TotalDone, snapshot.TotalTasks)) + "%";

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"_{ProgressFormat.Invariant(snapshot.ProjectsWithTasks)} projects · {ProgressFormat.Invariant(snapshot.OpenCount)} open{percentSuffix} · updated {updated}_");

        if (snapshot.Error is not null)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"_{ProgressFormat.EscapeMarkdown(snapshot.Error)}_");
        }

        sb.AppendLine();

        var visibleProjects = _settings.ShowCompleted
            ? snapshot.Projects
            : snapshot.Projects.Where(p => !(p.Total > 0 && p.Done == p.Total)).ToList();

        if (visibleProjects.Count == 0)
        {
            sb.AppendLine("_All projects are complete. Enable **Show completed** in Settings to see them._");
        }
        else
        {
            sb.AppendLine("| Project | Progress | % | Done/Total | Updated |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var project in visibleProjects)
            {
                var percent = project.PercentDone();
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"| {ProgressFormat.EscapeMarkdown(project.Name)} | {ProgressFormat.Bar(percent, 10)} | {percent}% | " +
                    $"{ProgressFormat.Invariant(project.Done)}/{ProgressFormat.Invariant(project.Total)} | {ProgressFormat.RelativeTime(project.LastWriteTime, now)} |");
            }
        }

        if (snapshot.Active is ProjectSnapshot active)
        {
            AppendOpenTasks(sb, active);
        }

        return sb.ToString();
    }

    private static void AppendOpenTasks(StringBuilder sb, ProjectSnapshot active)
    {
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"### Open tasks — {ProgressFormat.EscapeMarkdown(active.Name)}");

        var openPairs = active.Sections
            .SelectMany(section => section.Items.Where(i => !i.IsDone).Select(item => (Section: section.Name, item.Text)))
            .ToList();

        if (openPairs.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("_No open tasks._");
            return;
        }

        string? currentSection = null;
        foreach (var (section, text) in openPairs.Take(MaxOpenTasksShown))
        {
            if (!string.Equals(section, currentSection, StringComparison.Ordinal))
            {
                sb.AppendLine();
                sb.AppendLine(CultureInfo.InvariantCulture, $"#### {ProgressFormat.EscapeMarkdown(section)}");
                currentSection = section;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"- {ProgressFormat.EscapeMarkdown(text)}");
        }

        if (openPairs.Count > MaxOpenTasksShown)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"…and {ProgressFormat.Invariant(openPairs.Count - MaxOpenTasksShown)} more");
        }
    }
}

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>Manually triggers a store rescan (no throttle - the store itself guards overlap).</summary>
internal sealed partial class RefreshCommand : InvokableCommand
{
    private readonly TaskStore _store;

    public RefreshCommand(TaskStore store)
    {
        _store = store;
        Name = "Refresh";
        Icon = new IconInfo("🔄");
    }

    public override CommandResult Invoke()
    {
        _store.RefreshNow();
        return CommandResult.KeepOpen();
    }
}

#pragma warning restore SA1402 // File may only contain a single type
