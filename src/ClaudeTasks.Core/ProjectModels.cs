namespace ClaudeTasks.Core;

/// <summary>
/// A local Claude project directory that has a tasks/todo.md, found via
/// <see cref="ProjectDiscovery"/>. <see cref="Name"/> is the actual on-disk folder name
/// (correct casing), not necessarily how it was spelled in ~/.claude.json or a scan root.
/// </summary>
public sealed record ProjectLocation(string Path, string Name, string TodoPath);

/// <summary>
/// A <see cref="ProjectLocation"/> paired with its freshly parsed todo.md and that file's
/// last-write time. The passthrough members let a caller treat a snapshot as a flattened
/// project + document without reaching through <see cref="Document"/> everywhere.
/// </summary>
public sealed record ProjectSnapshot(ProjectLocation Location, TodoDocument Document, DateTimeOffset LastWriteTime)
{
    public string Name => Location.Name;

    public string Path => Location.Path;

    public IReadOnlyList<TodoSection> Sections => Document.Sections;

    public int Done => Document.Done;

    public int Total => Document.Total;

    public double? Percent => Document.Percent;
}
