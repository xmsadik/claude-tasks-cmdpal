using ClaudeTasks.Core;
using Xunit;

namespace ClaudeTasks.Tests;

public class ClaudePathsTests
{
    [Fact]
    public void ConfigDir_UsesConfiguredDirectoryWhenClaudeConfigDirIsSet()
    {
        WithEnvironmentVariable("CLAUDE_CONFIG_DIR", @"C:\fake\claude-config", () =>
            Assert.Equal(@"C:\fake\claude-config", ClaudePaths.ConfigDir()));
    }

    [Fact]
    public void ConfigDir_FallsBackToDotClaudeUnderUserProfileWhenNotConfigured()
    {
        WithEnvironmentVariable("CLAUDE_CONFIG_DIR", null, () =>
            Assert.EndsWith(".claude", ClaudePaths.ConfigDir(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClaudeJsonPath_LivesInsideConfiguredDirectoryWhenClaudeConfigDirIsSet()
    {
        WithEnvironmentVariable("CLAUDE_CONFIG_DIR", @"C:\fake\claude-config", () =>
            Assert.Equal(@"C:\fake\claude-config\.claude.json", ClaudePaths.ClaudeJsonPath()));
    }

    [Fact]
    public void ClaudeJsonPath_FallsBackToUserProfileWhenNotConfigured()
    {
        WithEnvironmentVariable("CLAUDE_CONFIG_DIR", null, () =>
            Assert.EndsWith(".claude.json", ClaudePaths.ClaudeJsonPath(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Runs <paramref name="action"/> with CLAUDE_CONFIG_DIR temporarily overridden, then restores it - keeps this process-wide mutation from leaking into other tests.</summary>
    private static void WithEnvironmentVariable(string name, string? value, Action action)
    {
        var original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }
}
