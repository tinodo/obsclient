namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require an inputName and a media cursor.
    /// </summary>
    public class MediaInputCursorRequest
    {
        /// <summary>
        /// Gets or sets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        /// <summary>
        /// Gets or sets the media cursor.
        /// </summary>
        [JsonPropertyName("mediaCursor")]
        public long MediaCursor { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInputCursorRequest"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="mediaCursor">The media cursor.</param>
        public MediaInputCursorRequest(string inputName, long mediaCursor)
        {
            this.InputName = inputName;
            this.MediaCursor = mediaCursor;
        }
    }
}
