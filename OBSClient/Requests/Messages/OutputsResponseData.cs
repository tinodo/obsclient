namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class OutputsResponseData : IResponseData
    {
        [JsonPropertyName("outputs")]
        public Output[] Outputs { get; set; }

        [JsonConstructor]
        public OutputsResponseData(Output[] outputs)
        {
            this.Outputs = outputs ?? Array.Empty<Output>();
        }
    }
}
