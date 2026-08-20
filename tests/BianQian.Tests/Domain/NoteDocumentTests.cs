using BianQian.App.Domain;
using System.Text.Json;

namespace BianQian.Tests.Domain;

public sealed class NoteDocumentTests
{
    [Fact]
    public void AddToday_creates_section_with_requested_date()
    {
        var document = NoteDocument.CreateEmpty();
        var date = new DateOnly(2026, 8, 20);

        var section = document.AddToday(date);

        Assert.Equal(date, section.Date);
        Assert.Single(document.Sections);
    }

    [Fact]
    public void Toggle_records_and_clears_completion_timestamp()
    {
        var task = new TaskBlock("Call client", DateTimeOffset.Parse("2026-08-20T08:00:00Z"));
        var completedAt = DateTimeOffset.Parse("2026-08-20T09:00:00Z");

        task.Toggle(completedAt);

        Assert.True(task.IsCompleted);
        Assert.Equal(completedAt, task.CompletedAt);

        task.Toggle(DateTimeOffset.Parse("2026-08-20T10:00:00Z"));

        Assert.False(task.IsCompleted);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Document_round_trips_sections_blocks_and_task_state()
    {
        var document = NoteDocument.CreateEmpty();
        var section = document.AddToday(new DateOnly(2026, 8, 20));
        section.IsCollapsed = true;
        section.Blocks.Add(new TextBlock("A note", DateTimeOffset.Parse("2026-08-20T08:00:00Z")));
        var task = new TaskBlock("Ship build", DateTimeOffset.Parse("2026-08-20T08:05:00Z"));
        task.Toggle(DateTimeOffset.Parse("2026-08-20T09:00:00Z"));
        section.Blocks.Add(task);

        var json = JsonSerializer.Serialize(document);
        var restored = JsonSerializer.Deserialize<NoteDocument>(json);

        Assert.NotNull(restored);
        var restoredSection = Assert.Single(restored.Sections);
        Assert.True(restoredSection.IsCollapsed);
        var restoredText = Assert.IsType<TextBlock>(restoredSection.Blocks[0]);
        Assert.Equal("A note", restoredText.Text);
        Assert.Equal(DateTimeOffset.Parse("2026-08-20T08:00:00Z"), restoredText.CreatedAt);
        var restoredTask = Assert.IsType<TaskBlock>(restoredSection.Blocks[1]);
        Assert.Equal(task.CreatedAt, restoredTask.CreatedAt);
        Assert.Equal(task.CompletedAt, restoredTask.CompletedAt);
        Assert.True(restoredTask.IsCompleted);
    }
}
