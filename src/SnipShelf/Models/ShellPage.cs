namespace SnipShelf.Models;

/// <summary>A top-level destination in the navigation pane.</summary>
/// <remarks>
/// <see cref="Vault"/> must stay the zero value: it is the fallback used when
/// settings.json holds an unrecognised page name.
/// </remarks>
public enum ShellPage
{
    Vault = 0,
    Favorites = 1,
    Settings = 2,
}
