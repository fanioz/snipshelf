namespace SnipShelf.Models;

/// <summary>Theme preference as the user picks it in Settings.</summary>
/// <remarks>
/// <see cref="System"/> must stay the zero value: it is the fallback used when
/// settings.json holds an unrecognised theme name.
/// </remarks>
public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}
