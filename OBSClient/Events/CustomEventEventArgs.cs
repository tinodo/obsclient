namespace OBSStudioClient.Events
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CustomEvent event.
    /// </summary>
    public class CustomEventEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the event data.
        /// </summary>
        [JsonPropertyName("eventData")]
        public JsonElement? EventData { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomEventEventArgs"/> class.
        /// </summary>
        /// <param name="eventData">The event data.</param>
        [JsonConstructor]
        public CustomEventEventArgs(JsonElement? eventData)
        {
            this.EventData = eventData;
        }
    }
}
