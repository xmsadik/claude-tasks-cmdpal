using ClaudeTasks.Core;
using Xunit;

namespace ClaudeTasks.Tests;

public class ProjectDiscoveryTests
{
    [Fact]
    public void ReadRegisteredProjects_MalformedJsonReturnsNull()
    {
        using var tmp = new TempDir();
        var claudeJson = Path.Combine(tmp.Path, ".claude.json");
        File.WriteAllText(claudeJson, "{ not valid json ");

        Assert.Null(ProjectDiscovery.ReadRegisteredProjects(claudeJson));
    }

    [Fact]
    public void ReadRegisteredProjects_MissingFileReturnsEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".json");

        Assert.Empty(ProjectDiscovery.ReadRegisteredProjects(missing)!);
    }

    [Fact]
    public void ReadRegisteredProjects_MissingProjectsKeyReturnsEmpty()
    {
        using var tmp = new TempDir();
        var claudeJson = Path.Combine(tmp.Path, ".claude.json");
        File.WriteAllText(claudeJson, "{ \"other\": 1 }");

        Assert.Empty(ProjectDiscovery.ReadRegisteredProjects(claudeJson)!);
    }

    [Fact]
    public void ReadRegisteredProjects_EmptyProjectsObjectReturnsEmpty()
    {
        using var tmp = new TempDir();
        var claudeJson = Path.Combine(tmp.Path, ".claude.json");
        File.WriteAllText(claudeJson, "{ \"projects\": {} }");

        Assert.Empty(ProjectDiscovery.ReadRegisteredProjects(claudeJson)!);
    }

    [Fact]
    public void ReadRegisteredProjects_ReturnsProjectsKeyPropertyNames()
    {
        using var tmp = new TempDir();
        var claudeJson = Path.Combine(tmp.Path, ".claude.json");
        File.WriteAllText(claudeJson, $$"""
            { "projects": { "{{JsonTestHelpers.EscapePath(@"C:\fake\a")}}": {}, "{{JsonTestHelpers.EscapePath(@"C:\fake\b")}}": {} } }
            """);

        var names = ProjectDiscovery.ReadRegisteredProjects(claudeJson);

        Assert.Equal(2, names!.Count);
    }

    [Fact]
    public void ParseRoots_SplitsOnSemicolonAndTrims()
    {
        var roots = ProjectDiscovery.ParseRoots(@" C:\a ; C:\b ");

        Assert.Equal([@"C:\a", @"C:\b"], roots);
    }

    [Fact]
    public void ParseRoots_ExpandsEnvironmentVariables()
    {
        using var tmp = new TempDir();
        const string varName = "CLAUDE_TASKS_TEST_ROOT";
        Environment.SetEnvironmentVariable(varName, tmp.Path);
        try
        {
            var roots = ProjectDiscovery.ParseRoots($"%{varName}%");

            Assert.Equal([tmp.Path], roots);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    public void ScanRoots_FindsImmediateSubdirectoryWithTodo()
    {
        using var tmp = new TempDir();
        var project = Directory.CreateDirectory(Path.Combine(tmp.Path, "SiblingProject"));
        Directory.CreateDirectory(Path.Combine(project.FullName, "tasks"));
        File.WriteAllText(Path.Combine(project.FullName, "tasks", "todo.md"), "- [ ] A\n");

        var locations = ProjectDiscovery.ScanRoots([tmp.Path]);

        var location = Assert.Single(locations);
        Assert.Equal("SiblingProject", location.Name);
        Assert.Equal(project.FullName, location.Path);
    }

    [Fact]
    public void ScanRoots_DoesNotDescendBeyondImmediateChildren()
    {
        using var tmp = new TempDir();
        var nested = Directory.CreateDirectory(Path.Combine(tmp.Path, "Outer", "Inner"));
        Directory.CreateDirectory(Path.Combine(nested.FullName, "tasks"));
        File.WriteAllText(Path.Combine(nested.FullName, "tasks", "todo.md"), "- [ ] A\n");

        var locations = ProjectDiscovery.ScanRoots([tmp.Path]);

        Assert.Empty(locations);
    }

    [Fact]
    public void ScanRoots_SkipsDirectoryWithoutTodo()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "NoTodo"));

        Assert.Empty(ProjectDiscovery.ScanRoots([tmp.Path]));
    }

    [Fact]
    public void ScanRoots_SkipsDotDirectories()
    {
        using var tmp = new TempDir();
        var dotDir = Directory.CreateDirectory(Path.Combine(tmp.Path, ".hidden"));
        Directory.CreateDirectory(Path.Combine(dotDir.FullName, "tasks"));
        File.WriteAllText(Path.Combine(dotDir.FullName, "tasks", "todo.md"), "- [ ] A\n");

        Assert.Empty(ProjectDiscovery.ScanRoots([tmp.Path]));
    }

    [Fact]
    public void Discover_SkipsRegisteredPathsThatNoLongerExist()
    {
        using var tmp = new TempDir();
        var missing = Path.Combine(tmp.Path, "DoesNotExist");
        var claudeJson = WriteClaudeJson(tmp.Path, missing);

        Assert.Empty(ProjectDiscovery.Discover(claudeJson, []));
    }

    [Fact]
    public void Discover_SkipsRegisteredPathWithoutTodoFile()
    {
        using var tmp = new TempDir();
        var projectDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "NoTodo"));
        var claudeJson = WriteClaudeJson(tmp.Path, projectDir.FullName);

        Assert.Empty(ProjectDiscovery.Discover(claudeJson, []));
    }

    [Fact]
    public void Discover_DedupesCaseAndSlashVariantsOfTheSamePath()
    {
        using var tmp = new TempDir();
        var projectDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "MyProject"));
        Directory.CreateDirectory(Path.Combine(projectDir.FullName, "tasks"));
        File.WriteAllText(Path.Combine(projectDir.FullName, "tasks", "todo.md"), "- [x] A\n");

        var claudeJson = Path.Combine(tmp.Path, ".claude.json");
        var lowerSlash = projectDir.FullName.ToLowerInvariant().Replace('\\', '/');
        File.WriteAllText(claudeJson, $$"""
            { "projects": { "{{JsonTestHelpers.EscapePath(projectDir.FullName)}}": {}, "{{lowerSlash}}": {} } }
            """);

        var locations = ProjectDiscovery.Discover(claudeJson, []);

        var location = Assert.Single(locations);
        Assert.Equal("MyProject", location.Name);
    }

    [Fact]
    public void Discover_UsesOnDiskFolderNameCasingForARegisteredProject()
    {
        using var tmp = new TempDir();
        var projectDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "Sample Project"));
        Directory.CreateDirectory(Path.Combine(projectDir.FullName, "tasks"));
        File.WriteAllText(Path.Combine(projectDir.FullName, "tasks", "todo.md"), "- [ ] A\n");

        var claudeJson = WriteClaudeJson(tmp.Path, projectDir.FullName.ToLowerInvariant());

        var location = Assert.Single(ProjectDiscovery.Discover(claudeJson, []));

        Assert.Equal("Sample Project", location.Name);
    }

    [Fact]
    public void Discover_UnionsRegisteredAndScannedProjectsWithoutDuplicates()
    {
        using var tmp = new TempDir();

        var registeredDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "Registered"));
        Directory.CreateDirectory(Path.Combine(registeredDir.FullName, "tasks"));
        File.WriteAllText(Path.Combine(registeredDir.FullName, "tasks", "todo.md"), "- [ ] A\n");

        var scanRoot = Directory.CreateDirectory(Path.Combine(tmp.Path, "roots"));
        var scannedDir = Directory.CreateDirectory(Path.Combine(scanRoot.FullName, "Scanned"));
        Directory.CreateDirectory(Path.Combine(scannedDir.FullName, "tasks"));
        File.WriteAllText(Path.Combine(scannedDir.FullName, "tasks", "todo.md"), "- [ ] B\n");

        var claudeJson = WriteClaudeJson(tmp.Path, registeredDir.FullName);

        var locations = ProjectDiscovery.Discover(claudeJson, [scanRoot.FullName]);

        Assert.Equal(2, locations.Count);
        Assert.Contains(locations, l => l.Name == "Registered");
        Assert.Contains(locations, l => l.Name == "Scanned");
    }

    private static string WriteClaudeJson(string dir, string projectPath)
    {
        var claudeJson = Path.Combine(dir, ".claude.json");
        File.WriteAllText(claudeJson, $$"""
            { "projects": { "{{JsonTestHelpers.EscapePath(projectPath)}}": {} } }
            """);
        return claudeJson;
    }
}
