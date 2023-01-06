namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputPropertiesListPropertyItems request.
    /// </summary>
    public class PropertyItemsResponse : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="PropertyItem"/>.
        /// </summary>
        [JsonPropertyName("propertyItems")]
        public PropertyItem[] PropertyItems { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyItemsResponse"/> class.
        /// </summary>
        /// <param name="propertyItems">The list of <see cref="PropertyItem"/>.</param>
        [JsonConstructor]
        public PropertyItemsResponse(PropertyItem[] propertyItems)
        {
            this.PropertyItems = propertyItems ?? Array.Empty<PropertyItem>();
        }
    }
}
