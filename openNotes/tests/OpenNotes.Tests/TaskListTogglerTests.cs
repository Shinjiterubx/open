using OpenNotes.Core.Markdown;
using Xunit;

namespace OpenNotes.Tests;

public class TaskListTogglerTests
{
    [Fact]
    public void Toggles_the_requested_task_only()
    {
        const string source = "- [ ] a\n- [ ] b\n- [x] c";
        Assert.Equal("- [ ] a\n- [x] b\n- [x] c", TaskListToggler.Toggle(source, 1));
        Assert.Equal("- [ ] a\n- [ ] b\n- [ ] c", TaskListToggler.Toggle(source, 2));
    }

    [Fact]
    public void Ignores_tasks_inside_code_fences()
    {
        const string source = "```\n- [ ] code\n```\n- [ ] real";
        Assert.Equal("```\n- [ ] code\n```\n- [x] real", TaskListToggler.Toggle(source, 0));
    }

    [Fact]
    public void Out_of_range_index_returns_input()
    {
        Assert.Equal("- [ ] a", TaskListToggler.Toggle("- [ ] a", 5));
    }
}
