using ClaudeTasks.Core;
using Xunit;

namespace ClaudeTasks.Tests;

public class ActiveProjectDetectorTests
{
    [Fact]
    public void FindActiveCwd_PicksNewestJsonlAndExtractsItsCwd()
    {
        using var tmp = new TempDir();
        var projectsDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "projects"));
        var projA = Directory.CreateDirectory(Path.Combine(projectsDir.FullName, "proj-a"));
        var projB = Directory.CreateDirectory(Path.Combine(projectsDir.FullName, "proj-b"));

        var older = Path.Combine(projA.FullName, "older.jsonl");
        File.WriteAllText(older, "{\"cwd\":\"C:\\\\fake\\\\old-project\"}\n");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddHours(-2));

        var newer = Path.Combine(projB.FullName, "newer.jsonl");
        File.WriteAllText(newer, "{\"other\":1}\n{\"cwd\":\"C:\\\\fake\\\\new-project\"}\n");
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

        var cwd = ActiveProjectDetector.FindActiveCwd(tmp.Path);

        Assert.NotNull(cwd);
        Assert.EndsWith("new-project", cwd, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindActiveCwd_IgnoresJsonlInNestedSubagentFolders()
    {
        using var tmp = new TempDir();
        var projectsDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "projects"));
        var proj = Directory.CreateDirectory(Path.Combine(projectsDir.FullName, "proj-a"));
        var subagentDir = Directory.CreateDirectory(Path.Combine(proj.FullName, "subagents"));

        var nested = Path.Combine(subagentDir.FullName, "nested.jsonl");
        File.WriteAllText(nested, "{\"cwd\":\"C:\\\\fake\\\\nested-project\"}\n");
        File.SetLastWriteTimeUtc(nested, DateTime.UtcNow.AddHours(1)); // Newer, but must still be ignored.

        var topLevel = Path.Combine(proj.FullName, "top.jsonl");
        File.WriteAllText(topLevel, "{\"cwd\":\"C:\\\\fake\\\\top-project\"}\n");
        File.SetLastWriteTimeUtc(topLevel, DateTime.UtcNow);

        var cwd = ActiveProjectDetector.FindActiveCwd(tmp.Path);

        Assert.NotNull(cwd);
        Assert.EndsWith("top-project", cwd, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindActiveCwd_SkipsLinesWithoutCwdUntilOneIsFound()
    {
        using var tmp = new TempDir();
        var projectsDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "projects"));
        var proj = Directory.CreateDirectory(Path.Combine(projectsDir.FullName, "proj-a"));
        var file = Path.Combine(proj.FullName, "session.jsonl");
        File.WriteAllText(file, "{\"type\":\"summary\"}\n{\"type\":\"user\"}\n{\"cwd\":\"C:\\\\fake\\\\found-project\"}\n");

        var cwd = ActiveProjectDetector.FindActiveCwd(tmp.Path);

        Assert.NotNull(cwd);
        Assert.EndsWith("found-project", cwd, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindActiveCwd_NoJsonlFilesReturnsNull()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "projects"));

        Assert.Null(ActiveProjectDetector.FindActiveCwd(tmp.Path));
    }

    [Fact]
    public void FindActiveCwd_NoProjectsDirectoryReturnsNull()
    {
        using var tmp = new TempDir();

        Assert.Null(ActiveProjectDetector.FindActiveCwd(tmp.Path));
    }
}
