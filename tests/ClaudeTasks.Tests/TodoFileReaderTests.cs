using System.Text;
using ClaudeTasks.Core;
using Xunit;

namespace ClaudeTasks.Tests;

public class TodoFileReaderTests
{
    [Fact]
    public void Read_ReturnsFileContentsAsUtf8()
    {
        using var tmp = new TempDir();
        var file = Path.Combine(tmp.Path, "todo.md");
        File.WriteAllText(file, "- [x] Görev\n", new UTF8Encoding(false));

        Assert.Equal("- [x] Görev\n", TodoFileReader.Read(file));
    }

    [Fact]
    public void Read_MissingFileReturnsNull()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".md");

        Assert.Null(TodoFileReader.Read(missing));
    }

    [Fact]
    public void Read_StripsUtf8ByteOrderMark()
    {
        using var tmp = new TempDir();
        var file = Path.Combine(tmp.Path, "todo.md");
        File.WriteAllText(file, "- [x] A\n", new UTF8Encoding(true));

        var text = TodoFileReader.Read(file);

        Assert.NotNull(text);
        Assert.False(text!.StartsWith('﻿'));
        Assert.StartsWith("- [x] A", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_AllowsSharedReadWriteAccess()
    {
        using var tmp = new TempDir();
        var file = Path.Combine(tmp.Path, "todo.md");
        File.WriteAllText(file, "- [ ] A\n");

        using var writer = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        Assert.Equal("- [ ] A\n", TodoFileReader.Read(file));
    }
}
