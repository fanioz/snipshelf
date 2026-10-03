namespace SnipShelf.Models;

/// <summary>Everything persisted to settings.json.</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public ShellPage LastPage { get; set; } = ShellPage.Vault;

    public AppSettings Clone() => new()
    {
        Theme = Theme,
        LastPage = LastPage,
    };
}
