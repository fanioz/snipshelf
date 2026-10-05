using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SnipShelf.Models;
using SnipShelf.Services;
using System.Collections.ObjectModel;

namespace SnipShelf.ViewModels;

/// <summary>
/// Vault: read-only list + search + filters. Lazy-loads on first navigation; debounces
/// search at 200 ms; holds the selected snippet detail.
/// </summary>
public sealed partial class VaultViewModel : ViewModelBase
{
    private readonly ISnippetRepository _repository;
    private CancellationTokenSource? _searchCts;
    private bool _isInitialized;
    private bool? _vaultHasSnippets;
    private IReadOnlyList<Snippet> _allResults = [];
    private IReadOnlyList<TagCount> _allTags = [];

    public string Title => "Vault";

    public IReadOnlyList<SortOptionViewModel> SortOptions { get; } =
    [
        new(SortMode.Updated, "Updated"),
        new(SortMode.Title, "Title"),
        new(SortMode.Created, "Created"),
    ];

    public bool IsLoading => State == VaultViewState.Loading;
    public bool IsEmptyVault => State == VaultViewState.EmptyVault;
    public bool IsNoResults => State == VaultViewState.NoResults;
    public bool IsError => State == VaultViewState.Error;
    public bool HasResults => State == VaultViewState.ShowingResults;
    public bool HasSelection => SelectedSnippet is not null;
    public string NoResultsText => string.IsNullOrWhiteSpace(SearchQuery)
        ? "No snippets match these filters."
        : $"No snippets match ‘{SearchQuery.Trim()}’.";

    [ObservableProperty]
    private VaultViewState state = VaultViewState.Loading;

    [ObservableProperty]
    private ObservableCollection<SnippetListItemViewModel> snippets = [];

    [ObservableProperty]
    private SnippetListItemViewModel? selectedSnippet;

    [ObservableProperty]
    private ObservableCollection<FilterTagViewModel> visibleTags = [];

    [ObservableProperty]
    private bool hasMoreTags;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool favoritesOnly;

    [ObservableProperty]
    private long? selectedTagId;

    [ObservableProperty]
    private FilterTagViewModel? selectedTag;

    [ObservableProperty]
    private SortMode sortMode = SortMode.Updated;

    public VaultViewModel(ISnippetRepository repository)
    {
        _repository = repository;
    }

    [RelayCommand]
    private Task LoadAsync() => InitializeAsync();

