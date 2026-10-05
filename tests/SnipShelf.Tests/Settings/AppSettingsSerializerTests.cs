using SnipShelf.Models;
using SnipShelf.Services;

namespace SnipShelf.Tests.Settings;

public class AppSettingsSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var original = new AppSettings { Theme = AppTheme.Dark, LastPage = ShellPage.Favorites, SeedsInserted = true };

        var json = AppSettingsSerializer.Serialize(original);
        Assert.True(AppSettingsSerializer.TryDeserialize(json, out var restored));

        Assert.Equal(AppTheme.Dark, restored.Theme);
        Assert.Equal(ShellPage.Favorites, restored.LastPage);
        Assert.True(restored.SeedsInserted);
    }

    [Fact]
    public void Serialize_WritesEnumsAsNames_SoTheFileStaysHandEditable()
    {
        var json = AppSettingsSerializer.Serialize(
            new AppSettings { Theme = AppTheme.Dark, LastPage = ShellPage.Settings });

        Assert.Contains("\"Dark\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Settings\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"a string\"")]
    public void TryDeserialize_RejectsUnusableInput(string? json)
    {
        Assert.False(AppSettingsSerializer.TryDeserialize(json, out var settings));
        Assert.Null(settings);
    }

    [Fact]
    public void TryDeserialize_MissingProperties_UsesDefaults()
    {
        Assert.True(AppSettingsSerializer.TryDeserialize("{}", out var settings));

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(ShellPage.Vault, settings.LastPage);
    }

    // A hand-edited typo in one key must not discard the other keys.
    [Fact]
    public void TryDeserialize_UnknownEnumValue_FallsBackPerProperty()
    {
        const string json = """{"theme":"Neon","lastPage":"Settings"}""";

        Assert.True(AppSettingsSerializer.TryDeserialize(json, out var settings));

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(ShellPage.Settings, settings.LastPage);
    }

    [Fact]
    public void TryDeserialize_WrongJsonTypeForEnum_FallsBackPerProperty()
    {
        const string json = """{"theme":7,"lastPage":{"nested":true}}""";

        Assert.True(AppSettingsSerializer.TryDeserialize(json, out var settings));

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(ShellPage.Vault, settings.LastPage);
    }

    [Fact]
    public void TryDeserialize_IsCaseInsensitive()
    {
        const string json = """{"THEME":"dark","LastPage":"favorites"}""";

        Assert.True(AppSettingsSerializer.TryDeserialize(json, out var settings));

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(ShellPage.Favorites, settings.LastPage);
    }

    [Fact]
    public void TryDeserialize_IgnoresUnknownProperties_SoOlderBuildsCanReadNewerFiles()
    {
        const string json = """{"theme":"Light","somethingFromTheFuture":{"a":1}}""";

        Assert.True(AppSettingsSerializer.TryDeserialize(json, out var settings));

        Assert.Equal(AppTheme.Light, settings.Theme);
    }
}
