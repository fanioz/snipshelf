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

        await viewModel.WhenSearchSettlesAsync();

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
        await viewModel.WhenSearchSettlesAsync();

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
        await viewModel.WhenSearchSettlesAsync();

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
    public async Task Refresh_KeepsTheSelectionWhenTheSelectedSnippetIsStillListed()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha"), MakeSnippet(2, "Beta")],
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        var selectedRow = viewModel.Snippets[0];
        viewModel.SelectedSnippet = selectedRow;
        viewModel.SearchQuery = "alph"; // Alpha still matches; every row object is rebuilt.
        await viewModel.WhenSearchSettlesAsync();

        Assert.Equal(1, viewModel.SelectedSnippet?.Id);
        Assert.NotSame(selectedRow, viewModel.SelectedSnippet);
    }

    [Fact]
    public async Task Refresh_ClearsTheSelectionWhenTheSelectedSnippetDropsOutOfTheResults()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha"), MakeSnippet(2, "Beta")],
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.SelectedSnippet = viewModel.Snippets[0];
        viewModel.SearchQuery = "beta";
        await viewModel.WhenSearchSettlesAsync();

        Assert.Null(viewModel.SelectedSnippet);
        Assert.False(viewModel.HasSelection);
    }

    [Fact]
    public async Task Refresh_WithResultsOnScreen_DoesNotFlashLoadingWhileSearching()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha"), MakeSnippet(2, "Beta")],
            Delay = TimeSpan.FromMilliseconds(50),
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.SearchQuery = "alpha";

        // The previous list stays visible while the debounced search is pending.
        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.HasResults);

        await viewModel.WhenSearchSettlesAsync();

        Assert.Equal(VaultViewState.ShowingResults, viewModel.State);
        Assert.Equal("Alpha", Assert.Single(viewModel.Snippets).Title);
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

        // The first query's debounced search is still pending when the second is typed;
        // only the newer request may produce the final result.
        viewModel.SearchQuery = "alpha";
        viewModel.SearchQuery = "beta";
        await viewModel.WhenSearchSettlesAsync();

        Assert.Equal("beta", repository.SearchCalls[^1].Term);
        Assert.Equal("Beta", Assert.Single(viewModel.Snippets).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadFailure_ShowsErrorAndRetryCommandCanReload(bool failTags)
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha")],
            ExceptionToThrow = failTags ? null : new InvalidOperationException("database unavailable"),
            TagExceptionToThrow = failTags ? new InvalidOperationException("tags unavailable") : null,
        };
        var viewModel = new VaultViewModel(repository);

        await viewModel.InitializeAsync();

        Assert.Equal(VaultViewState.Error, viewModel.State);
        Assert.True(viewModel.IsError);

        var errorNotifications = new List<bool>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.IsError)) errorNotifications.Add(viewModel.IsError);
        };
        repository.ExceptionToThrow = null;
        repository.TagExceptionToThrow = null;
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasResults);
        Assert.False(viewModel.IsError);
        Assert.Contains(false, errorNotifications);
        Assert.Equal("Alpha", Assert.Single(viewModel.Snippets).Title);
    }

    [Fact]
    public async Task FavoritesOnly_ChangingTheFilterAloneRefreshesResults()
    {
        var repository = new FakeSnippetRepository
        {
            Snippets = [MakeSnippet(1, "Alpha", favorite: true), MakeSnippet(2, "Beta")],
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        viewModel.FavoritesOnly = true;
        await viewModel.WhenSearchSettlesAsync();
        Assert.True(repository.SearchCalls[^1].FavoritesOnly);
        Assert.Equal("Alpha", Assert.Single(viewModel.Snippets).Title);

        viewModel.FavoritesOnly = false;
        await viewModel.WhenSearchSettlesAsync();
        Assert.False(repository.SearchCalls[^1].FavoritesOnly);
        Assert.Equal(2, viewModel.Snippets.Count);
    }

    [Fact]
    public async Task DeferredSearchFailure_CanRetryThroughLoadCommand()
    {
        var repository = new FakeSnippetRepository { Snippets = [MakeSnippet(1, "Alpha")] };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        repository.ExceptionToThrow = new InvalidOperationException("database unavailable");
        viewModel.SearchQuery = "alpha";
        await viewModel.WhenSearchSettlesAsync();
        Assert.True(viewModel.IsError);

        repository.ExceptionToThrow = null;
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasResults);
        Assert.False(viewModel.IsError);
        Assert.Equal("alpha", repository.SearchCalls[^1].Term);
    }

    [Fact]
    public async Task CanceledInitialization_CanRetry()
    {
        var repository = new FakeSnippetRepository
        {
            ExceptionToThrow = new OperationCanceledException(),
            Snippets = [MakeSnippet(1, "Alpha")],
        };
        var viewModel = new VaultViewModel(repository);
        await viewModel.InitializeAsync();

        repository.ExceptionToThrow = null;
        await viewModel.InitializeAsync();
        Assert.True(viewModel.HasResults);
    }
}
