namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class CustomEventEventArgs : EventArgs
    {
        [JsonPropertyName("eventData")]
        public object? EventData { get; }

        [JsonConstructor]
        public CustomEventEventArgs(object? eventData)
        {
            this.EventData = eventData;
        }
    }
}
