namespace SnipShelf.Models;

/// <summary>A Tag together with how many Snippets carry it, for the filter row.</summary>
public sealed record TagCount(long Id, string Name, int SnippetCount);
