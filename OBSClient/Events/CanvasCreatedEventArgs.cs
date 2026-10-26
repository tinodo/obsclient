namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CanvasCreated event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CanvasCreatedEventArgs"/> class.
    /// </remarks>
    /// <param name="canvasName">The name of the new canvas.</param>
    /// <param name="canvasUuid">The uuid of the new canvas.</param>
    [method: JsonConstructor]
    public class CanvasCreatedEventArgs(string canvasName, Guid canvasUuid) : EventArgs
    {
        /// <summary>
        /// Gets the name of the new canvas.
        /// </summary>
        [JsonPropertyName("canvasName")]
        public string CanvasName { get; } = canvasName;

        /// <summary>
        /// Gets the uuid of the new canvas.
        /// </summary>
        [JsonPropertyName("canvasUuid")]
        public Guid CanvasUuid { get; } = canvasUuid;
    }
}
