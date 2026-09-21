using System.Text;
using System.Text.Json;

namespace ClaudeTasks.Core;

/// <summary>
/// Finds local Claude project directories: the ones ~/.claude.json already knows about, plus
/// whatever "extra scan roots" the user configures (a project opened from a home-directory
/// session records the home dir as its cwd, not the actual project subfolder - see
/// <see cref="ActiveProjectDetector"/> - so scan roots are the fallback for those).
/// </summary>
public static class ProjectDiscovery
{
    /// <summary>
    /// Property names of the top-level "projects" object in ~/.claude.json. Claude Code
    /// rewrites this file constantly, so it is read with shared access. A missing file is a
    /// legitimate "nothing registered yet" and returns an empty list; a file that exists but
    /// can't be read or parsed (e.g. caught mid-write) is a <em>transient</em> failure and
    /// returns null - the caller is expected to keep whatever list it already had rather than
    /// treat null the same as "no projects".
    /// </summary>
    public static IReadOnlyList<string>? ReadRegisteredProjects(string claudeJsonPath)
    {
        string text;
        try
        {
            if (!File.Exists(claudeJsonPath))
            {
                return [];
            }

            using var stream = new FileStream(claudeJsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("projects", out var projects) ||
                projects.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var names = new List<string>();
            foreach (var property in projects.EnumerateObject())
            {
                names.Add(property.Name);
            }

            return names;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Splits a ";"-separated "Extra scan roots" setting and expands %ENV% vars in each entry.</summary>
    public static IReadOnlyList<string> ParseRoots(string? rootsSetting)
    {
        if (string.IsNullOrWhiteSpace(rootsSetting))
        {
            return [];
        }

        var roots = new List<string>();
        foreach (var part in rootsSetting.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var expanded = Environment.ExpandEnvironmentVariables(part);
            if (expanded.Length > 0)
            {
                roots.Add(expanded);
            }
        }

        return roots;
    }

    /// <summary>
    /// Depth-1 scan: for each root, every immediate subdirectory that has a tasks/todo.md.
    /// Inaccessible, hidden/system, and dot-prefixed directories are skipped silently.
    /// </summary>
    public static IReadOnlyList<ProjectLocation> ScanRoots(IEnumerable<string> roots)
    {
        var results = new List<ProjectLocation>();

        foreach (var root in roots)
        {
            var expanded = Environment.ExpandEnvironmentVariables(root);

            DirectoryInfo rootInfo;
            IEnumerable<DirectoryInfo> children;
            try
            {
                rootInfo = new DirectoryInfo(expanded);
                if (!rootInfo.Exists)
                {
                    continue;
                }

                children = rootInfo.EnumerateDirectories();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                continue;
            }

            foreach (var dir in children)
            {
                if (IsHiddenOrDotDir(dir))
                {
                    continue;
                }

                string todoPath;
                try
                {
                    todoPath = Path.Combine(dir.FullName, "tasks", "todo.md");
                    if (!File.Exists(todoPath))
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                results.Add(new ProjectLocation(dir.FullName, dir.Name, todoPath));
            }
        }

        return results;
    }

    /// <summary>
    /// Registered projects (as already read by the caller, e.g. via <see cref="ReadRegisteredProjects"/>)
    /// union'd with a roots scan: normalized, deduped case-insensitively, and filtered down to
    /// directories that still exist and still have a tasks/todo.md. A path registered twice
    /// (e.g. differently-cased or with forward slashes) collapses into one <see cref="ProjectLocation"/>.
    /// </summary>
    public static IReadOnlyList<ProjectLocation> Discover(IReadOnlyList<string> registeredPaths, IEnumerable<string> roots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ProjectLocation>();

        foreach (var rawPath in registeredPaths)
        {
            if (ResolveRegistered(rawPath) is ProjectLocation location && seen.Add(location.Path))
            {
                result.Add(location);
            }
        }

        foreach (var location in ScanRoots(roots))
        {
            if (seen.Add(location.Path))
            {
                result.Add(location);
            }
        }

        return result;
    }

    /// <summary>Convenience overload: reads ~/.claude.json fresh (a read/parse failure is treated as "no registered projects").</summary>
    public static IReadOnlyList<ProjectLocation> Discover(string claudeJsonPath, IEnumerable<string> roots) =>
        Discover(ReadRegisteredProjects(claudeJsonPath) ?? [], roots);

    private static ProjectLocation? ResolveRegistered(string rawPath)
    {
        var normalized = PathNormalizer.Normalize(rawPath);
        if (normalized is null)
        {
            return null;
        }

        DirectoryInfo dir;
        try
        {
            dir = new DirectoryInfo(normalized);
            if (!dir.Exists)
            {
                return null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        var todoPath = Path.Combine(dir.FullName, "tasks", "todo.md");
        if (!File.Exists(todoPath))
        {
            return null;
        }

        return new ProjectLocation(dir.FullName, ResolveOnDiskName(dir), todoPath);
    }

    /// <summary>
    /// ~/.claude.json may spell a project path in any casing; DirectoryInfo.Name can echo that
    /// casing back verbatim rather than the disk's. Resolving it through the parent's own
    /// directory listing gets the real on-disk spelling (e.g. "My Project", not "my project").
    /// </summary>
    private static string ResolveOnDiskName(DirectoryInfo dir)
    {
        var parent = dir.Parent;
        if (parent is null || dir.Name.Contains('*') || dir.Name.Contains('?'))
        {
            // No parent to resolve against, or the name itself contains search-pattern
            // wildcards - use it as a literal EnumerateDirectories pattern below and it could
            // match siblings it has no business matching, so just keep the caller-provided name.
            return dir.Name;
        }

        try
        {
            return parent.EnumerateDirectories(dir.Name).FirstOrDefault()?.Name ?? dir.Name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall through: caller-provided casing is still a reasonable fallback.
            return dir.Name;
        }
    }

    private static bool IsHiddenOrDotDir(DirectoryInfo dir)
    {
        if (dir.Name.StartsWith('.'))
        {
            return true;
        }

        try
        {
            return dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Attributes.HasFlag(FileAttributes.System);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
