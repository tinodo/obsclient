namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SlotValueResponse : IResponse
    {
        [JsonPropertyName("slotValue")]
        public object? SlotValue { get;set; }

        [JsonConstructor]
        public SlotValueResponse(object? slotValue)
        {
            this.SlotValue = slotValue;
        }
    }
}
