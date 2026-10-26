namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the capability flags of a <see cref="Canvas"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CanvasFlags"/> class.
    /// </remarks>
    /// <param name="main">A value indicating whether this is the main canvas.</param>
    /// <param name="activate">A value indicating whether the canvas is activated (rendering/outputting).</param>
    /// <param name="mixAudio">A value indicating whether the canvas mixes audio.</param>
    /// <param name="sceneRef">A value indicating whether scenes on this canvas are reference counted.</param>
    /// <param name="ephemeral">A value indicating whether the canvas is not saved to disk.</param>
    [method: JsonConstructor]
    public class CanvasFlags(bool main, bool activate, bool mixAudio, bool sceneRef, bool ephemeral)
    {
        /// <summary>
        /// Gets a value indicating whether this is the main canvas.
        /// </summary>
        [JsonPropertyName("MAIN")]
        public bool Main { get; } = main;

        /// <summary>
        /// Gets a value indicating whether the canvas is activated (rendering/outputting).
        /// </summary>
        [JsonPropertyName("ACTIVATE")]
        public bool Activate { get; } = activate;

        /// <summary>
        /// Gets a value indicating whether the canvas mixes audio.
        /// </summary>
        [JsonPropertyName("MIX_AUDIO")]
        public bool MixAudio { get; } = mixAudio;

        /// <summary>
        /// Gets a value indicating whether scenes on this canvas are reference counted.
        /// </summary>
        [JsonPropertyName("SCENE_REF")]
        public bool SceneRef { get; } = sceneRef;

        /// <summary>
        /// Gets a value indicating whether the canvas is not saved to disk.
        /// </summary>
        [JsonPropertyName("EPHEMERAL")]
        public bool Ephemeral { get; } = ephemeral;
    }
}
