namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require an inputName and a media cursor offset.
    /// </summary>
    public class MediaInputCursorOffsetRequest
    {
        /// <summary>
        /// Gets or sets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        /// <summary>
        /// Gets or sets the media cursor offset.
        /// </summary>
        [JsonPropertyName("mediaCursorOffset")]
        public long MediaCursorOffset { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInputCursorOffsetRequest"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="mediaCursorOffset">The media cursor offset.</param>
        public MediaInputCursorOffsetRequest(string inputName, long mediaCursorOffset)
        {
            this.InputName = inputName;
            this.MediaCursorOffset = mediaCursorOffset;
        }
    }
}
