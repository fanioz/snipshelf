using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnipShelf.Services;

/// <summary>
/// Reads an enum by name and falls back to the zero value instead of throwing. Only names
/// match (case-insensitively) — a numeric string like "1" must not select a member by its
/// value, because settings.json is a plain file a user may hand-edit and one bad value must
/// not cost them the rest of their settings.
/// </summary>
internal sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            // Name comparison, not Enum.TryParse: TryParse also accepts numeric strings,
            // which would turn {"theme": "1"} into Light instead of a fallback.
            var value = reader.GetString();
            foreach (var name in Enum.GetNames<TEnum>())
            {
                if (string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse<TEnum>(name);
                }
            }
        }

        // Consume the value we are rejecting; for objects and arrays this skips the whole subtree.
        reader.Skip();
        return default;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
