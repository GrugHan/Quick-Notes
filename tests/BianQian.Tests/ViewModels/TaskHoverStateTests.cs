using BianQian.App.ViewModels;
using FluentAssertions;

namespace BianQian.Tests.ViewModels;

public sealed class TaskHoverStateTests
{
    private const string Metadata = "创建：2026-08-20 08:00；完成：2026-08-20 09:00";

    [Fact]
    public void Completion_while_pointer_remains_over_task_starts_full_three_second_delay()
    {
        var state = new TaskHoverState();
        state.PointerEntered(false, null);

        state.CompletionChanged(true, Metadata);
        state.Advance(TimeSpan.FromMilliseconds(2999));

        state.IsDelayPending.Should().BeTrue();
        state.VisibleMetadata.Should().BeNull();

        state.Advance(TimeSpan.FromMilliseconds(1));

        state.IsDelayPending.Should().BeFalse();
        state.VisibleMetadata.Should().Be(Metadata);
    }

    [Fact]
    public void Becoming_incomplete_while_hovered_clears_visible_metadata_immediately()
    {
        var state = new TaskHoverState();
        state.PointerEntered(true, Metadata);
        state.Advance(TimeSpan.FromSeconds(3));
        state.VisibleMetadata.Should().Be(Metadata);

        state.CompletionChanged(false, null);

        state.IsDelayPending.Should().BeFalse();
        state.VisibleMetadata.Should().BeNull();
    }
}
