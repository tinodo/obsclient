namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class SceneTransitionListResponse : IResponse
    {
        [JsonPropertyName("currentSceneTransitionName")]
        public string CurrentSceneTransitionName { get;set; }

        [JsonPropertyName("currentSceneTransitionKind")]
        public string CurrentSceneTransitionKind { get; set; }

        [JsonPropertyName("transitions")]
        public Transition[] Transitions { get; set; }

        public SceneTransitionListResponse(string currentSceneTransitionName, string currentSceneTransitionKind, Transition[] transitions)
        {
            this.CurrentSceneTransitionName = currentSceneTransitionName;
            this.CurrentSceneTransitionKind = currentSceneTransitionKind;
            this.Transitions = transitions;
        }
    }
}
