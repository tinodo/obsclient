namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for PropertyItem
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PropertyItem"/> class.
    /// </remarks>
    /// <param name="itemEnabled">A value indicating whether the items is enabled.</param>
    /// <param name="itemName">The item name.</param>
    /// <param name="itemValue">The item value.</param>
    /// <summary>
    /// Provides a class for PropertyItem
    /// </summary>
    [method: JsonConstructor]
    public class PropertyItem(bool itemEnabled, string itemName, string itemValue)
    {
        /// <summary>
        /// Gets a value indicating whether the items is enabled.
        /// </summary>
        [JsonPropertyName("itemEnabled")]
        public bool ItemEnabled { get; } = itemEnabled;

        /// <summary>
        /// Gets the item name.
        /// </summary>
        [JsonPropertyName("itemName")]
        public string ItemName { get; } = itemName;

        /// <summary>
        /// Gets the item value.
        /// </summary>
        [JsonPropertyName("itemValue")]
        public string ItemValue { get; } = itemValue;
    }
}
