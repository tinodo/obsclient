namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SlotValueResponseData : IResponseData
    {
        [JsonPropertyName("slotValue")]
        public object? SlotValue { get;set; }

        [JsonConstructor]
        public SlotValueResponseData(object? slotValue)
        {
            this.SlotValue = slotValue;
        }
    }
}
