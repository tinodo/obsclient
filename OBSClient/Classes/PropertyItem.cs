namespace OBSStudioClient.Classes
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for PropertyItem
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PropertyItem"/> class.
    /// </remarks>
    /// <param name="itemEnabled">A value indicating whether the items is enabled.</param>
    /// <param name="itemName">The item name.</param>
    /// <param name="itemValue">The item value, as OBS Studio sent it.</param>
    /// <summary>
    /// Provides a class for PropertyItem
    /// </summary>
    [method: JsonConstructor]
    public class PropertyItem(bool itemEnabled, string itemName, JsonElement? itemValue)
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
        /// Gets the item value, as OBS Studio sent it.
        /// </summary>
        /// <remarks>
        /// The kind of the value depends on the format of the list: a string, an integer or a floating point number
        /// (<see cref="JsonElement.GetString"/>, <see cref="JsonElement.GetInt64"/> and <see cref="JsonElement.GetDouble"/>), or
        /// <see langword="null"/> for a list of another format. A setting that is set from it has to be set as the same kind.
        /// </remarks>
        [JsonPropertyName("itemValue")]
        public JsonElement? ItemValue { get; } = itemValue;
    }
}
