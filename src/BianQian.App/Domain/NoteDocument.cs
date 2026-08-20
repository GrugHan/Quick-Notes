namespace BianQian.App.Domain;

public sealed class NoteDocument
{
    private NoteDocument()
    {
    }

    public List<NoteSection> Sections { get; } = [];

    public static NoteDocument CreateEmpty() => new();

    public NoteSection AddToday(DateOnly date)
    {
        var section = new NoteSection(Guid.NewGuid(), date);
        Sections.Add(section);
        return section;
    }

    public TaskBlock AddTask(Guid sectionId, int index, DateTimeOffset createdAt)
    {
        var section = Sections.Single(section => section.Id == sectionId);
        var task = new TaskBlock(string.Empty, createdAt);
        section.Blocks.Insert(Math.Clamp(index, 0, section.Blocks.Count), task);
        return task;
    }
}
