namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CanvasNameChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CanvasNameChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="canvasUuid">The uuid of the canvas.</param>
    /// <param name="oldCanvasName">The old name of the canvas.</param>
    /// <param name="canvasName">The new name of the canvas.</param>
    [method: JsonConstructor]
    public class CanvasNameChangedEventArgs(Guid canvasUuid, string oldCanvasName, string canvasName) : EventArgs
    {
        /// <summary>
        /// Gets the uuid of the canvas.
        /// </summary>
        [JsonPropertyName("canvasUuid")]
        public Guid CanvasUuid { get; } = canvasUuid;

        /// <summary>
        /// Gets the old name of the canvas.
        /// </summary>
        [JsonPropertyName("oldCanvasName")]
        public string OldCanvasName { get; } = oldCanvasName;

        /// <summary>
        /// Gets the new name of the canvas.
        /// </summary>
        [JsonPropertyName("canvasName")]
        public string CanvasName { get; } = canvasName;
    }
}
