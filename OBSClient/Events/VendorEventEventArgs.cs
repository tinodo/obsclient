namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class VendorEventEventArgs : EventArgs
    {
        [JsonPropertyName("vendorName")]
        public string VendorName { get; }

        [JsonPropertyName("eventType")]
        public string EventType { get; }

        [JsonPropertyName("eventData")]
        public object? EventData { get; }

        [JsonConstructor]
        public VendorEventEventArgs(string vendorName, string eventType, object? eventData)
        {
            this.VendorName = vendorName;
            this.EventType = eventType;
            this.EventData = eventData;
        }
    }
}
