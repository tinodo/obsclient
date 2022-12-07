namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputActiveResponseData : IResponseData
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; set; }

        [JsonConstructor]
        public OutputActiveResponseData(bool outputActive)
        {
            this.OutputActive = outputActive;
        }
    }
}
