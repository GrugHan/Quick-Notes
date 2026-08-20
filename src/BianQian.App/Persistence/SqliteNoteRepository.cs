using System.IO;
using System.Text.Json;
using BianQian.App.Domain;
using Microsoft.Data.Sqlite;

namespace BianQian.App.Persistence;

public sealed class SqliteNoteRepository : INoteRepository
{
    private const string CreateNoteStateTable = """
        CREATE TABLE IF NOT EXISTS note_state (
            id INTEGER PRIMARY KEY CHECK(id=1),
            document_json TEXT NOT NULL,
            updated_utc TEXT NOT NULL
        );
        """;

    private const string CreateSettingsTable = """
        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY,
            value_json TEXT NOT NULL
        );
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;

    private SqliteNoteRepository(string databasePath)
    {
        _connectionString = CreateConnectionString(databasePath);
    }

    public static SqliteNoteRepository ForPath(string databasePath) => new(databasePath);

    public async Task<NoteDocument> LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenInitializedConnectionAsync(cancellationToken, _connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT document_json FROM note_state WHERE id = 1;";

        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        return json is null
            ? NoteDocument.CreateEmpty()
            : JsonSerializer.Deserialize<NoteDocument>(json, JsonOptions) ?? NoteDocument.CreateEmpty();
    }

    public async Task SaveAsync(NoteDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var connection = await OpenInitializedConnectionAsync(cancellationToken, _connectionString);
        await BeginImmediateAsync(connection, cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO note_state (id, document_json, updated_utc)
                VALUES (1, $documentJson, $updatedUtc)
                ON CONFLICT(id) DO UPDATE SET
                    document_json = excluded.document_json,
                    updated_utc = excluded.updated_utc;
                """;
            command.Parameters.AddWithValue("$documentJson", JsonSerializer.Serialize(document, JsonOptions));
            command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);

            await CommitAsync(connection, cancellationToken);
        }
        catch
        {
            await RollbackAsync(connection);
            throw;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    internal static async Task<SqliteConnection> OpenInitializedConnectionAsync(CancellationToken cancellationToken, string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"{CreateNoteStateTable}{Environment.NewLine}{CreateSettingsTable}";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal static async Task BeginImmediateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "BEGIN IMMEDIATE;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task CommitAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "COMMIT;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task RollbackAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "ROLLBACK;";

        try
        {
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (SqliteException)
        {
            // A failed BEGIN IMMEDIATE leaves no active transaction to roll back.
        }
    }

    internal static string CreateConnectionString(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }
}
