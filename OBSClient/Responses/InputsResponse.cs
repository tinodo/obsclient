namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Messages;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputList request.
    /// </summary>
    public class InputsResponse : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="Input"/>.
        /// </summary>
        [JsonPropertyName("inputs")]
        public Input[] Inputs { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputsResponse"/> class.
        /// </summary>
        /// <param name="inputs">List of <see cref="Input"/>.</param>
        public InputsResponse(Input[] inputs)
        {
            this.Inputs = inputs ?? Array.Empty<Input>();
        }
    }
}
