namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetProfileParameter request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProfileParameterResponse"/> class.
    /// </remarks>
    /// <param name="parameterValue">The parameter value.</param>
    /// <param name="defaultParameterValue">The default parameter value.</param>
    public class ProfileParameterResponse(string? parameterValue, string? defaultParameterValue) : IResponse
    {
        /// <summary>
        /// Gets the value for the parameter.
        /// </summary>
        [JsonPropertyName("parameterValue")]
        public string? ParameterValue { get; } = parameterValue;

        /// <summary>
        /// Gets the default value for the parameter.
        /// </summary>
        [JsonPropertyName("defaultParameterValue")]
        public string? DefaultParameterValue { get; } = defaultParameterValue;
    }
}
