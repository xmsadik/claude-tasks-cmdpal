using System.Text;

namespace ClaudeTasks.Core;

/// <summary>
/// Reads a todo.md off disk without locking out a concurrently-writing Claude session -
/// mirrors the reference project's transcript-reading pattern of sharing read access and
/// treating any IO failure as "nothing to read" rather than throwing.
/// </summary>
public static class TodoFileReader
{
    /// <summary>Reads the whole file as UTF-8 (BOM aware). Returns null on any IO error.</summary>
    public static string? Read(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
