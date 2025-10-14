namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputDeinterlaceFieldOrder request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputDeinterlaceFieldOrderResponse"/> class.
    /// </remarks>
    /// <param name="inputDeinterlaceFieldOrder">The deinterlace field order of the input.</param>
    public class InputDeinterlaceFieldOrderResponse(string inputDeinterlaceFieldOrder) : IResponse
    {
        /// <summary>
        /// Gets the deinterlace field order of the input.
        /// </summary>
        [JsonPropertyName("inputDeinterlaceFieldOrder")]
        public string InputDeinterlaceFieldOrder { get; } = inputDeinterlaceFieldOrder;
    }
}
