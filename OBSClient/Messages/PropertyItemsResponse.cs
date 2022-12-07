namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class PropertyItemsResponse : IResponse
    {
        [JsonPropertyName("propertyItems")]
        public PropertyItem[] PropertyItems { get;set; }

        [JsonConstructor]
        public PropertyItemsResponse(PropertyItem[] propertyItems)
        {
            this.PropertyItems = propertyItems ?? Array.Empty<PropertyItem>();
        }
    }
}
