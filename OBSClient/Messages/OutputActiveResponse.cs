namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputActiveResponse : IResponse
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; set; }

        [JsonConstructor]
        public OutputActiveResponse(bool outputActive)
        {
            this.OutputActive = outputActive;
        }
    }
}
