namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class TransitionKindsResponseData : IResponseData
    {
        [JsonPropertyName("transitionKinds")]
        public string[] TransitionKinds { get; set; }

        public TransitionKindsResponseData(string[] transitionKinds)
        {
            this.TransitionKinds = transitionKinds ?? Array.Empty<string>();
        }
    }
}
