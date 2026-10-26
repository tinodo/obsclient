namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CanvasRemoved event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CanvasRemovedEventArgs"/> class.
    /// </remarks>
    /// <param name="canvasName">The name of the removed canvas.</param>
    /// <param name="canvasUuid">The uuid of the removed canvas.</param>
    [method: JsonConstructor]
    public class CanvasRemovedEventArgs(string canvasName, Guid canvasUuid) : EventArgs
    {
        /// <summary>
        /// Gets the name of the removed canvas.
        /// </summary>
        [JsonPropertyName("canvasName")]
        public string CanvasName { get; } = canvasName;

        /// <summary>
        /// Gets the uuid of the removed canvas.
        /// </summary>
        [JsonPropertyName("canvasUuid")]
        public Guid CanvasUuid { get; } = canvasUuid;
    }
}
