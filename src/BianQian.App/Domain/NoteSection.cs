namespace BianQian.App.Domain;

public sealed class NoteSection
{
    public NoteSection(Guid id, DateOnly date)
    {
        Id = id;
        Date = date;
    }

    private NoteSection()
    {
    }

    public Guid Id { get; init; }

    public DateOnly Date { get; init; }

    public bool IsCollapsed { get; set; }

    public List<NoteBlock> Blocks { get; } = [];
}
