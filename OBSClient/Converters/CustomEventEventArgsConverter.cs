namespace OBSStudioClient.Converters
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using OBSStudioClient.Events;

    /// <summary>
    /// Reads the data of a <c>CustomEvent</c> as a whole: that is the data that the other client broadcast.
    /// </summary>
    internal sealed class CustomEventEventArgsConverter : JsonConverter<CustomEventEventArgs>
    {
        /// <inheritdoc/>
        public override bool HandleNull => true;

        /// <inheritdoc/>
        public override CustomEventEventArgs Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return new CustomEventEventArgs(document.RootElement.Clone());
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, CustomEventEventArgs value, JsonSerializerOptions options)
        {
            if (value is { EventData: { } data })
            {
                data.WriteTo(writer);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
