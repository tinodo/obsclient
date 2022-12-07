namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class TransitionKindsResponse : IResponse
    {
        [JsonPropertyName("transitionKinds")]
        public string[] TransitionKinds { get; set; }

        public TransitionKindsResponse(string[] transitionKinds)
        {
            this.TransitionKinds = transitionKinds ?? Array.Empty<string>();
        }
    }
}
