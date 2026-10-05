using SnipShelf.Models;
using SnipShelf.Services;

namespace SnipShelf.Tests.Support;

public sealed class FakeSnippetRepository : ISnippetRepository
{
    public List<Snippet> Snippets { get; set; } = [];
    public List<TagCount> TagCounts { get; set; } = [];
    public List<SearchCall> SearchCalls { get; } = [];
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public Exception? TagExceptionToThrow { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public sealed record SearchCall(
        string? Term,
        SortMode Sort,
        long? TagId,
        bool FavoritesOnly);

    public async Task<IReadOnlyList<Snippet>> SearchAsync(
        string? term = null,
        SortMode sort = SortMode.Updated,
        long? tagId = null,
        bool favoritesOnly = false,
        CancellationToken cancellationToken = default)
    {
        SearchCalls.Add(new SearchCall(term, sort, tagId, favoritesOnly));

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<Snippet> query = Snippets;

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(s =>
                s.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.Body.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.Tags.Any(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (tagId is not null)
        {
            query = query.Where(s => s.Tags.Any(t => t.Id == tagId));
        }

        if (favoritesOnly)
        {
            query = query.Where(s => s.Favorite);
        }

        query = sort switch
        {
            SortMode.Title => query.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase),
            SortMode.Created => query.OrderByDescending(s => s.CreatedUtc),
            _ => query.OrderByDescending(s => s.UpdatedUtc),
        };

        return query.ToList();
    }

    public Task<long> UpsertAsync(Snippet snippet, CancellationToken cancellationToken = default)
    {
        var id = snippet.Id == 0 ? Snippets.Count + 1 : snippet.Id;
        var saved = snippet with { Id = id };
        Snippets.RemoveAll(s => s.Id == id);
        Snippets.Add(saved);
        return Task.FromResult(id);
    }

    public Task<Snippet?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var match = Snippets.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(match);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        Snippets.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TagCount>> GetTagsWithCountsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TagExceptionToThrow is not null)
        {
            throw TagExceptionToThrow;
        }

        return Task.FromResult<IReadOnlyList<TagCount>>(TagCounts.ToList());
    }
}
