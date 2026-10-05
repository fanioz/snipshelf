using SnipShelf.Models;
using SnipShelf.Tests.Support;

namespace SnipShelf.Tests.Data;

public sealed class SqliteSnippetRepositoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UpsertAsync_NewSnippet_StampsBothTimestampsFromTheClock()
    {
        using var vault = await TempVault.CreateAsync();

        var id = await vault.Repository.UpsertAsync(NewSnippet("First", "body"));

        var saved = await vault.Repository.GetAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(Start, saved!.CreatedUtc);
        Assert.Equal(Start, saved.UpdatedUtc);
    }

    [Fact]
    public async Task UpsertAsync_NewSnippet_TrimsTheTitleButLeavesTheBodyAlone()
    {
        using var vault = await TempVault.CreateAsync();

        var id = await vault.Repository.UpsertAsync(NewSnippet("  Padded  ", "  keep my indentation\n"));

        Assert.Equal("Padded", (await vault.Repository.GetAsync(id))!.Title);
        Assert.Equal("  keep my indentation\n", (await vault.Repository.GetAsync(id))!.Body);
    }

    [Fact]
    public async Task UpsertAsync_WithoutATitle_Throws()
    {
        using var vault = await TempVault.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => vault.Repository.UpsertAsync(NewSnippet("   ", "body")));
    }

    [Fact]
    public async Task GetAsync_ExistingSnippet_RoundTripsEveryField()
    {
        using var vault = await TempVault.CreateAsync();
        var original = NewSnippet("Title", "Body") with
        {
            Kind = SnippetKind.Prompt,
            Favorite = true,
            Tags = [new Tag(0, "powershell"), new Tag(0, "one-liner")],
        };
        var id = await vault.Repository.UpsertAsync(original);

        var saved = await vault.Repository.GetAsync(id);

        Assert.Equal(original.Title, saved!.Title);
        Assert.Equal(original.Body, saved.Body);
        Assert.Equal(SnippetKind.Prompt, saved.Kind);
        Assert.True(saved.Favorite);
        Assert.Equal(["one-liner", "powershell"], saved.Tags.Select(t => t.Name));
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        using var vault = await TempVault.CreateAsync();

        Assert.Null(await vault.Repository.GetAsync(4242));
    }

    [Fact]
    public async Task UpsertAsync_ExistingSnippet_RewritesFieldsAndBumpsOnlyUpdatedUtc()
    {
        using var vault = await TempVault.CreateAsync();
        var id = await vault.Repository.UpsertAsync(NewSnippet("Before", "before"));
        vault.Clock.Advance(TimeSpan.FromHours(3));

        await vault.Repository.UpsertAsync(NewSnippet("After", "after") with { Id = id, Kind = SnippetKind.Prompt });
        var saved = await vault.Repository.GetAsync(id);

        Assert.Equal("After", saved!.Title);
        Assert.Equal("after", saved.Body);
        Assert.Equal(SnippetKind.Prompt, saved.Kind);
        Assert.Equal(Start, saved.CreatedUtc);
        Assert.Equal(Start.AddHours(3), saved.UpdatedUtc);
    }

    [Fact]
    public async Task UpsertAsync_ExistingSnippet_ReplacesItsTagSet()
    {
        using var vault = await TempVault.CreateAsync();
        var id = await vault.Repository.UpsertAsync(NewSnippet("Tagged", "body") with
        {
            Tags = [new Tag(0, "keep"), new Tag(0, "drop")],
        });

        await vault.Repository.UpsertAsync(NewSnippet("Tagged", "body") with
        {
            Id = id,
            Tags = [new Tag(0, "keep"), new Tag(0, "added")],
        });
        var saved = await vault.Repository.GetAsync(id);

        Assert.Equal(["added", "keep"], saved!.Tags.Select(t => t.Name));
    }

    [Fact]
    public async Task UpsertAsync_TagNameDifferingOnlyByCase_ReusesTheExistingTag()
    {
        using var vault = await TempVault.CreateAsync();
        await vault.Repository.UpsertAsync(NewSnippet("One", "body") with { Tags = [new Tag(0, "PowerShell")] });

        var second = await vault.Repository.UpsertAsync(NewSnippet("Two", "body") with { Tags = [new Tag(0, "powershell")] });

        Assert.Equal("PowerShell", Assert.Single((await vault.Repository.GetAsync(second))!.Tags).Name);
    }

    // The database's NOCASE folds ASCII only, so non-ASCII pairs like café/CAFÉ are matched
    // in C# — one lookup miss there would otherwise become a second tag row.
    [Fact]
    public async Task UpsertAsync_TagNameDifferingOnlyByNonAsciiCase_ReusesTheExistingTag()
    {
        using var vault = await TempVault.CreateAsync();
        var first = await vault.Repository.UpsertAsync(NewSnippet("One", "body") with { Tags = [new Tag(0, "café")] });
        var second = await vault.Repository.UpsertAsync(NewSnippet("Two", "body") with { Tags = [new Tag(0, "CAFÉ")] });

        var tag = Assert.Single(await vault.Repository.GetTagsWithCountsAsync());
        Assert.Equal("café", tag.Name);
        Assert.Equal(2, tag.SnippetCount);
        Assert.Equal(
            (await vault.Repository.GetAsync(first))!.Tags.Single().Id,
            (await vault.Repository.GetAsync(second))!.Tags.Single().Id);
    }

    [Fact]
    public async Task UpsertAsync_WithATagThatIsOnlyWhitespace_IgnoresIt()
    {
        using var vault = await TempVault.CreateAsync();

        var id = await vault.Repository.UpsertAsync(NewSnippet("Untagged", "body") with { Tags = [new Tag(0, "   ")] });

        Assert.Empty((await vault.Repository.GetAsync(id))!.Tags);
    }

    [Fact]
    public async Task DeleteAsync_ExistingSnippet_RemovesItButKeepsTheTag()
    {
        using var vault = await TempVault.CreateAsync();
        var id = await vault.Repository.UpsertAsync(NewSnippet("Doomed", "body") with { Tags = [new Tag(0, "shared")] });

        await vault.Repository.DeleteAsync(id);

        Assert.Null(await vault.Repository.GetAsync(id));
        Assert.Contains(await vault.Repository.GetTagsWithCountsAsync(), tag => tag.Name == "shared");
    }

    // SnippetTags cascades, so nothing in the repository has to clean up the join rows.
    [Fact]
    public async Task DeleteAsync_ExistingSnippet_CascadesToItsTagLinks()
    {
        using var vault = await TempVault.CreateAsync();
        var doomed = await vault.Repository.UpsertAsync(NewSnippet("Doomed", "body") with { Tags = [new Tag(0, "gone")] });
        var keeper = await vault.Repository.UpsertAsync(NewSnippet("Keeper", "body") with { Tags = [new Tag(0, "gone")] });

        await vault.Repository.DeleteAsync(doomed);

        Assert.Equal("gone", Assert.Single((await vault.Repository.GetAsync(keeper))!.Tags).Name);
        Assert.Equal(1, Assert.Single(await vault.Repository.GetTagsWithCountsAsync()).SnippetCount);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_IsQuietlyIgnored()
    {
        using var vault = await TempVault.CreateAsync();

        await vault.Repository.DeleteAsync(4242);

        Assert.Empty(await vault.Repository.SearchAsync());
    }

    // The like() override in VaultDatabase gives SQL LIKE the app's Unicode case folding,
    // so café/CAFÉ count as the same letters — the ASCII-only LIKE built into SQLite would
    // fail this search.
    [Fact]
    public async Task SearchAsync_TermDifferingOnlyByUnicodeCase_FindsTheSnippetByTitle()
    {
        using var vault = await TempVault.CreateAsync();
        await vault.Repository.UpsertAsync(NewSnippet("café", "body"));

        Assert.Equal("café", Assert.Single(await vault.Repository.SearchAsync("CAFÉ")).Title);
    }

    [Fact]
    public async Task SearchAsync_TermMatchingATagNameByUnicodeCase_FindsTheTaggedSnippet()
    {
        using var vault = await TempVault.CreateAsync();
        await vault.Repository.UpsertAsync(NewSnippet("Tagged", "body") with { Tags = [new Tag(0, "café")] });

        Assert.Equal("Tagged", Assert.Single(await vault.Repository.SearchAsync("CAFÉ")).Title);
    }

    [Fact]
    public async Task SearchAsync_TermMatchingTheBody_FindsTheSnippet()
    {
        using var vault = await TempVault.CreateAsync();
        await vault.Repository.UpsertAsync(NewSnippet("Untouched", "the needle is buried in the body"));

        Assert.Equal("Untouched", Assert.Single(await vault.Repository.SearchAsync("needle")).Title);
    }

    // % is escaped before binding, so LIKE treats the user's wildcards as literal characters.
    [Fact]
    public async Task SearchAsync_TermWithPercent_MatchesItLiterally()
    {
        using var vault = await TempVault.CreateAsync();
        await vault.Repository.UpsertAsync(NewSnippet("Progress: 100% done", "body"));

        Assert.Equal("Progress: 100% done", Assert.Single(await vault.Repository.SearchAsync("100%")).Title);
    }

    private static Snippet NewSnippet(string title, string body) => new()
    {
        Title = title,
        Body = body,
        CreatedUtc = Start,
        UpdatedUtc = Start,
    };
}
