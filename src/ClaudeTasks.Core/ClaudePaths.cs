namespace ClaudeTasks.Core;

/// <summary>Resolves the Claude Code config directory and its top-level ~/.claude.json.</summary>
public static class ClaudePaths
{
    public static string ConfigDir()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        var home = Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty;
        return Path.Combine(home, ".claude");
    }

    /// <summary>
    /// ~/.claude.json lives next to ~/.claude when CLAUDE_CONFIG_DIR is unset, but inside the
    /// configured directory itself when it is set - it is not simply "ConfigDir() + .json".
    /// </summary>
    public static string ClaudeJsonPath()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configured))
        {
            return Path.Combine(configured, ".claude.json");
        }

        var home = Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty;
        return Path.Combine(home, ".claude.json");
    }
}
