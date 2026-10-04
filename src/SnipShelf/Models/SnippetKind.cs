namespace SnipShelf.Models;

/// <summary>The discriminator on a Snippet. Presentation and filtering only, never storage shape.</summary>
public enum SnippetKind
{
    Snippet = 0,
    Prompt = 1,
}
