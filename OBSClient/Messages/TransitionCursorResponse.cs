namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class TransitionCursorResponse : IResponse
    {
        [JsonPropertyName("transitionCursor")]
        public float TransitionCursor { get; set; }

        [JsonConstructor]
        public TransitionCursorResponse(float transitionCursor)
        {
            this.TransitionCursor = transitionCursor;
        }
    }
}
