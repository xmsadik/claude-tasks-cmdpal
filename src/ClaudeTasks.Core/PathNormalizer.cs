namespace ClaudeTasks.Core;

/// <summary>
/// Shared "/" -> "\", full-path, no-trailing-separator normalization used by both
/// <see cref="ProjectDiscovery"/> (dedupe) and <see cref="ActiveProjectDetector"/> (the cwd
/// read out of a transcript line).
/// </summary>
internal static class PathNormalizer
{
    public static string? Normalize(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(rawPath.Replace('/', '\\'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return full.TrimEnd('\\');
    }
}
