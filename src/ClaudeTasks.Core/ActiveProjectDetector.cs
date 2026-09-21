using System.Text;
using System.Text.Json;

namespace ClaudeTasks.Core;

/// <summary>
/// Finds the "active" project by newest transcript activity: the most recently written
/// *.jsonl directly under &lt;configDir&gt;/projects/*/ (one level - a subagent's own jsonl
/// lives one folder deeper and is deliberately not considered), then reads just enough of
/// that file to pull out its "cwd".
/// </summary>
public static class ActiveProjectDetector
{
    // Session/subagent transcripts are line-per-message; the cwd is recorded on essentially
    // every line near the start, so 200 lines is generous without risking reading a large
    // multi-MB transcript end to end just to answer "which folder is this".
    private const int MaxLinesToScan = 200;

    public static string? FindActiveCwd(string configDir)
    {
        var newestFile = FindNewestJsonl(Path.Combine(configDir, "projects"));
        return newestFile is null ? null : ReadCwd(newestFile);
    }

    private static string? FindNewestJsonl(string projectsDir)
    {
        string[] projectDirs;
        try
        {
            projectDirs = Directory.GetDirectories(projectsDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        string? newestFile = null;
        var newestTime = DateTime.MinValue;

        foreach (var dir in projectDirs)
        {
            string[] jsonlFiles;
            try
            {
                jsonlFiles = Directory.GetFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in jsonlFiles)
            {
                DateTime writeTime;
                try
                {
                    writeTime = File.GetLastWriteTimeUtc(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (writeTime > newestTime)
                {
                    newestTime = writeTime;
                    newestFile = file;
                }
            }
        }

        return newestFile;
    }

    private static string? ReadCwd(string file)
    {
        StreamReader reader;
        try
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        using (reader)
        {
            for (var i = 0; i < MaxLinesToScan; i++)
            {
                string? line;
                try
                {
                    line = reader.ReadLine();
                }
                catch (IOException)
                {
                    return null;
                }

                if (line is null)
                {
                    break;
                }

                // Cheap pre-filter before parsing: most lines in a transcript aren't the ones
                // carrying a top-level "cwd" property.
                if (line.IndexOf("\"cwd\"", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (TryExtractCwd(line) is string cwd)
                {
                    return cwd;
                }
            }
        }

        return null;
    }

    private static string? TryExtractCwd(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("cwd", out var cwdEl) ||
                cwdEl.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var raw = cwdEl.GetString();
            return raw is null ? null : PathNormalizer.Normalize(raw);
        }
        catch (JsonException)
        {
            return null; // Garbled line: keep scanning the rest of the file.
        }
    }
}
