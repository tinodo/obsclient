namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the VendorEvent event.
    /// </summary>
    /// <remarks>
    /// VendorEventEventArgs a new instance of the <see cref="TransitionDurationEventArgs"/> class.
    /// </remarks>
    /// <param name="vendorName">The vendor name.</param>
    /// <param name="eventType">The event type.</param>
    /// <param name="eventData">The event data.</param>
    [method: JsonConstructor]
    public class VendorEventEventArgs(string vendorName, string eventType, object? eventData) : EventArgs
    {
        /// <summary>
        /// Gets the name of the vendor.
        /// </summary>
        [JsonPropertyName("vendorName")]
        public string VendorName { get; } = vendorName;

        /// <summary>
        /// Gets the type of the event.
        /// </summary>
        [JsonPropertyName("eventType")]
        public string EventType { get; } = eventType;

        /// <summary>
        /// Gets the event data.
        /// </summary>
        [JsonPropertyName("eventData")]
        public object? EventData { get; } = eventData;
    }
}
