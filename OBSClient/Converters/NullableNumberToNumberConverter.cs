namespace OBSStudioClient.Converters
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Reads a nullable number as a number.
    /// </summary>
    public class NullableNumberToNumberConverter : JsonConverter<float>
    {
        /// <summary>
        /// Converts a nullable number to a number.
        /// </summary>
        /// <param name="reader">The json reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">JsonSerializer options.</param>
        /// <returns>The result, 0 if the json file had null value.</returns>
        /// <exception cref="JsonException">Thrown when the json token is neither null or a number.</exception>
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return 0f;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetSingle();
            }
            else
            {
                throw new JsonException();
            }
        }

        /// <summary>
        /// Writes a float as a number to the json stream.
        /// </summary>
        /// <param name="writer">The json writer.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">JsonSerializer options.</param>
        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}
