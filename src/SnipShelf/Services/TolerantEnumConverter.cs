using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnipShelf.Services;

/// <summary>
/// Reads an enum by name and falls back to the zero value instead of throwing.
/// settings.json is a plain file a user may hand-edit, so one bad value must not
/// cost them the rest of their settings.
/// </summary>
internal sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<TEnum>(reader.GetString(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        // Consume the value we are rejecting; for objects and arrays this skips the whole subtree.
        reader.Skip();
        return default;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
