namespace OBSStudioClient.Converters
{
    using OBSStudioClient.Enums;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Reads an OBS Studio event type, falling back to <see cref="EventType.Unknown"/> for event types this
    /// version of the client does not yet recognize, instead of throwing.
    /// </summary>
    /// <remarks>
    /// obs-websocket can introduce new event types in minor protocol updates (for example <c>CanvasCreated</c>
    /// in obs-websocket 5.7.0). Without this converter, receiving an event type unknown to this client would
    /// throw a <see cref="JsonException"/> from within the receive loop, which could tear down the connection.
    /// See <see href="https://github.com/tinodo/obsclient/issues/12"/>.
    /// </remarks>
    internal sealed class EventTypeConverter : JsonConverter<EventType>
    {
        private static readonly Dictionary<string, EventType> _eventTypes = Enum.GetValues<EventType>().ToDictionary(eventType => eventType.ToString(), StringComparer.Ordinal);

        /// <summary>
        /// Reads an event type name, returning <see cref="EventType.Unknown"/> when it is not recognized.
        /// </summary>
        /// <param name="reader">The json reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">JsonSerializer options.</param>
        /// <returns>The matching <see cref="EventType"/>, or <see cref="EventType.Unknown"/> when unrecognized.</returns>
        public override EventType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value && _eventTypes.TryGetValue(value, out var eventType))
            {
                return eventType;
            }

            reader.Skip();
            return EventType.Unknown;
        }

        /// <summary>
        /// Writes an event type as its string name to the json stream.
        /// </summary>
        /// <param name="writer">The json writer.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">JsonSerializer options.</param>
        public override void Write(Utf8JsonWriter writer, EventType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
