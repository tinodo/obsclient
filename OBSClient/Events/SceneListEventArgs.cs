namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemTransformChanged event.
    /// </summary>
    public class SceneListEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the list of <see cref="Scene"/>.
        /// </summary>
        [JsonPropertyName("scenes")]
        public Scene[] Scenes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneListEventArgs"/> class.
        /// </summary>
        /// <param name="scenes">The list of <see cref="Scene"/>.</param>
        [JsonConstructor]
        public SceneListEventArgs(Scene[] scenes)
        {
            this.Scenes = scenes ?? Array.Empty<Scene>();
        }
    }
}
