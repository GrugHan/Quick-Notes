using BianQian.App.Domain;

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
}
