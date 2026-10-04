using Microsoft.Data.Sqlite;

namespace SnipShelf.Services;

/// <summary>
/// The vault on disk: <c>snipvault.db</c> in the app data folder, plus how to reach it.
/// </summary>
/// <remarks>
/// Owning the connection factory here is the point. SQLite has foreign keys off by default,
/// so a connection opened anywhere else would quietly break the ON DELETE CASCADE between
/// Snippets and their tags.
/// </remarks>
public sealed class VaultDatabase
{
    public const string FileName = "snipvault.db";

    public VaultDatabase(string dataFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
        DataFolder = dataFolder;
        DatabasePath = Path.Combine(dataFolder, FileName);
    }

    public string DataFolder { get; }

    public string DatabasePath { get; }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            ForeignKeys = true,
        }.ToString());

        connection.Open();
        return connection;
    }
}
