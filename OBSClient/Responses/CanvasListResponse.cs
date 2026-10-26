namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetCanvasList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CanvasListResponse"/> class.
    /// </remarks>
    /// <param name="canvases">A list of <see cref="Canvas"/>.</param>
    [method: JsonConstructor]
    public class CanvasListResponse(Canvas[] canvases) : IResponse
    {
        /// <summary>
        /// Gets a list of <see cref="Canvas"/>.
        /// </summary>
        [JsonPropertyName("canvases")]
        public Canvas[] Canvases { get; } = canvases ?? [];
    }
}
