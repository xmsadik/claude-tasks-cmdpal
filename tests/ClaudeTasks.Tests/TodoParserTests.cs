using ClaudeTasks.Core;
using Xunit;

namespace ClaudeTasks.Tests;

public class TodoParserTests
{
    [Fact]
    public void Parse_BasicDoneAndOpenCheckboxes()
    {
        var doc = TodoParser.Parse("- [x] Done thing\n- [ ] Open thing\n");

        Assert.Equal(2, doc.Total);
        Assert.Equal(1, doc.Done);
        var section = Assert.Single(doc.Sections);
        Assert.Equal("General", section.Name);
        Assert.True(section.Items[0].IsDone);
        Assert.False(section.Items[1].IsDone);
    }

    [Fact]
    public void Parse_DashAndTildeAndSlashCheckboxesAreOpen()
    {
        var doc = TodoParser.Parse("- [-] In progress\n- [~] Deferred\n- [/] Slash marker\n");

        Assert.Equal(3, doc.Total);
        Assert.Equal(0, doc.Done);
        Assert.All(doc.Sections[0].Items, i => Assert.False(i.IsDone));
    }

    [Fact]
    public void Parse_NumberedTasksWithDotAndParenMarkers()
    {
        var doc = TodoParser.Parse("1. [x] First\n2) [ ] Second\n");

        Assert.Equal(2, doc.Total);
        Assert.Equal(1, doc.Done);
    }

    [Fact]
    public void Parse_StarAndPlusBullets()
    {
        var doc = TodoParser.Parse("* [x] Star item\n+ [ ] Plus item\n");

        Assert.Equal(2, doc.Total);
        Assert.Equal(1, doc.Done);
    }

    [Fact]
    public void Parse_EmptyCheckboxTextIsIgnored()
    {
        var doc = TodoParser.Parse("- [ ] \n- [x] Real task\n");

        Assert.Equal(1, doc.Total);
        Assert.Equal("Real task", doc.Sections[0].Items[0].Text);
    }

    [Fact]
    public void Parse_FencedCodeBlockWithBackticksIsIgnored()
    {
        var doc = TodoParser.Parse("- [ ] Before\n```\n- [x] Inside fence\n```\n- [ ] After\n");

        Assert.Equal(2, doc.Total);
        Assert.DoesNotContain(doc.Sections[0].Items, i => i.Text == "Inside fence");
    }

    [Fact]
    public void Parse_FencedCodeBlockWithTildesIsIgnored()
    {
        var doc = TodoParser.Parse("- [ ] Before\n~~~\n- [x] Inside fence\n~~~\n- [ ] After\n");

        Assert.Equal(2, doc.Total);
        Assert.DoesNotContain(doc.Sections[0].Items, i => i.Text == "Inside fence");
    }

    [Fact]
    public void Parse_FencedCodeBlockWithLanguageTagIsIgnored()
    {
        var doc = TodoParser.Parse("- [ ] Before\n```csharp\n- [x] Inside fence\n```\n- [ ] After\n");

        Assert.Equal(2, doc.Total);
        Assert.DoesNotContain(doc.Sections[0].Items, i => i.Text == "Inside fence");
    }

    [Fact]
    public void Parse_BlockquoteLineIsIgnored()
    {
        var doc = TodoParser.Parse("- [ ] Real task\n> - [x] Quoted task\n");

        Assert.Equal(1, doc.Total);
        Assert.Equal("Real task", doc.Sections[0].Items[0].Text);
    }

    [Fact]
    public void Parse_FirstH1BecomesTitleAndLaterOnesAreIgnored()
    {
        var doc = TodoParser.Parse("# Project Title\n\n# Second H1\n\n- [ ] Task\n");

        Assert.Equal("Project Title", doc.Title);
    }

    [Fact]
    public void Parse_H2HeadingsStartNewSections()
    {
        var doc = TodoParser.Parse("## Phase 1\n- [x] A\n## Phase 2\n- [ ] B\n");

        Assert.Equal(2, doc.Sections.Count);
        Assert.Equal("Phase 1", doc.Sections[0].Name);
        Assert.Equal("Phase 2", doc.Sections[1].Name);
    }

