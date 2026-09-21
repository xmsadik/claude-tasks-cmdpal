using System;
using System.Collections.Generic;
using System.Linq;
using ClaudeTasks.Core;
using ClaudeTasks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ClaudeTasks;

/// <summary>
/// Full task tree for one project, grouped by "##" section via <see cref="ListItem.Section"/>.
/// Holds only the project's path, not a snapshot - <see cref="GetItems"/> looks the project up
/// in the store fresh every call, so a page kept alive by <see cref="ProjectsPage"/>'s cache
/// still reflects the latest scan without ever being rebuilt.
/// </summary>
internal sealed partial class ProjectTasksPage : ListPage, IDisposable
{
    private readonly TaskStore _store;
    private readonly SettingsManager _settings;
    private readonly string _path;

    public ProjectTasksPage(TaskStore store, SettingsManager settings, string path, string initialName)
    {
        _store = store;
        _settings = settings;
        _path = path;

        Id = "ClaudeTasks.page.project." + path;
        Title = initialName;
        Name = initialName;
        Icon = new IconInfo("📁");
        PlaceholderText = "Search tasks";

        _store.Changed += OnStoreChanged;
    }

    public void Dispose() => _store.Changed -= OnStoreChanged;

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (FindProject() is ProjectSnapshot project)
        {
            Title = project.Name;
            Name = project.Name;
        }

        RaiseItemsChanged();
    }

    private ProjectSnapshot? FindProject() =>
        _store.Snapshot.Projects.FirstOrDefault(p => string.Equals(p.Path, _path, StringComparison.OrdinalIgnoreCase));

    public override IListItem[] GetItems()
    {
        var project = FindProject();
        if (project is null)
        {
            return [new ListItem { Title = "Project not found", Subtitle = "It may have been removed, or its todo.md deleted." }];
        }

        var todoPath = project.Location.TodoPath;
        var sections = _settings.ShowCompleted
            ? project.Sections
            : FilterOpenOnly(project.Sections);

        if (sections.Count == 0)
        {
            return [new ListItem
            {
                Title = "No tasks",
                Subtitle = _settings.ShowCompleted
                    ? "This project's todo.md has no tasks."
                    : "All tasks are complete - enable Show completed in Settings to see them.",
            }];
        }

        return sections
            .SelectMany(section => section.Items.Select(item => (Section: section.Name, Item: item)))
            .Select(t => (IListItem)new ListItem(new OpenPathCommand("Open todo.md", "📄", () => todoPath))
            {
                Title = t.Item.Text,
                Subtitle = t.Item.SubHeading ?? string.Empty,
                Section = t.Section,
                Icon = new IconInfo(t.Item.IsDone ? "✓" : "☐"),
            })
            .ToArray();
    }

    private static List<TodoSection> FilterOpenOnly(IReadOnlyList<TodoSection> sections) =>
        sections
            .Select(s => new TodoSection(s.Name, s.Items.Where(i => !i.IsDone).ToList()))
            .Where(s => s.Items.Count > 0)
            .ToList();
}
