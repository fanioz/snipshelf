using SnipShelf.Models;
using SnipShelf.Services;
using SnipShelf.Tests.Support;

namespace SnipShelf.Tests.Data;

public sealed class DatabaseBootstrapperTests
{
    private static readonly string[] ExpectedTables = ["Meta", "SnippetTags", "Snippets", "Tags"];

    [Fact]
    public async Task InitializeAsync_CreatesTheVaultFileInTheDataFolder()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: false);

        await vault.Bootstrapper.InitializeAsync();

        Assert.Equal(Path.Combine(vault.Folder, "snipvault.db"), vault.Database.DatabasePath);
        Assert.True(File.Exists(vault.Database.DatabasePath));
    }

    [Fact]
    public async Task InitializeAsync_AppliesTheDocumentedSchema()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: false);

        await vault.Bootstrapper.InitializeAsync();

        var tables = Query(vault.Database, "SELECT Name FROM sqlite_master WHERE Type = 'table' AND Name NOT LIKE 'sqlite_%' ORDER BY Name");
        Assert.Equal(ExpectedTables, tables);
        var indexes = Query(vault.Database, "SELECT Name FROM sqlite_master WHERE Type = 'index' AND Name NOT LIKE 'sqlite_%' ORDER BY Name");
        Assert.Equal(["IX_Snippets_Updated"], indexes);
        Assert.Equal(
            [DatabaseBootstrapper.SchemaVersion.ToString()],
            Query(vault.Database, "SELECT Value FROM Meta WHERE Key = 'schema_version'"));
    }

    [Fact]
    public async Task InitializeAsync_FirstRun_InsertsTheThreeSeedsAndRemembersIt()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: false);

        await vault.Bootstrapper.InitializeAsync();

        var seeds = await vault.Repository.SearchAsync();
        Assert.Equal(
            ["Explain this code", "Regex: semver", "Welcome — how to use SnipShelf"],
            seeds.Select(s => s.Title).OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal(SnippetKind.Prompt, seeds.Single(s => s.Title == "Explain this code").Kind);
        Assert.True(vault.Settings.Current.SeedsInserted);
    }

    // The seeds teach tag filtering, so they carry tags like any other record.
    [Fact]
    public async Task InitializeAsync_FirstRun_SeedsAreTagged()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: false);

        await vault.Bootstrapper.InitializeAsync();

        var seeds = await vault.Repository.SearchAsync();
        Assert.All(seeds, seed => Assert.NotEmpty(seed.Tags));
    }

    [Fact]
    public async Task InitializeAsync_SecondRun_DoesNotDuplicateOrReseed()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: false);
        await vault.Bootstrapper.InitializeAsync();
        var seeded = await vault.Repository.SearchAsync();

        await vault.Bootstrapper.InitializeAsync();

        Assert.Equal(seeded.Select(s => s.Id), (await vault.Repository.SearchAsync()).Select(s => s.Id));
    }

    // The seeds are ordinary records: deleting one must stick across restarts.
    [Fact]
    public async Task InitializeAsync_AfterASeedWasDeleted_DoesNotBringItBack()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: false);
        await vault.Bootstrapper.InitializeAsync();
        var welcome = (await vault.Repository.SearchAsync()).Single(s => s.Title.StartsWith("Welcome"));
        await vault.Repository.DeleteAsync(welcome.Id);

        await vault.Bootstrapper.InitializeAsync();

        Assert.DoesNotContain(await vault.Repository.SearchAsync(), s => s.Id == welcome.Id);
    }

    [Fact]
    public async Task InitializeAsync_WhenFirstRunAlreadyHappened_LeavesTheVaultEmpty()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: true);

        await vault.Bootstrapper.InitializeAsync();

        Assert.Empty(await vault.Repository.SearchAsync());
    }

    private static List<string> Query(VaultDatabase database, string sql)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
