using Microsoft.Data.Sqlite;

namespace SnipShelf.Services;

/// <summary>
/// Brings the vault database up to date on every launch: creates it if it is missing, applies
/// the schema, and — only the very first time — inserts the seeds.
/// </summary>
public sealed class DatabaseBootstrapper(
    VaultDatabase database,
    ISettingsService settings)
{
    /// <summary>Bumped whenever the DDL in <see cref="SchemaSql"/> changes shape.</summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// Appendix B of the PRD, verbatim. Everything is IF NOT EXISTS so a launch against an
    /// existing vault is a no-op rather than an error.
    /// </summary>
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS Meta (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Snippets (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          Title TEXT NOT NULL,
          Body TEXT NOT NULL,
          Kind TEXT NOT NULL DEFAULT 'snippet',
          Favorite INTEGER NOT NULL DEFAULT 0,
          CreatedUtc TEXT NOT NULL,
          UpdatedUtc TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_Snippets_Updated ON Snippets(UpdatedUtc DESC);
        CREATE TABLE IF NOT EXISTS Tags (
          Id INTEGER PRIMARY KEY AUTOINCREMENT,
          Name TEXT NOT NULL UNIQUE COLLATE NOCASE
        );
        CREATE TABLE IF NOT EXISTS SnippetTags (
          SnippetId INTEGER NOT NULL REFERENCES Snippets(Id) ON DELETE CASCADE,
          TagId     INTEGER NOT NULL REFERENCES Tags(Id)     ON DELETE CASCADE,
          PRIMARY KEY (SnippetId, TagId)
        );
        """;

    public string DatabasePath => database.DatabasePath;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.Run(
        async () =>
        {
            Directory.CreateDirectory(database.DataFolder);
            ApplySchema();
            await SeedFirstRunAsync(cancellationToken).ConfigureAwait(false);
        },
        cancellationToken);

    private void ApplySchema()
    {
        using var connection = database.OpenConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = SchemaSql;
            command.ExecuteNonQuery();
        }

        using var version = connection.CreateCommand();
        version.CommandText = """
            INSERT INTO Meta (Key, Value) VALUES ('schema_version', @version)
            ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value;
            """;
        version.Parameters.AddWithValue("@version", SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        version.ExecuteNonQuery();
    }

    /// <summary>
    /// Seeds once, guarded by the database marker rather than by looking for the records: a user who
    /// deletes the Welcome snippet is not asking for it back.
    /// </summary>
    private async Task SeedFirstRunAsync(CancellationToken cancellationToken)
    {
        using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText = "SELECT Value FROM Meta WHERE Key = 'seeds_inserted';";
            if (await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is "true")
            {
                return;
            }
        }

        // Migrate the legacy settings flag so previously deleted seeds stay deleted.
        if (!settings.Current.SeedsInserted)
        {
            foreach (var seed in FirstRunSeeds.All)
            {
                await SqliteSnippetRepository.UpsertInTransactionAsync(
                    connection, transaction, seed, TimeProvider.System, cancellationToken).ConfigureAwait(false);
            }
        }

        using var marker = connection.CreateCommand();
        marker.Transaction = transaction;
        marker.CommandText = """
            INSERT INTO Meta (Key, Value) VALUES ('seeds_inserted', 'true')
            ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value;
            """;
        await marker.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
