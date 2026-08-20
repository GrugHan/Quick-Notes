using System.Text.Json.Serialization;

namespace BianQian.App.Domain;

public sealed class TaskBlock : NoteBlock
{
    public TaskBlock(string text, DateTimeOffset createdAt)
    {
        Text = text;
        CreatedAt = createdAt;
    }

    private TaskBlock()
    {
        Text = string.Empty;
    }

    [JsonConstructor]
    public TaskBlock(string text, DateTimeOffset createdAt, bool isCompleted, DateTimeOffset? completedAt)
    {
        Text = text;
        CreatedAt = createdAt;
        IsCompleted = isCompleted;
        CompletedAt = completedAt;
    }

    public string Text { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsCompleted { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public void Toggle(DateTimeOffset now)
    {
        if (IsCompleted)
        {
            IsCompleted = false;
            CompletedAt = null;
        }
        else
        {
            IsCompleted = true;
            CompletedAt = now;
        }
    }
}
