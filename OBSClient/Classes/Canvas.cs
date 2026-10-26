namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for a Canvas.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Canvas"/> class.
    /// Canvases let OBS Studio render multiple independent outputs; available since obs-websocket 5.7.0.
    /// </remarks>
    /// <param name="canvasName">The canvas name.</param>
    /// <param name="canvasUuid">The canvas uuid.</param>
    /// <param name="canvasFlags">The capability flags of the canvas.</param>
    /// <param name="canvasVideoSettings">The video settings of the canvas.</param>
    [method: JsonConstructor]
    public class Canvas(string canvasName, Guid canvasUuid, CanvasFlags canvasFlags, CanvasVideoSettings canvasVideoSettings)
    {
        /// <summary>
        /// Gets the canvas name.
        /// </summary>
        [JsonPropertyName("canvasName")]
        public string CanvasName { get; } = canvasName;

        /// <summary>
        /// Gets the canvas uuid.
        /// </summary>
        [JsonPropertyName("canvasUuid")]
        public Guid CanvasUuid { get; } = canvasUuid;

        /// <summary>
        /// Gets the capability flags of the canvas.
        /// </summary>
        [JsonPropertyName("canvasFlags")]
        public CanvasFlags CanvasFlags { get; } = canvasFlags;

        /// <summary>
        /// Gets the video settings of the canvas.
        /// </summary>
        [JsonPropertyName("canvasVideoSettings")]
        public CanvasVideoSettings CanvasVideoSettings { get; } = canvasVideoSettings;
    }
}
