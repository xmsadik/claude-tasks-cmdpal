namespace ClaudeTasks.Tests;

/// <summary>
/// A throwaway directory under the OS temp folder, deleted on Dispose. Every
/// discovery/file-reading test uses one of these instead of a real project path, so nothing
/// here is machine- or user-specific.
/// </summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ClaudeTasksTests_" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp dir isn't worth failing the test over.
        }
    }
}

/// <summary>JSON-string escaping for the synthetic .claude.json fixtures below (backslashes only - our test paths never contain quotes or control chars).</summary>
internal static class JsonTestHelpers
{
    public static string EscapePath(string path) => path.Replace("\\", "\\\\");
}
