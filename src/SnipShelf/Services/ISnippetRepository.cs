using SnipShelf.Models;

namespace SnipShelf.Services;

/// <summary>The vault's data access surface. Every call is off the UI thread.</summary>
public interface ISnippetRepository
{
    /// <summary>
    /// Snippets matching <paramref name="term"/> in title, body, or tag name, narrowed by the
    /// optional tag and favorites filters. A blank term matches everything, not nothing —
    /// the Vault list asks for "all" by passing no term.
    /// </summary>
    Task<IReadOnlyList<Snippet>> SearchAsync(
        string? term = null,
        SortMode sort = SortMode.Updated,
        long? tagId = null,
        bool favoritesOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves <paramref name="snippet"/> and returns its id: an insert when the id is zero,
    /// an update otherwise. The tag set is replaced by the one on the record, so this is the
    /// single write path for a snippet's tags as well as its text.
    /// </summary>
    Task<long> UpsertAsync(Snippet snippet, CancellationToken cancellationToken = default);

    /// <summary>Reads the snippet with the given id, or null if it does not exist.</summary>
    Task<Snippet?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Deletes the snippet with the given id. A no-op if the id does not exist.</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// All tags with how many snippets carry each, ordered by count descending then name
    /// ascending. Tags with zero snippets are included.
    /// </summary>
    Task<IReadOnlyList<TagCount>> GetTagsWithCountsAsync(CancellationToken cancellationToken = default);
}
