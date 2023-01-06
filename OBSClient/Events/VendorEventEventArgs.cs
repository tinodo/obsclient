namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the VendorEvent event.
    /// </summary>
    public class VendorEventEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the vendor.
        /// </summary>
        [JsonPropertyName("vendorName")]
        public string VendorName { get; }

        /// <summary>
        /// Gets the type of the event.
        /// </summary>
        [JsonPropertyName("eventType")]
        public string EventType { get; }

        /// <summary>
        /// Gets the event data.
        /// </summary>
        [JsonPropertyName("eventData")]
        public object? EventData { get; }

        /// <summary>
        /// VendorEventEventArgs a new instance of the <see cref="TransitionDurationEventArgs"/> class.
        /// </summary>
        /// <param name="vendorName">The vendor name.</param>
        /// <param name="eventType">The event type.</param>
        /// <param name="eventData">The event data.</param>
        [JsonConstructor]
        public VendorEventEventArgs(string vendorName, string eventType, object? eventData)
        {
            this.VendorName = vendorName;
            this.EventType = eventType;
            this.EventData = eventData;
        }
    }
}
