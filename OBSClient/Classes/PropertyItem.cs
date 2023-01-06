namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for PropertyItem
    /// </summary>
    public class PropertyItem
    {
        /// <summary>
        /// Gets a value indicating whether the items is enabled.
        /// </summary>
        [JsonPropertyName("itemEnabled")]
        public bool ItemEnabled { get; }

        /// <summary>
        /// Gets the item name.
        /// </summary>
        [JsonPropertyName("itemName")]
        public string ItemName { get; }

        /// <summary>
        /// Gets the item value.
        /// </summary>
        [JsonPropertyName("itemValue")]
        public string ItemValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyItem"/> class.
        /// </summary>
        /// <param name="itemEnabled">A value indicating whether the items is enabled.</param>
        /// <param name="itemName">The item name.</param>
        /// <param name="itemValue">The item value.</param>
        [JsonConstructor]
        public PropertyItem(bool itemEnabled, string itemName, string itemValue)
        {
            this.ItemEnabled = itemEnabled;
            this.ItemName = itemName;
            this.ItemValue = itemValue;
        }
    }
}
