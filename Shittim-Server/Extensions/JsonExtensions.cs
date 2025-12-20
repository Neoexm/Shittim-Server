using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shittim.Extensions
{
    /// <summary>
    /// JSON converter that handles long values from both numeric and string tokens.
    /// Useful for handling inconsistent JSON formats where numbers may be quoted.
    /// </summary>
    public class NullableLongConverter : JsonConverter<long>
    {
        /// <summary>
        /// Reads a long value from JSON, accepting both number and string tokens.
        /// Returns 0 as fallback for unparseable values.
        /// </summary>
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number &&
                reader.TryGetInt64(out var value))
            {
                return value;
            }
            if (reader.TokenType == JsonTokenType.String &&
                long.TryParse(reader.GetString(), out var l))
            {
                return l;
            }

            return 0;
        }

        /// <summary>
        /// Writes a long value to JSON as a numeric value.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }

    }
}
