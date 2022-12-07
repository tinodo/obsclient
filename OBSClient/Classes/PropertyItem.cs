namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class PropertyItem
    {
        [JsonPropertyName("itemEnabled")]
        public bool ItemEnabled { get; }

        [JsonPropertyName("itemName")]
        public string ItemName { get; }

        [JsonPropertyName("itemValue")]
        public string ItemValue { get; }

        [JsonConstructor]
        public PropertyItem(bool itemEnabled, string itemName, string itemValue)
        {
            ItemEnabled = itemEnabled;
            ItemName = itemName;
            ItemValue = itemValue;
        }
    }
}