    [Fact]
    public void Parse_H3DoesNotStartNewSectionButTagsFollowingItems()
    {
        var doc = TodoParser.Parse("## Phase 1\n- [x] A\n### Sub\n- [ ] B\n- [ ] C\n");

        var section = Assert.Single(doc.Sections);
        Assert.Equal("Phase 1", section.Name);
        Assert.Equal(3, section.Items.Count);
        Assert.Null(section.Items[0].SubHeading);
        Assert.Equal("Sub", section.Items[1].SubHeading);
        Assert.Equal("Sub", section.Items[2].SubHeading);
    }

    [Fact]
    public void Parse_TasksBeforeFirstH2GoToGeneralSection()
    {
        var doc = TodoParser.Parse("- [x] Before any heading\n## Real Section\n- [ ] After heading\n");

        Assert.Equal(2, doc.Sections.Count);
        Assert.Equal("General", doc.Sections[0].Name);
        Assert.Equal("Real Section", doc.Sections[1].Name);
    }

    [Fact]
    public void Parse_SectionsWithNoTasksAreOmitted()
    {
        var doc = TodoParser.Parse("## Empty Section\n## Has Tasks\n- [x] A\n");

        var section = Assert.Single(doc.Sections);
        Assert.Equal("Has Tasks", section.Name);
    }

    [Fact]
    public void Parse_NoTasksAnywhereYieldsNoSections()
    {
        var doc = TodoParser.Parse("# Title\n## Section A\nJust prose, no tasks.\n");

        Assert.Empty(doc.Sections);
        Assert.Null(doc.Percent);
    }

    [Fact]
    public void Parse_NestedItemsCountIndentButAreEachCountedOnce()
    {
        var doc = TodoParser.Parse("- [x] Parent\n  - [ ] Child\n    - [ ] Grandchild\n");

        var items = doc.Sections[0].Items;
        Assert.Equal(3, items.Count);
        Assert.Equal(0, items[0].Indent);
        Assert.Equal(2, items[1].Indent);
        Assert.Equal(4, items[2].Indent);
    }

    [Fact]
    public void Parse_TabIndentCountsAsFour()
    {
        var doc = TodoParser.Parse("\t- [ ] Tabbed\n");

        Assert.Equal(4, doc.Sections[0].Items[0].Indent);
    }

    [Fact]
    public void Parse_HandlesCrLfLineEndings()
    {
        var doc = TodoParser.Parse("## Section\r\n- [x] A\r\n- [ ] B\r\n");

        Assert.Equal(2, doc.Total);
        Assert.Equal("Section", doc.Sections[0].Name);
    }

    [Fact]
    public void Parse_StripsLeadingByteOrderMark()
    {
        var doc = TodoParser.Parse("﻿# Title\n- [x] A\n");

        Assert.Equal("Title", doc.Title);
        Assert.Equal(1, doc.Total);
    }

    [Fact]
    public void Parse_HandlesTurkishCharacters()
    {
        var doc = TodoParser.Parse("## Görevler\n- [x] Öğle yemeği hazırlığı\n- [ ] Şirket içi çalışma\n");

        Assert.Equal("Görevler", doc.Sections[0].Name);
        Assert.Equal("Öğle yemeği hazırlığı", doc.Sections[0].Items[0].Text);
        Assert.Equal("Şirket içi çalışma", doc.Sections[0].Items[1].Text);
    }

    [Fact]
    public void Parse_StripsBoldAndBacktickMarkersFromText()
    {
        var doc = TodoParser.Parse("- [x] **Bold** and `code` text\n");

        Assert.Equal("Bold and code text", doc.Sections[0].Items[0].Text);
    }

    [Fact]
    public void Parse_PercentIsNullWhenNoTasks()
    {
        var doc = TodoParser.Parse("# Title\nNo tasks here.\n");

        Assert.Null(doc.Percent);
        Assert.Equal(0, doc.Total);
    }

    [Fact]
    public void Parse_PercentIsComputedFromDoneOverTotal()
    {
        var doc = TodoParser.Parse("- [x] A\n- [x] B\n- [ ] C\n- [ ] D\n");

        Assert.Equal(50.0, doc.Percent);
    }
}
