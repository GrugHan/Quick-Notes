using System.Text.Json.Serialization;

namespace BianQian.App.Domain;

public sealed class NoteSection
{
    public NoteSection(Guid id, DateOnly date)
    {
        Id = id;
        Date = date;
    }

    public NoteSection()
    {
    }

    [JsonConstructor]
    public NoteSection(Guid id, DateOnly date, bool isCollapsed, List<NoteBlock>? blocks)
    {
        Id = id;
        Date = date;
        IsCollapsed = isCollapsed;
        Blocks = blocks ?? [];
    }

    public Guid Id { get; init; }

    public DateOnly Date { get; init; }

    public bool IsCollapsed { get; set; }

    [JsonInclude]
    public List<NoteBlock> Blocks { get; private set; } = [];
}
