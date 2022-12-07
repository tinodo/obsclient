namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class TransitionCursorResponseData : IResponseData
    {
        [JsonPropertyName("transitionCursor")]
        public float TransitionCursor { get; set; }

        [JsonConstructor]
        public TransitionCursorResponseData(float transitionCursor)
        {
            this.TransitionCursor = transitionCursor;
        }
    }
}
