using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeTasks;

internal sealed partial class SettingsManager : JsonSettingsManager
{
    private const string DefaultRefreshSeconds = "60";
    private const string DefaultExtraScanRoots = "%USERPROFILE%";

    private static readonly string _namespace = "ClaudeTasks";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly ChoiceSetSetting _refreshInterval = new(
        Namespaced(nameof(RefreshInterval)),
        "Refresh interval",
        "How often to re-scan local todo.md files",
        [
            new ChoiceSetSetting.Choice("30 seconds", "30"),
            new ChoiceSetSetting.Choice("1 minute", DefaultRefreshSeconds),
            new ChoiceSetSetting.Choice("2 minutes", "120"),
            new ChoiceSetSetting.Choice("5 minutes", "300"),
        ]);

    private readonly TextSetting _extraScanRoots = new(
        Namespaced(nameof(ExtraScanRoots)),
        "Extra scan roots",
        "Semicolon-separated folders to check for <root>\\*\\tasks\\todo.md, beyond known Claude projects",
        DefaultExtraScanRoots);

    private readonly ToggleSetting _showCompleted = new(
        Namespaced(nameof(ShowCompleted)),
        "Show completed",
        "Show fully-completed projects and finished tasks",
        true);

    // The values as of the last SettingsChanged firing, so the combined event (which doesn't say
    // which setting changed) can be split into RefreshIntervalChanged/ShowCompletedChanged.
    private TimeSpan _lastRefreshInterval;
    private string _lastExtraScanRoots = string.Empty;
    private bool _lastShowCompleted;

    /// <summary>Raised when the refresh interval or extra scan roots change - the store restarts its timer and rescans.</summary>
    public event EventHandler? RefreshIntervalChanged;

    /// <summary>Raised when "Show completed" changes - view-only, so the store just re-renders the current snapshot.</summary>
    public event EventHandler? ShowCompletedChanged;

    public TimeSpan RefreshInterval
    {
        get
        {
            var raw = _refreshInterval.Value;
            return int.TryParse(raw, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(int.Parse(DefaultRefreshSeconds, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public string ExtraScanRoots => string.IsNullOrWhiteSpace(_extraScanRoots.Value)
        ? DefaultExtraScanRoots
        : _extraScanRoots.Value;

    public bool ShowCompleted => _showCompleted.Value;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("ClaudeTasks");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_refreshInterval);
        Settings.Add(_extraScanRoots);
        Settings.Add(_showCompleted);

        // Load settings from file upon initialization
        LoadSettings();

        _lastRefreshInterval = RefreshInterval;
        _lastExtraScanRoots = ExtraScanRoots;
        _lastShowCompleted = ShowCompleted;

        // The toolkit fires one combined event for any setting change (it doesn't say which),
        // so which of our two events to raise is determined by comparing against the values we
        // saw last time.
        Settings.SettingsChanged += (_, _) =>
        {
            try
            {
                SaveSettings();
            }
            catch (Exception)
            {
                // Persisting to disk failed (e.g. a locked/readonly settings.json); the
                // in-memory values are still correct, so the change events below must still
                // fire rather than being skipped.
            }

            var intervalOrRootsChanged = _lastRefreshInterval != RefreshInterval
                || !string.Equals(_lastExtraScanRoots, ExtraScanRoots, StringComparison.Ordinal);
            var showCompletedChanged = _lastShowCompleted != ShowCompleted;

            _lastRefreshInterval = RefreshInterval;
            _lastExtraScanRoots = ExtraScanRoots;
            _lastShowCompleted = ShowCompleted;

            if (intervalOrRootsChanged)
            {
                RefreshIntervalChanged?.Invoke(this, EventArgs.Empty);
            }

            if (showCompletedChanged)
            {
                ShowCompletedChanged?.Invoke(this, EventArgs.Empty);
            }
        };
    }
}
