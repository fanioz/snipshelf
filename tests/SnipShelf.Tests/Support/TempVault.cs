using Microsoft.Data.Sqlite;
using SnipShelf.Models;
using SnipShelf.Services;

namespace SnipShelf.Tests.Support;

/// <summary>A real vault database in a throwaway folder.</summary>
/// <remarks>
/// <see cref="CreateAsync"/> hands back a bootstrapped vault with no seeds in it, so a
/// repository test starts from an empty vault. The bootstrapper tests use <see cref="New"/>
/// instead, because when seeding happens is exactly what they are asserting.
/// </remarks>
public sealed class TempVault : IDisposable
{
    private TempVault(string folder, bool seedsAlreadyInserted)
    {
        Folder = folder;
        Clock = new FixedClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        Settings = new FakeSettingsService(new AppSettings { SeedsInserted = seedsAlreadyInserted });
        Database = new VaultDatabase(folder);
        Repository = new SqliteSnippetRepository(Database, Clock);
        Bootstrapper = new DatabaseBootstrapper(Database, Settings, Repository);
    }

    public string Folder { get; }

    public FixedClock Clock { get; }

    public FakeSettingsService Settings { get; }

    public VaultDatabase Database { get; }

    public ISnippetRepository Repository { get; }

    public DatabaseBootstrapper Bootstrapper { get; }

    /// <summary>An uninitialised vault: the schema is only applied when the caller says so.</summary>
    public static TempVault New(bool seedsAlreadyInserted)
    {
        var folder = Path.Combine(Path.GetTempPath(), "snipshelf-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(folder);
        return new TempVault(folder, seedsAlreadyInserted);
    }

    /// <summary>An empty, ready-to-use vault: first-run seeding is marked as already done.</summary>
    public static async Task<TempVault> CreateAsync()
    {
        var vault = New(seedsAlreadyInserted: true);
        await vault.Bootstrapper.InitializeAsync();
        return vault;
    }

    public void Dispose()
    {
        // Pooled connections keep the database file open; drop them before the folder goes.
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }
    }
}
