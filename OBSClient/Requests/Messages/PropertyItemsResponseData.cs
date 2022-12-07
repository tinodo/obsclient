namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class PropertyItemsResponseData : IResponseData
    {
        [JsonPropertyName("propertyItems")]
        public PropertyItem[] PropertyItems { get;set; }

        [JsonConstructor]
        public PropertyItemsResponseData(PropertyItem[] propertyItems)
        {
            this.PropertyItems = propertyItems ?? Array.Empty<PropertyItem>();
        }
    }
}
