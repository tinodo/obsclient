namespace OBSStudioClient.Events
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CustomEvent event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CustomEventEventArgs"/> class.
    /// </remarks>
    /// <param name="eventData">The event data.</param>
    [method: JsonConstructor]
    public class CustomEventEventArgs(JsonElement? eventData) : EventArgs
    {
        /// <summary>
        /// Gets the event data.
        /// </summary>
        [JsonPropertyName("eventData")]
        public JsonElement? EventData { get; } = eventData;
    }
}
