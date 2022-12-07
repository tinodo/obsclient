namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class ProfileParameterResponseData : IResponseData
    {
        [JsonPropertyName("parameterValue")]
        public string? ParameterValue { get; set; }

        [JsonPropertyName("defaultParameterValue")]
        public string? DefaultParameterValue { get; set; }

        public ProfileParameterResponseData(string? parameterValue, string? defaultParameterValue)
        {
            this.ParameterValue = parameterValue;
            this.DefaultParameterValue = defaultParameterValue;
        }
    }
}
