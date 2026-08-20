using BianQian.App.Domain;
using BianQian.App.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace BianQian.Tests.Persistence;

public sealed class SqliteNoteRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"bianqian-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Load_on_first_run_returns_an_empty_document()
    {
        await using var repo = SqliteNoteRepository.ForPath(_databasePath);

        var document = await repo.LoadAsync(CancellationToken.None);

        document.Should().BeEquivalentTo(NoteDocument.CreateEmpty());
    }

    [Fact]
    public async Task Save_then_load_preserves_task_and_fold_state()
    {
        await using var repo = SqliteNoteRepository.ForPath(_databasePath);
        var document = NoteDocument.CreateEmpty();
        var section = document.AddToday(new DateOnly(2026, 8, 20));
        section.IsCollapsed = true;
        section.Blocks.Add(new TaskBlock("Ship build", DateTimeOffset.UtcNow));

        await repo.SaveAsync(document, CancellationToken.None);

        (await repo.LoadAsync(CancellationToken.None)).Should().BeEquivalentTo(document);
    }

    [Fact]
    public async Task Settings_save_then_load_preserves_a_typed_value_by_key()
    {
        await using var settings = SettingsRepository.ForPath(_databasePath);
        var expected = new TestSetting("tomato", 24);

        await settings.SaveAsync("appearance", expected, CancellationToken.None);

        var actual = await settings.LoadAsync<TestSetting>("appearance", CancellationToken.None);

        actual.Should().Be(expected);
    }

    [Fact]
    public async Task Settings_load_returns_null_when_key_does_not_exist()
    {
        await using var settings = SettingsRepository.ForPath(_databasePath);

        var actual = await settings.LoadAsync<TestSetting>("missing", CancellationToken.None);

        actual.Should().BeNull();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed record TestSetting(string Accent, int FontSize);
}
