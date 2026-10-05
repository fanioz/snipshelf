using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using SnipShelf.Models;

namespace SnipShelf.Services;

/// <summary>settings.json read/write. Never throws on malformed input.</summary>
public static class AppSettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new TolerantEnumConverter<AppTheme>(),
            new TolerantEnumConverter<ShellPage>(),
        },
    };

    public static string Serialize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings, Options);
    }

    /// <summary>Returns <c>false</c> when <paramref name="json"/> is not a settings object.</summary>
    public static bool TryDeserialize(string? json, [NotNullWhen(true)] out AppSettings? settings)
    {
        settings = Deserialize(json);
        return settings is not null;
    }

    /// <summary>Returns <c>null</c> when <paramref name="json"/> is not a settings object.</summary>
    private static AppSettings? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
