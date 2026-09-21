using System;
using System.Globalization;
using ClaudeTasks.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeTasks;

/// <summary>
/// The always-visible Dock item: aggregate percent (Σdone/Σtotal across projects that have
/// tasks) plus a project/open-task count, live-updated from <see cref="TaskStore.Changed"/>.
/// </summary>
internal sealed partial class TasksBand : ListItem, IDisposable
{
    private readonly TaskStore _store;

    public TasksBand(TaskStore store, TasksSummaryPage summaryPage)
        : base(summaryPage)
    {
        _store = store;
        _store.Changed += OnStoreChanged;

        Render();
    }

    public void Dispose() => _store.Changed -= OnStoreChanged;

    private void OnStoreChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        var snapshot = _store.Snapshot;

        if (!snapshot.HasScanned)
        {
            Title = "--%";
            Icon = new IconInfo("📋");
            Subtitle = "Claude Tasks";
            return;
        }

        if (snapshot.ProjectsWithTasks == 0)
        {
            Title = "—";
            Icon = new IconInfo("📋");
            Subtitle = "No tasks found";
            return;
        }

        var percent = ProgressFormat.PercentDone(snapshot.TotalDone, snapshot.TotalTasks);
        Title = percent.ToString(CultureInfo.InvariantCulture) + "%";
        Icon = new IconInfo(snapshot.OpenCount == 0 ? "✅" : "📋");
        Subtitle = ProgressFormat.Invariant(snapshot.ProjectsWithTasks) + " projects · " + ProgressFormat.Invariant(snapshot.OpenCount) + " open";
    }
}
