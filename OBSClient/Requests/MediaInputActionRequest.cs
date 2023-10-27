namespace OBSStudioClient.Requests
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require an inputName and a mediaAction.
    /// </summary>
    public class MediaInputActionRequest
    {
        /// <summary>
        /// Gets or sets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="MediaAction"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaAction")]
        public ObsMediaInputAction MediaAction { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInputActionRequest"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="mediaAction">The <see cref="MediaAction"/>.</param>
        public MediaInputActionRequest(string inputName, ObsMediaInputAction mediaAction)
        {
            this.InputName = inputName;
            this.MediaAction = mediaAction;
        }
    }
}
