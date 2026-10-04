using SnipShelf.Models;
using SnipShelf.Tests.Support;
using SnipShelf.ViewModels;

namespace SnipShelf.Tests.ViewModels;

public sealed class VaultViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static Snippet MakeSnippet(
        long id,
        string title,
        bool favorite = false,
        params Tag[] tags) => new()
    {
        Id = id,
        Title = title,
        Body = $"Body of {title}",
        Favorite = favorite,
        Kind = id % 2 == 0 ? SnippetKind.Prompt : SnippetKind.Snippet,
        CreatedUtc = Now.AddDays(-2),
        UpdatedUtc = Now.AddHours(-1),
        Tags = tags,
    };

    [Fact]
    public async Task InitializeAsync_LoadsTagsAndSnippets_AndShowsResults()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha")],
            TagCounts = [new TagCount(4, "work", 1)],
        };
        var viewModel = new VaultViewModel(repository);

        await viewModel.InitializeAsync();

        Assert.Equal(VaultViewState.ShowingResults, viewModel.State);
        Assert.Single(viewModel.Snippets);
        Assert.Equal("Alpha", viewModel.Snippets[0].Title);
        Assert.Single(viewModel.VisibleTags);
        Assert.Equal("work", viewModel.VisibleTags[0].Name);
        Assert.Null(repository.SearchCalls[0].Term);
    }

    [Fact]
    public async Task InitializeAsync_WhenRepositoryIsEmpty_ShowsEmptyVault()
    {
        var viewModel = new VaultViewModel(new FakeSnippetRepository());

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsEmptyVault);
        Assert.False(viewModel.IsNoResults);
    }

    [Fact]
    public async Task SearchQuery_DebouncesAndTrimsTerm()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha")],
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.SearchQuery = "a";
        viewModel.SearchQuery = "al";
        viewModel.SearchQuery = " alpha ";

        await Task.Delay(350);

        Assert.Equal(2, repository.SearchCalls.Count);
        Assert.Equal("alpha", repository.SearchCalls[^1].Term);
    }

    [Fact]
    public async Task SearchFilterSort_UsesRepositoryArguments()
    {
        var tagged = new Tag(9, "work");
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha", favorite: true, tagged)],
            TagCounts = [new TagCount(9, "work", 1)],
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.SearchQuery = "alpha";
        viewModel.SortMode = SortMode.Title;
        viewModel.SelectedTagId = 9;
        viewModel.FavoritesOnly = true;
        await Task.Delay(350);

        var call = repository.SearchCalls[^1];
        Assert.Equal("alpha", call.Term);
        Assert.Equal(SortMode.Title, call.Sort);
        Assert.Equal(9, call.TagId);
        Assert.True(call.FavoritesOnly);
    }

    [Fact]
    public async Task FilteredZeroMatches_ShowsNoResultsInsteadOfEmptyVault()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha")],
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.SearchQuery = "missing";
        await Task.Delay(350);

        Assert.True(viewModel.IsNoResults);
        Assert.False(viewModel.IsEmptyVault);
        Assert.Contains("missing", viewModel.NoResultsText);
    }

    [Fact]
    public async Task TagDisplay_LimitsInitialTagsAndCanExpand()
    {
        var tags = Enumerable.Range(1, 7)
            .Select(i => new TagCount(i, $"tag{i}", 8 - i))
            .ToList();
        var repository = new FakeSnippetRepository { TagCounts = tags };
        var viewModel = new VaultViewModel(repository);

        await viewModel.InitializeAsync();

        Assert.Equal(5, viewModel.VisibleTags.Count);
        Assert.True(viewModel.HasMoreTags);

        viewModel.ExpandTagsCommand.Execute(null);

        Assert.Equal(7, viewModel.VisibleTags.Count);
        Assert.False(viewModel.HasMoreTags);
    }

    [Fact]
    public async Task SelectingASnippet_ExposesItsReadOnlyDetailFields()
    {
        var snippet = MakeSnippet(2, "Prompt", favorite: true, new Tag(3, "ai"));
        var repository = new FakeSnippetRepository { Snippets = [snippet] };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.SelectedSnippet = viewModel.Snippets[0];

        Assert.True(viewModel.HasSelection);
        Assert.Equal("Prompt", viewModel.SelectedSnippet?.KindLabel);
        Assert.True(viewModel.SelectedSnippet?.IsFavorite);
        Assert.Equal(snippet.Body, viewModel.SelectedSnippet?.Body);
        Assert.Equal("ai", viewModel.SelectedSnippet?.Tags[0].Name);
    }

    [Fact]
    public async Task NewerSearch_SupersedesAnInFlightQuery()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha"), MakeSnippet(2, "Beta")],
            Delay = TimeSpan.FromMilliseconds(300),
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.SearchQuery = "alpha";
        await Task.Delay(250);
        viewModel.SearchQuery = "beta";
        await Task.Delay(600);

        Assert.Equal("beta", repository.SearchCalls[^1].Term);
        Assert.Equal("Beta", Assert.Single(viewModel.Snippets).Title);
    }

    [Fact]
    public async Task SearchFailure_ShowsErrorAndRetryCommandCanReload()
    {
        var repository = new FakeSnippetRepository
        {
            ExceptionToThrow = new InvalidOperationException("database unavailable"),
        };
        var viewModel = new VaultViewModel(repository);

        await viewModel.InitializeAsync();

        Assert.Equal(VaultViewState.Error, viewModel.State);
    }
}
