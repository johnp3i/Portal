using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portal.Web.Json;

/// <summary>
/// JSON converter for <see cref="DateOnly"/> that tolerantly accepts common date string formats
/// (e.g. "2026-09-28", "28/09/2026"). HTML &lt;input type="date"&gt; controls emit "yyyy-MM-dd",
/// and an empty date input can arrive as an empty string — both of which can otherwise fail the
/// entire request-body deserialization. This converter accepts common formats and always writes
/// "yyyy-MM-dd".
/// </summary>
public class FlexibleDateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private static readonly string[] AcceptedFormats =
    {
        "yyyy-MM-dd",
        "dd/MM/yyyy",
        "MM/dd/yyyy",
        "dd-MM-yyyy",
        "d/M/yyyy",
        "dd.MM.yyyy"
    };

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("Unable to parse an empty value as DateOnly.");

        if (DateOnly.TryParseExact(value, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;

        // Fall back to culture-invariant general parsing for any other well-formed date string.
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        throw new JsonException($"Unable to parse '{value}' as DateOnly.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Nullable variant of <see cref="FlexibleDateOnlyJsonConverter"/>. Treats null and empty strings
/// as null — important because an empty HTML date input posts "".
/// </summary>
public class FlexibleNullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    private readonly FlexibleDateOnlyJsonConverter _inner = new();

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return null;
        }

        return _inner.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            _inner.Write(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}
