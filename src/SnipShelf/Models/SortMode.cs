namespace SnipShelf.Models;

/// <summary>Ordering for the Vault list.</summary>
/// <remarks><see cref="Updated"/> must stay the zero value: it is the default order.</remarks>
public enum SortMode
{
    Updated = 0,
    Title = 1,
    Created = 2,
}
