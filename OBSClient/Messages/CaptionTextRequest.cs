namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The <see cref="CaptionTextRequest"/> object holds the Request Data in the Request Message (<see cref="IMessage"/>) for a SendStreamCaption request.
    /// </summary>
    public class CaptionTextRequest
    {
        /// <summary>
        /// The caption text
        /// </summary>
        [JsonPropertyName("captionText")]
        public string CaptionText { get; set; }

        /// <summary>
        /// Creates a new instance of a <see cref="CaptionTextRequest"/> class.
        /// </summary>
        /// <param name="captionText">The caption text</param>
        [JsonConstructor]
        public CaptionTextRequest(string captionText)
        {
            this.CaptionText = captionText;
        }
    }
}
