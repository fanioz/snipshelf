namespace SnipShelf.Models;

/// <summary>A case-insensitive label, unique by name, many-to-many with Snippets.</summary>
public sealed record Tag(long Id, string Name);
