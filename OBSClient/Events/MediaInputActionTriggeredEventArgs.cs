namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the MediaInputActionTriggered event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="MediaInputActionTriggeredEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputUuid">The input uuid.</param>
    /// <param name="mediaAction">The <see cref="MediaAction"/>.</param>
    [method: JsonConstructor]
    public class MediaInputActionTriggeredEventArgs(string inputName, Guid inputUuid, ObsMediaInputAction mediaAction) : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the UUID of the input.
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;

        /// <summary>
        /// Gets the <see cref="ObsMediaInputAction"/> for the input.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaAction")]
        public ObsMediaInputAction MediaAction { get; } = mediaAction;
    }
}
