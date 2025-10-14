namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputsResponse"/> class.
    /// </remarks>
    /// <param name="inputs">List of <see cref="Input"/>.</param>
    public class InputsResponse(Input[] inputs) : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="Input"/>.
        /// </summary>
        [JsonPropertyName("inputs")]
        public Input[] Inputs { get; } = inputs ?? [];
    }
}
