namespace SnipShelf.Models;

/// <summary>A stored, reusable text record — the only kind of item the vault holds.</summary>
/// <remarks>
/// A POCO, not an observable: the list hands out snapshots and the view models copy what
/// they need. Id is zero for a Snippet that has not been saved yet.
/// </remarks>
public sealed record Snippet
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public SnippetKind Kind { get; init; } = SnippetKind.Snippet;

    public bool Favorite { get; init; }

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset UpdatedUtc { get; init; }

    public IReadOnlyList<Tag> Tags { get; init; } = [];
}
