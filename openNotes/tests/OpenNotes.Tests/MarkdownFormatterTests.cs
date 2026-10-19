using OpenNotes.Core.Editing;
using Xunit;

namespace OpenNotes.Tests;

public class MarkdownFormatterTests
{
    private readonly MarkdownFormatter _formatter = new();

    [Fact]
    public void Bold_wraps_selection()
    {
        var surface = new FakeTextSurface("hello [world]");
        _formatter.ToggleWrap(surface, "**", "**", "x");
        Assert.Equal("hello **world**", surface.Normalized);
    }

    [Fact]
    public void Bold_twice_unwraps()
    {
        var surface = new FakeTextSurface("hello [world]");
        _formatter.ToggleWrap(surface, "**", "**", "x");
        _formatter.ToggleWrap(surface, "**", "**", "x");
        Assert.Equal("hello world", surface.Normalized);
    }

    [Fact]
    public void Heading_toggles_on_and_off()
    {
        var surface = new FakeTextSurface("Title|");
        _formatter.ToggleBlock(surface, BlockStyle.Heading1);
        Assert.Equal("# Title", surface.Normalized);
        _formatter.ToggleBlock(surface, BlockStyle.Heading1);
        Assert.Equal("Title", surface.Normalized);
    }

    [Fact]
    public void Heading_replaces_other_heading_level()
    {
        var surface = new FakeTextSurface("## Title|");
        _formatter.ToggleBlock(surface, BlockStyle.Heading1);
        Assert.Equal("# Title", surface.Normalized);
    }

    [Fact]
    public void Numbered_list_numbers_each_line()
    {
        var surface = new FakeTextSurface("[a\nb\nc]");
        _formatter.ToggleBlock(surface, BlockStyle.NumberedList);
        Assert.Equal("1. a\n2. b\n3. c", surface.Normalized);
    }

    [Fact]
    public void Task_list_turns_bullets_into_tasks()
    {
        var surface = new FakeTextSurface("[- milk\n- bread]");
        _formatter.ToggleBlock(surface, BlockStyle.TaskList);
        Assert.Equal("- [ ] milk\n- [ ] bread", surface.Normalized);
    }

    [Fact]
    public void Ctrl_enter_checks_task()
    {
        var surface = new FakeTextSurface("- [ ] milk|");
        _formatter.ToggleTaskAtCaret(surface);
        Assert.Equal("- [x] milk", surface.Normalized);
    }

    [Theory]
    [InlineData("- item|", "- item\n- ")]
    [InlineData("- [x] done|", "- [x] done\n- [ ] ")]
    [InlineData("3. third|", "3. third\n4. ")]
    [InlineData("  * nested|", "  * nested\n  * ")]
    public void Enter_continues_lists(string markup, string expected)
    {
        var surface = new FakeTextSurface(markup);
        Assert.True(_formatter.TryContinueList(surface));
        Assert.Equal(expected, surface.Normalized);
    }

    [Fact]
    public void Enter_on_empty_item_ends_list()
    {
        var surface = new FakeTextSurface("- a\n- |");
        Assert.True(_formatter.TryContinueList(surface));
        Assert.Equal("- a\n", surface.Normalized);
    }

    [Fact]
    public void Enter_on_plain_text_uses_default_behaviour()
    {
        var surface = new FakeTextSurface("plain|");
        Assert.False(_formatter.TryContinueList(surface));
    }

    [Fact]
    public void Rule_after_paragraph_gets_blank_line()
    {
        var surface = new FakeTextSurface("text|");
        _formatter.InsertBlock(surface, "---");
        Assert.Equal("text\n\n---\n", surface.Normalized);
    }
}
