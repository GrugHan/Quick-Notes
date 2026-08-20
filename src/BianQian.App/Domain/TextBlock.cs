namespace BianQian.App.Domain;

public sealed class TextBlock : NoteBlock
{
    public TextBlock(string text, DateTimeOffset createdAt)
    {
        Text = text;
        CreatedAt = createdAt;
    }

    private TextBlock()
    {
        Text = string.Empty;
    }

    public string Text { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
