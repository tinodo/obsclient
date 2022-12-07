namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class OutputsResponse : IResponse
    {
        [JsonPropertyName("outputs")]
        public Output[] Outputs { get; set; }

        [JsonConstructor]
        public OutputsResponse(Output[] outputs)
        {
            this.Outputs = outputs ?? Array.Empty<Output>();
        }
    }
}
