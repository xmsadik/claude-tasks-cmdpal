using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeTasks.Core;
using ClaudeTasks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeTasks;

/// <summary>
/// Top-level ListPage: one row per local Claude project, each opening its
/// <see cref="ProjectTasksPage"/>. Those child pages are cached by project path (created once,
/// reused across every <see cref="GetItems"/> call) so they subscribe to
/// <see cref="TaskStore.Changed"/> exactly once instead of leaking a handler per rebuild;
/// pages for projects that disappear from the store are disposed and dropped.
/// </summary>
internal sealed partial class ProjectsPage : ListPage, IDisposable
{
    private readonly TaskStore _store;
    private readonly SettingsManager _settings;
    private readonly Dictionary<string, ProjectTasksPage> _pages = new(StringComparer.OrdinalIgnoreCase);

    public ProjectsPage(TaskStore store, SettingsManager settings)
    {
        _store = store;
        _settings = settings;

        Id = "ClaudeTasks.page.projects";
        Title = "Claude Tasks";
        Name = "Browse all tasks";
        Icon = new IconInfo("📋");
        PlaceholderText = "Search projects";

        _store.Changed += OnStoreChanged;
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        foreach (var page in _pages.Values)
        {
            page.Dispose();
        }

        _pages.Clear();
    }

    private void OnStoreChanged(object? sender, EventArgs e) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var snapshot = _store.Snapshot;
        PrunePages(snapshot.Projects);

        var visibleProjects = _settings.ShowCompleted
            ? snapshot.Projects
            : snapshot.Projects.Where(p => !(p.Total > 0 && p.Done == p.Total)).ToList();

        if (visibleProjects.Count == 0)
        {
            return [new ListItem
            {
                Title = snapshot.Projects.Count == 0 ? "No projects found" : "All projects complete",
                Subtitle = snapshot.Projects.Count == 0
                    ? "Projects are found via ~/.claude.json and the Extra scan roots setting."
                    : "Enable Show completed in Settings to see them.",
            }];
        }

        var activePath = snapshot.Active?.Path;
        var now = DateTimeOffset.UtcNow;

        return visibleProjects.Select(project =>
        {
            var page = GetOrCreatePage(project);
            var percent = project.PercentDone();
            var isActive = activePath is not null && string.Equals(project.Path, activePath, StringComparison.OrdinalIgnoreCase);
            var prefix = isActive ? "● " : string.Empty;

            return (IListItem)new ListItem(page)
            {
                Title = project.Name,
                Subtitle = $"{prefix}{ProgressFormat.Bar(percent)} {ProgressFormat.Invariant(percent)}% · " +
                    $"{ProgressFormat.Invariant(project.Done)}/{ProgressFormat.Invariant(project.Total)} · {ProgressFormat.RelativeTime(project.LastWriteTime, now)}",
                Icon = new IconInfo(project.Total > 0 && project.Done == project.Total ? "✅" : "📁"),
                MoreCommands =
                [
                    new CommandContextItem(new OpenPathCommand("Open todo.md", "📄", () => project.Location.TodoPath)),
                    new CommandContextItem(new OpenPathCommand("Open folder", "📁", () => project.Path)),
                ],
            };
        }).ToArray();
    }

    private ProjectTasksPage GetOrCreatePage(ProjectSnapshot project)
    {
        if (_pages.TryGetValue(project.Path, out var page))
        {
            return page;
        }

        page = new ProjectTasksPage(_store, _settings, project.Path, project.Name);
        _pages[project.Path] = page;
        return page;
    }

    private void PrunePages(IReadOnlyList<ProjectSnapshot> allProjects)
    {
        var alive = new HashSet<string>(allProjects.Select(p => p.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var stalePath in _pages.Keys.Where(path => !alive.Contains(path)).ToList())
        {
            _pages[stalePath].Dispose();
            _pages.Remove(stalePath);
        }
    }
}
