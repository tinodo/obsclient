namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class OBSClient
    {
        /// <summary>
        /// Gets an array of all scenes in OBS.
        /// </summary>
        /// <returns>A <see cref="SceneListResponseData"/></returns>
        public SceneListResponseData GetSceneList()
        {
            return this.SendRequest< SceneListResponseData>();
        }

        /// <summary>
        /// Gets an array of all groups in OBS.
        /// </summary>
        /// <returns>Array of group names</returns>
        /// <remarks>
        /// Groups in OBS are actually scenes, but renamed and modified. In obs-websocket, we treat them as scenes where we can.
        /// </remarks>
        public string[] GetGroupList()
        {
            return this.SendRequest<GroupsResponseData>().Groups;
        }

        /// <summary>
        /// Gets the current program scene.
        /// </summary>
        /// <returns>Current program scene</returns>
        public string GetCurrentProgramScene()
        {
            return this.SendRequest<CurrentProgramSceneNameResponseData>().CurrentProgramSceneName;
        }

        /// <summary>
        /// Sets the current program scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current program scene</param>
        public void SetCurrentProgramScene(string sceneName)
        {
            this.SendRequest(new { sceneName });
        }

        /// <summary>
        /// Gets the current preview scene.
        /// </summary>
        /// <returns>Current preview scene</returns>
        public string? GetCurrentPreviewScene()
        {
            return this.SendRequest<CurrentPreviewSceneNameResponseData>().CurrentPreviewSceneName;
        }

        /// <summary>
        /// Sets the current preview scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current preview scene</param>
        public void SetCurrentPreviewScene(string sceneName)
        {
            this.SendRequest(new { sceneName });
        }

        /// <summary>
        /// Creates a new scene in OBS.
        /// </summary>
        /// <param name="sceneName">Name for the new scene</param>
        public void CreateScene(string sceneName)
        {
            this.SendRequest(new { sceneName });
        }

        /// <summary>
        /// Removes a scene from OBS.
        /// </summary>
        /// <param name="sceneName">Name of the scene to remove</param>
        public void RemoveScene(string sceneName)
        {
            this.SendRequest(new { sceneName });
        }

        /// <summary>
        /// Sets the name of a scene (rename).
        /// </summary>
        /// <param name="sceneName">Name of the scene to be renamed</param>
        /// <param name="newSceneName">New name for the scene</param>
        public void SetSceneName(string sceneName, string newSceneName)
        {
            this.SendRequest(new { sceneName, newSceneName });
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <returns>A <see cref="SceneTransitionResponseData"/></returns>
        public SceneTransitionResponseData GetSceneSceneTransitionOverride(string sceneName)
        {
            return this.SendRequest<SceneTransitionResponseData>(new { sceneName });
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove (>= 50, <= 20000)</param>
        public void SetSceneSceneTransitionOverride(string sceneName, string? transitionName, int? transitionDuration)
        {
            this.SendRequest(new { sceneName, transitionName, transitionDuration });
        }
    }
}
