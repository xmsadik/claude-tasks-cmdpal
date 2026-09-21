using System.Text.RegularExpressions;

namespace ClaudeTasks.Core;

/// <summary>
/// Parses a todo.md's text into a <see cref="TodoDocument"/>. Pure string-in/model-out: no
/// file IO here, so it is trivial to unit test with synthetic fixtures and reusable from
/// anything that already has the text (e.g. an incremental-refresh loop that only re-reads
/// changed files).
/// </summary>
public static partial class TodoParser
{
    public static TodoDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // A caller may hand us the raw file text including its UTF-8 BOM (TodoFileReader
        // strips it, but Parse is also called directly in tests/callers with in-memory text).
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');

        string? title = null;
        var titleSeen = false;

        var sections = new List<TodoSection>();
        var sectionName = "General";
        var sectionItems = new List<TodoItem>();
        string? subHeading = null;

        var inFence = false;
        var fenceChar = '\0';
        var fenceLength = 0;

        foreach (var line in lines)
        {
            if (inFence)
            {
                var closing = FenceRegex().Match(line);
                if (closing.Success)
                {
                    var run = closing.Groups[1].Value;
                    if (run[0] == fenceChar && run.Length >= fenceLength)
                    {
                        inFence = false;
                    }
                }

                continue; // Fence delimiters and everything between them are never content.
            }

            var opening = FenceRegex().Match(line);
            if (opening.Success)
            {
                var run = opening.Groups[1].Value;
                fenceChar = run[0];
                fenceLength = run.Length;
                inFence = true;
                continue;
            }

            if (line.TrimStart().StartsWith('>'))
            {
                continue; // Blockquote line: ignored whole, including as a heading/task.
            }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                var level = heading.Groups[1].Value.Length;
                var headingText = StripFormatting(heading.Groups[2].Value);

                if (level == 1)
                {
                    if (!titleSeen && headingText.Length > 0)
                    {
                        title = headingText;
                        titleSeen = true;
                    }

                    continue;
                }

                if (level == 2)
                {
                    FlushSection(sections, sectionName, sectionItems);
                    sectionName = headingText.Length > 0 ? headingText : "General";
                    sectionItems = [];
                    subHeading = null;
                    continue;
                }

                // "###"+ tags following items in the current section; it does not split it.
                subHeading = headingText.Length > 0 ? headingText : null;
                continue;
            }

            var task = TaskLineRegex().Match(line);
            if (!task.Success)
            {
                continue;
            }

            var isDone = task.Groups[2].Value is "x" or "X";
            var itemText = StripFormatting(task.Groups[3].Value);
            if (itemText.Length == 0)
            {
                continue; // Formatting-only text (e.g. just "**") strips down to nothing.
            }

            sectionItems.Add(new TodoItem(itemText, isDone, ComputeIndent(line), subHeading));
        }

        FlushSection(sections, sectionName, sectionItems);

        return new TodoDocument(title, sections);
    }

    private static void FlushSection(List<TodoSection> sections, string name, List<TodoItem> items)
    {
        if (items.Count > 0)
        {
            sections.Add(new TodoSection(name, items));
        }
    }

    /// <summary>Leading-whitespace width of a line, tab counted as 4 (display nesting only).</summary>
    private static int ComputeIndent(string line)
    {
        var width = 0;
        foreach (var c in line)
        {
            if (c == '\t')
            {
                width += 4;
            }
            else if (c == ' ')
            {
                width += 1;
            }
            else
            {
                break;
            }
        }

        return width;
    }

    /// <summary>Strips only "**" and backtick markers from task/heading text, then trims.</summary>
    private static string StripFormatting(string text) =>
        text.Replace("**", string.Empty).Replace("`", string.Empty).Trim();

    // x/X = done; any other single char inside [ ] (space, -, ~, /, ...) = open. The trailing
    // (\S.*) requires non-empty text, so an empty checkbox like "- [ ] " never matches at all.
    [GeneratedRegex(@"^\s*([-*+]|\d+[.)])\s+\[([^\]])\]\s+(\S.*)$")]
    private static partial Regex TaskLineRegex();

    // ATX heading: 1-6 '#' then a space then the text. Level = number of '#'.
    [GeneratedRegex(@"^\s*(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    // A fenced-code-block delimiter: 3+ backticks or 3+ tildes at line start (optional leading
    // whitespace, optional trailing language tag on the opening fence - both ignored here).
    [GeneratedRegex(@"^\s*(`{3,}|~{3,})")]
    private static partial Regex FenceRegex();
}
