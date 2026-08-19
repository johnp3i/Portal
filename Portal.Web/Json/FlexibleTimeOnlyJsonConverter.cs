using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portal.Web.Json;

/// <summary>
/// JSON converter for <see cref="TimeOnly"/> that tolerantly accepts time strings both with
/// and without seconds (e.g. "14:30" or "14:30:00"). HTML &lt;input type="time"&gt; controls emit
/// "HH:mm" by default, which the built-in System.Text.Json converter rejects — causing the entire
/// request body to fail deserialization. This converter accepts common formats and always writes
/// "HH:mm:ss".
/// </summary>
public class FlexibleTimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] AcceptedFormats =
    {
        "HH:mm",
        "HH:mm:ss",
        "HH:mm:ss.fff"
    };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("Unable to parse an empty value as TimeOnly.");

        if (TimeOnly.TryParseExact(value, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;

        // Fall back to culture-invariant general parsing for any other well-formed time string.
        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        throw new JsonException($"Unable to parse '{value}' as TimeOnly.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Nullable variant of <see cref="FlexibleTimeOnlyJsonConverter"/>. Treats null and empty strings as null.
/// </summary>
public class FlexibleNullableTimeOnlyJsonConverter : JsonConverter<TimeOnly?>
{
    private readonly FlexibleTimeOnlyJsonConverter _inner = new();

    public override TimeOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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

    public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            _inner.Write(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}
