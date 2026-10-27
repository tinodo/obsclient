namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Event Messages 
    /// </summary>
    /// <remarks>
    /// Creates a new instance of an <see cref="EventMessage"/> object.
    /// </remarks>
    /// <param name="eventType">The type of event.</param>
    /// <param name="eventIntent">The subscribed events.</param>
    /// <param name="rawEventData">The json data for the event data.</param>
    [method: JsonConstructor]
    public partial class EventMessage(EventType eventType, EventSubscriptions eventIntent, JsonElement? rawEventData) : IMessage, IJsonOnDeserialized
    {
        // The type of the arguments of each event (_responseTypeMap) is generated from the protocol:
        // see Generated/EventMessage.g.cs.

        /// <summary>
        /// The type of Event
        /// </summary>
        [JsonPropertyName("eventType")]
        public EventType EventType { get; } = eventType;

        /// <summary>
        /// The Events you have subscribed to
        /// </summary>
        [JsonPropertyName("eventIntent")]
        public EventSubscriptions EventIntent { get; } = eventIntent;

        /// <summary>
        /// The raw JSON data sent to or received from OBS Studio
        /// </summary>
        [JsonPropertyName("eventData")]
        public JsonElement? RawEventData { get; } = rawEventData;

        /// <summary>
        /// The deserialized event
        /// </summary>
        [JsonIgnore]
        public EventArgs EventData { get; private set; } = EventArgs.Empty;

        /// <summary>
        /// Deserializes the raw JSON data to EventArgs.
        /// </summary>
        /// <exception cref="NotImplementedException">When the event isn't expected to hold any event data.</exception>
        /// <exception cref="ObsClientException">When deserialization failed.</exception>
        public void OnDeserialized()
        {
            if (!_responseTypeMap.TryGetValue(this.EventType, out Type? responseType))
            {
                throw new ObsClientException("OBS Studio event could not be mapped.");
            }

            // If no data is present, and the event type expects no data, set EventArgs.Empty
            if (!this.RawEventData.HasValue || responseType == typeof(EventArgs))
            {
                this.EventData = EventArgs.Empty;
                return;
            }

            if (JsonSerializer.Deserialize(this.RawEventData.Value.GetRawText(), responseType) is not EventArgs eventArgs)
            {
                throw new ObsClientException("OBS Studio event could not be read.");
            }

            this.EventData = eventArgs;
        }
    }
}