    /// <summary>Initialize on first navigation (lazy).</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        try
        {
            State = VaultViewState.Loading;
            await RefreshTagsAsync(cancellationToken);
            await RefreshSnippetsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _isInitialized = false;
            // Navigation away; clean up gracefully.
        }
        catch
        {
            State = VaultViewState.Error;
        }
    }

    partial void OnStateChanged(VaultViewState value)
    {
        if (value == VaultViewState.Error)
        {
            _isInitialized = false;
        }

        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsEmptyVault));
        OnPropertyChanged(nameof(IsNoResults));
        OnPropertyChanged(nameof(HasResults));
    }

    partial void OnSelectedSnippetChanged(SnippetListItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(NoResultsText));
        DeferredSearch();
    }

    partial void OnSelectedTagChanged(FilterTagViewModel? value)
    {
        SelectedTagId = value?.Id;
    }

    partial void OnSelectedTagIdChanged(long? value)
    {
        if (value is null)
        {
            SelectedTag = null;
        }
        else if (SelectedTag?.Id != value)
        {
            SelectedTag = VisibleTags.FirstOrDefault(tag => tag.Id == value);
        }

        DeferredSearch();
    }

    partial void OnFavoritesOnlyChanged(bool value) => DeferredSearch();

    partial void OnSortModeChanged(SortMode value) => DeferredSearch();

    private void DeferredSearch()
    {
        if (!_isInitialized)
        {
            return;
        }

        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = DebounceAndSearchAsync(cts);
    }

    private async Task DebounceAndSearchAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
            await RefreshSnippetsAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // A newer query or filter superseded this request.
        }
        finally
        {
            if (ReferenceEquals(_searchCts, cts))
            {
                _searchCts = null;
            }

            cts.Dispose();
        }
    }

    private async Task RefreshTagsAsync(CancellationToken cancellationToken)
    {
        _allTags = await _repository.GetTagsWithCountsAsync(cancellationToken);
        UpdateVisibleTags();
    }

    private async Task RefreshSnippetsAsync(CancellationToken cancellationToken)
    {
        try
        {
            State = VaultViewState.Loading;

            var results = await _repository.SearchAsync(
                term: string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim(),
                sort: SortMode,
                tagId: SelectedTagId,
                favoritesOnly: FavoritesOnly,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _allResults = results;

            if (_vaultHasSnippets is null &&
                string.IsNullOrWhiteSpace(SearchQuery) && !FavoritesOnly && SelectedTagId is null)
            {
                _vaultHasSnippets = results.Count > 0;
            }

            if (_allResults.Count == 0)
            {
                State = _vaultHasSnippets == false
                    ? VaultViewState.EmptyVault
                    : VaultViewState.NoResults;
            }
            else
            {
                State = VaultViewState.ShowingResults;
            }

            UpdateSnippetRows();
            SelectedSnippet = null;
        }
        catch (OperationCanceledException)
        {
            throw; // Let initialization reset its guard, or the debounce discard a stale result.
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                State = VaultViewState.Error;
            }
        }
    }

    private void UpdateSnippetRows()
    {
        var rows = _allResults
            .Select(s => new SnippetListItemViewModel(s))
            .ToList();

        Snippets.Clear();
        foreach (var row in rows)
        {
            Snippets.Add(row);
        }
    }

    private void UpdateVisibleTags()
    {
        const int MaxInitialTags = 5;
        var tags = _allTags.Take(MaxInitialTags).ToList();
        HasMoreTags = _allTags.Count > MaxInitialTags;

        VisibleTags.Clear();
        foreach (var tag in tags)
        {
            VisibleTags.Add(new FilterTagViewModel(tag.Id, tag.Name, tag.SnippetCount));
        }

        if (SelectedTagId is not null)
        {
            SelectedTag = VisibleTags.FirstOrDefault(tag => tag.Id == SelectedTagId);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        FavoritesOnly = false;
        SelectedTag = null;
    }

    [RelayCommand]
    private void SelectFavorites()
    {
        FavoritesOnly = true;
        SelectedTag = null;
    }

    [RelayCommand]
    private void SelectTag(long tagId)
    {
        FavoritesOnly = false;
        SelectedTag = VisibleTags.FirstOrDefault(tag => tag.Id == tagId);
    }

    [RelayCommand]
    private void ExpandTags()
    {
        VisibleTags.Clear();
        foreach (var tag in _allTags)
        {
            VisibleTags.Add(new FilterTagViewModel(tag.Id, tag.Name, tag.SnippetCount));
        }
        HasMoreTags = false;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        FavoritesOnly = false;
        SelectedTag = null;
        SortMode = SortMode.Updated;
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}

/// <summary>Rendering state of the Vault list.</summary>
public enum VaultViewState
{
    Loading,
    EmptyVault,
    NoResults,
    ShowingResults,
    Error,
}

/// <summary>A snippet rendered in the list.</summary>
public sealed class SnippetListItemViewModel
{
    public SnippetListItemViewModel(Snippet snippet)
    {
        Id = snippet.Id;
        Title = snippet.Title;
        Body = snippet.Body;
        IsFavorite = snippet.Favorite;
        Kind = snippet.Kind;
        UpdatedUtc = snippet.UpdatedUtc;
        CreatedUtc = snippet.CreatedUtc;
        Tags = snippet.Tags;
    }

    public long Id { get; }
    public string Title { get; }
    public string Body { get; }
    public bool IsFavorite { get; }
    public SnippetKind Kind { get; }
    public string KindLabel => Kind == SnippetKind.Prompt ? "Prompt" : "Snippet";
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; }
    public IReadOnlyList<Tag> Tags { get; }

    public string Preview => string.IsNullOrWhiteSpace(Body)
        ? "No preview"
        : Body.Length > 120
            ? Body[..117] + "…"
            : Body;

    public string RelativeTime
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - UpdatedUtc;
            return elapsed.TotalSeconds < 60
                ? "now"
                : elapsed.TotalMinutes < 60
                    ? $"{(int)elapsed.TotalMinutes}m ago"
                    : elapsed.TotalHours < 24
                        ? $"{(int)elapsed.TotalHours}h ago"
                        : elapsed.TotalDays < 7
                            ? $"{(int)elapsed.TotalDays}d ago"
                            : UpdatedUtc.ToString("MMM d");
        }
    }

    public string RelativeUpdatedText => $"Updated {RelativeTime}";
}

public sealed class SortOptionViewModel
{
    public SortOptionViewModel(SortMode value, string label)
    {
        Value = value;
        Label = label;
    }

    public SortMode Value { get; }
    public string Label { get; }
}

/// <summary>A tag shown in the filter chip row.</summary>
public sealed class FilterTagViewModel
{
    public FilterTagViewModel(long id, string name, int count)
    {
        Id = id;
        Name = name;
        Count = count;
    }

    public long Id { get; }
    public string Name { get; }
    public int Count { get; }
}
