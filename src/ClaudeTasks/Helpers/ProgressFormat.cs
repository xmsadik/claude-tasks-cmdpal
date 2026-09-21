using System;
using System.Globalization;
using System.Linq;
using System.Text;
using ClaudeTasks.Core;

namespace ClaudeTasks;

/// <summary>Small formatting helpers shared by the Dock band, ContentPage flyout, and ListPages.</summary>
internal static class ProgressFormat
{
    public static (int Done, int Total) TaskCounts(this ProjectSnapshot project)
    {
        var items = project.Sections.SelectMany(s => s.Items).ToList();
        return (items.Count(i => i.IsDone), items.Count);
    }

    public static int PercentDone(this ProjectSnapshot project)
    {
        var (done, total) = project.TaskCounts();
        return PercentDone(done, total);
    }

    /// <summary>
    /// Floor(done*100/total) via integer math, so the result is 100 only when done==total - e.g.
    /// 199/200 is 99%, never rounded up to a misleading "100%"/✅ while still open.
    /// </summary>
    public static int PercentDone(int done, int total) => total <= 0 ? 0 : (int)((long)done * 100 / total);

    /// <summary>Renders a 5-block bar, e.g. "▰▰▰▱▱" for 62%. Filled cells are floored, same as <see cref="PercentDone(int, int)"/>.</summary>
    public static string Bar(int percent, int blocks = 5)
    {
        var filled = Math.Clamp(percent * blocks / 100, 0, blocks);
        return new string('▰', filled) + new string('▱', blocks - filled);
    }

    public static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Backslash-escapes markdown-significant characters in project/task text before it goes into a table cell or bullet.</summary>
    public static string EscapeMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\\' or '|' or '*' or '_' or '`' or '[' or ']' or '<' or '>' or '#' or '~')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>"just now" / "5m ago" / "3h ago" / "2d ago" / "4mo ago" / "1y ago". Never negative (clamps future timestamps to "just now").</summary>
    public static string RelativeTime(DateTimeOffset when, DateTimeOffset now)
    {
        var span = now - when;
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalSeconds < 60)
        {
            return "just now";
        }

        if (span.TotalMinutes < 60)
        {
            return Invariant((int)span.TotalMinutes) + "m ago";
        }

        if (span.TotalHours < 24)
        {
            return Invariant((int)span.TotalHours) + "h ago";
        }

        if (span.TotalDays < 30)
        {
            return Invariant((int)span.TotalDays) + "d ago";
        }

        if (span.TotalDays < 365)
        {
            return Invariant((int)(span.TotalDays / 30)) + "mo ago";
        }

        return Invariant((int)(span.TotalDays / 365)) + "y ago";
    }
}
