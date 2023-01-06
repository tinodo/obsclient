namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the MediaInputActionTriggered event.
    /// </summary>
    public class MediaInputActionTriggeredEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the <see cref="ObsMediaInputAction"/> for the input.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaAction")]
        public ObsMediaInputAction MediaAction { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInputActionTriggeredEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="mediaAction">The <see cref="MediaAction"/>.</param>
        [JsonConstructor]
        public MediaInputActionTriggeredEventArgs(string inputName, ObsMediaInputAction mediaAction)
        {
            this.InputName = inputName;
            this.MediaAction = mediaAction;
        }
    }
}
