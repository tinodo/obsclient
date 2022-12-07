namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;

    public class SceneListEventArgs : EventArgs
    {
        [JsonPropertyName("scenes")]
        public Scene[] Scenes { get; }

        [JsonConstructor]
        public SceneListEventArgs(Scene[] scenes)
        {
            this.Scenes = scenes ?? Array.Empty<Scene>();
        }
    }
}
