namespace ClaudeTasks.Core;

/// <summary>
/// A single "- [ ] ..." / "- [x] ..." line parsed from a todo.md. <see cref="Indent"/> is the
/// leading-whitespace width (tab counted as 4) used only for nested display - every item still
/// counts once towards its <see cref="TodoSection"/>'s <see cref="TodoSection.Done"/>/
/// <see cref="TodoSection.Total"/>, regardless of nesting.
/// </summary>
public sealed record TodoItem(string Text, bool IsDone, int Indent = 0, string? SubHeading = null);

/// <summary>
/// A "##" heading in a todo.md, grouping the <see cref="TodoItem"/>s under it (including any
/// that fall under a "###"+ sub-heading further down the same section - see
/// <see cref="TodoItem.SubHeading"/>).
/// </summary>
public sealed record TodoSection(string Name, IReadOnlyList<TodoItem> Items)
{
    public int Done => Items.Count(i => i.IsDone);

    public int Total => Items.Count;
}

/// <summary>
/// A parsed todo.md: an optional "# " title plus its "##" sections. <see cref="TodoParser"/>
/// never emits a section with zero tasks, so every entry in <see cref="Sections"/> has at
/// least one <see cref="TodoItem"/>.
/// </summary>
public sealed record TodoDocument(string? Title, IReadOnlyList<TodoSection> Sections)
{
    public int Done => Sections.Sum(s => s.Done);

    public int Total => Sections.Sum(s => s.Total);

    /// <summary>0-100. Null when <see cref="Total"/> is 0 ("no tasks" is not "0%").</summary>
    public double? Percent => Total == 0 ? null : 100.0 * Done / Total;
}
