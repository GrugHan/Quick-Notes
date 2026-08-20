using System.Text.Json;

namespace BianQian.App.Persistence;

public sealed class SettingsRepository : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;

    private SettingsRepository(string databasePath)
    {
        _connectionString = SqliteNoteRepository.CreateConnectionString(databasePath);
    }

    public static SettingsRepository ForPath(string databasePath) => new(databasePath);

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var connection = await SqliteNoteRepository.OpenInitializedConnectionAsync(cancellationToken, _connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value_json FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);

        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        return json is null ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var connection = await SqliteNoteRepository.OpenInitializedConnectionAsync(cancellationToken, _connectionString);
        await SqliteNoteRepository.BeginImmediateAsync(connection, cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO settings (key, value_json)
                VALUES ($key, $valueJson)
                ON CONFLICT(key) DO UPDATE SET value_json = excluded.value_json;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$valueJson", JsonSerializer.Serialize(value, JsonOptions));
            await command.ExecuteNonQueryAsync(cancellationToken);

            await SqliteNoteRepository.CommitAsync(connection, cancellationToken);
        }
        catch
        {
            await SqliteNoteRepository.RollbackAsync(connection);
            throw;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
