namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputsResponseData : IResponseData
    {
        [JsonPropertyName("inputs")]
        public Input[] Inputs { get; set; }

        public InputsResponseData(Input[] inputs)
        {
            this.Inputs = inputs ?? Array.Empty<Input>();
        }
    }
}
