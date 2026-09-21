using ClaudeTasks.Core;
using Xunit;

namespace ClaudeTasks.Tests;

public class TodoModelsTests
{
    [Fact]
    public void TodoSection_ComputesDoneAndTotalFromItems()
    {
        var section = new TodoSection("Phase 1", [
            new TodoItem("Do the thing", true),
            new TodoItem("Do another thing", false),
        ]);

        Assert.Equal(1, section.Done);
        Assert.Equal(2, section.Total);
    }

    [Fact]
    public void TodoDocument_SumsDoneAndTotalAcrossSections()
    {
        var document = new TodoDocument("Sample", [
            new TodoSection("Phase 1", [new TodoItem("A", true), new TodoItem("B", false)]),
            new TodoSection("Phase 2", [new TodoItem("C", true)]),
        ]);

        Assert.Equal("Sample", document.Title);
        Assert.Equal(2, document.Done);
        Assert.Equal(3, document.Total);
        Assert.Equal(200.0 / 3, document.Percent);
    }

    [Fact]
    public void TodoDocument_PercentIsNullWithNoSections()
    {
        var document = new TodoDocument(null, []);

        Assert.Null(document.Percent);
        Assert.Equal(0, document.Total);
    }

    [Fact]
    public void TodoItem_IndentAndSubHeadingDefaultToUnset()
    {
        var item = new TodoItem("Task", false);

        Assert.Equal(0, item.Indent);
        Assert.Null(item.SubHeading);
    }
}
