using Microsoft.Data.Sqlite;
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
        // The Meta table holds exactly the seed marker: the DDL is create-only, so there is
        // no schema version row for a migration mechanism that does not exist yet.
        Assert.Equal(["true"], Query(vault.Database, "SELECT Value FROM Meta"));
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
        Assert.Equal(["true"], Query(vault.Database, "SELECT Value FROM Meta WHERE Key = 'seeds_inserted'"));
        Assert.Empty(vault.Settings.Updates);
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

        Assert.DoesNotContain(await vault.Repository.SearchAsync(), s => s.Title.StartsWith("Welcome"));
    }

    [Fact]
    public async Task InitializeAsync_WhenFirstRunAlreadyHappened_LeavesTheVaultEmpty()
    {
        using var vault = TempVault.New(seedsAlreadyInserted: true);

        await vault.Bootstrapper.InitializeAsync();

        Assert.Empty(await vault.Repository.SearchAsync());
        Assert.Equal(["true"], Query(vault.Database, "SELECT Value FROM Meta WHERE Key = 'seeds_inserted'"));

        var restarted = new DatabaseBootstrapper(vault.Database, new FakeSettingsService());
        await restarted.InitializeAsync();
        Assert.Empty(await vault.Repository.SearchAsync());
    }

    [Theory]
    [InlineData("Snippets", "NEW.Title = 'Explain this code'")]
    [InlineData("Meta", "NEW.Key = 'seeds_inserted'")]
    public async Task InitializeAsync_WhenSeedingFails_RollsBackAndCanRetry(string table, string condition)
    {
        using var vault = await TempVault.CreateAsync();
        await vault.Settings.UpdateAsync(current => current.SeedsInserted = false);
        using (var connection = vault.Database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                DELETE FROM Meta WHERE Key = 'seeds_inserted';
                CREATE TRIGGER FailSeeding BEFORE INSERT ON {table}
                WHEN {condition}
                BEGIN SELECT RAISE(ABORT, 'simulated write failure'); END;
                """;
            command.ExecuteNonQuery();
        }

        await Assert.ThrowsAsync<SqliteException>(() => vault.Bootstrapper.InitializeAsync());

        Assert.Empty(await vault.Repository.SearchAsync());
        Assert.Empty(await vault.Repository.GetTagsWithCountsAsync());
        Assert.Empty(Query(vault.Database, "SELECT Value FROM Meta WHERE Key = 'seeds_inserted'"));
        Assert.Empty(Query(vault.Database, "SELECT CAST(SnippetId AS TEXT) FROM SnippetTags"));

        using (var connection = vault.Database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TRIGGER FailSeeding;";
            command.ExecuteNonQuery();
        }

        await vault.Bootstrapper.InitializeAsync();
        Assert.Equal(FirstRunSeeds.All.Count, (await vault.Repository.SearchAsync()).Count);
        Assert.Equal(["true"], Query(vault.Database, "SELECT Value FROM Meta WHERE Key = 'seeds_inserted'"));
    }

    // The seed is meant to teach the regex, so the pattern itself must be strict semver:
    // \d would also match Unicode digits and $ would tolerate a trailing newline.
    [Fact]
    public void FirstRunSeeds_SemverSnippet_PatternIsStrictSemver()
    {
        var body = FirstRunSeeds.All.Single(s => s.Title == "Regex: semver").Body;
        var pattern = body.Split('\n')[0].TrimEnd('\r');

        Assert.Matches(pattern, "1.2.3");
        Assert.Matches(pattern, "1.2.3-beta.1+build.2");
        Assert.DoesNotMatch(pattern, "01.2.3");
        Assert.DoesNotMatch(pattern, "1.2.3\n4.5.6");
        Assert.DoesNotMatch(pattern, "١.٢.٣");
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
