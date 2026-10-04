namespace SnipShelf.Models;

/// <summary>Everything persisted to settings.json.</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public ShellPage LastPage { get; set; } = ShellPage.Vault;

    /// <summary>
    /// Set once the first-run seeds have been inserted. Guards against re-seeding a vault the
    /// user has since edited or emptied.
    /// </summary>
    public bool SeedsInserted { get; set; }

    public AppSettings Clone() => new()
    {
        Theme = Theme,
        LastPage = LastPage,
        SeedsInserted = SeedsInserted,
    };
}
