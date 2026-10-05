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

    // Completes when the next search is genuinely in flight so tests can await that moment
    // deterministically; rotated after each signal so every search gets a fresh task.
    public TaskCompletionSource SearchStarted { get; private set; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // While non-null, SearchAsync parks in flight on this gate until it completes, so tests
    // can hold a search mid-flight deterministically; cancellation while parked throws
    // OperationCanceledException, matching a real in-flight cancellation.
    public TaskCompletionSource? SearchGate { get; set; }

    public sealed record SearchCall(
        string? Term,
        SortMode Sort,
        long? TagId,
        bool FavoritesOnly,
        CancellationToken CancellationToken);

    public async Task<IReadOnlyList<Snippet>> SearchAsync(
        string? term = null,
        SortMode sort = SortMode.Updated,
        long? tagId = null,
        bool favoritesOnly = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SearchCalls.Add(new SearchCall(term, sort, tagId, favoritesOnly, cancellationToken));

        // TrySetResult: the task may already be completed. Rotating hands the next search
        // a fresh task instead of re-signaling a completed one.
        SearchStarted.TrySetResult();
        SearchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (SearchGate is not null)
        {
            await SearchGate.Task.WaitAsync(cancellationToken);
        }

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
        cancellationToken.ThrowIfCancellationRequested();

        if (snippet.Id != 0 && !Snippets.Any(s => s.Id == snippet.Id))
        {
            // Matches the real repository, which rejects updates to missing snippets.
            throw new InvalidOperationException($"Snippet with Id {snippet.Id} not found or was deleted.");
        }

        // Max existing id + 1: Count + 1 collides whenever the seeded ids have gaps.
        var id = snippet.Id == 0
            ? Snippets.Select(s => s.Id).DefaultIfEmpty(0).Max() + 1
            : snippet.Id;
        var saved = snippet with { Id = id };
        Snippets.RemoveAll(s => s.Id == id);
        Snippets.Add(saved);
        return Task.FromResult(id);
    }

    public Task<Snippet?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var match = Snippets.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(match);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        // Mirror the real repository's contract: count descending, then name ascending.
        return Task.FromResult<IReadOnlyList<TagCount>>(
            TagCounts
                .OrderByDescending(t => t.SnippetCount)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }
}
