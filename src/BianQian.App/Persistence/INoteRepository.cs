using BianQian.App.Domain;

namespace BianQian.App.Persistence;

public interface INoteRepository : IAsyncDisposable
{
    Task<NoteDocument> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(NoteDocument document, CancellationToken cancellationToken);
}
