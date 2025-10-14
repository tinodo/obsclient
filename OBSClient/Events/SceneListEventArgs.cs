namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemTransformChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneListEventArgs"/> class.
    /// </remarks>
    /// <param name="scenes">The list of <see cref="Scene"/>.</param>
    [method: JsonConstructor]
    public class SceneListEventArgs(Scene[] scenes) : EventArgs
    {
        /// <summary>
        /// Gets the list of <see cref="Scene"/>.
        /// </summary>
        [JsonPropertyName("scenes")]
        public Scene[] Scenes { get; } = scenes ?? [];
    }
}
