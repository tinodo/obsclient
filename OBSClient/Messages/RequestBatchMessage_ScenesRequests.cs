namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Responses;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get an array of all scenes in OBS.
        /// </summary>
        /// <returns>A <see cref="SceneListResponse"/></returns>
        public void AddGetSceneListRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get an array of all groups in OBS.
        /// </summary>
        /// <returns>Array of group names</returns>
        /// <remarks>
        /// Groups in OBS are actually scenes, but renamed and modified. In obs-websocket, we treat them as scenes where we can.
        /// </remarks>
        public void AddGetGroupListRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get the current program scene.
        /// </summary>
        /// <returns>Current program scene</returns>
        public void AddGetCurrentProgramSceneRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to set the current program scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current program scene</param>
        public void AddSetCurrentProgramSceneRequest(string sceneName)
        {
            this._requests.Add(new(new { sceneName }));
        }

        /// <summary>
        /// Adds a request to get the current preview scene.
        /// </summary>
        /// <returns>Current preview scene</returns>
        public void AddGetCurrentPreviewSceneRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to set the current preview scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current preview scene</param>
        public void AddSetCurrentPreviewSceneRequest(string sceneName)
        {
            this._requests.Add(new(new { sceneName }));
        }

        /// <summary>
        ///Adds a request to create a new scene in OBS.
        /// </summary>
        /// <param name="sceneName">Name for the new scene</param>
        public void AddCreateSceneRequest(string sceneName)
        {
            this._requests.Add(new(new { sceneName }));
        }

        /// <summary>
        /// Adds a request to remove a scene from OBS.
        /// </summary>
        /// <param name="sceneName">Name of the scene to remove</param>
        public void AddRemoveSceneRequest(string sceneName)
        {
            this._requests.Add(new(new { sceneName }));
        }

        /// <summary>
        /// Adds a request to set the name of a scene (rename).
        /// </summary>
        /// <param name="sceneName">Name of the scene to be renamed</param>
        /// <param name="newSceneName">New name for the scene</param>
        public void AddSetSceneNameRequest(string sceneName, string newSceneName)
        {
            this._requests.Add(new(new { sceneName, newSceneName }));
        }

        /// <summary>
        /// Adds a request to get the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <returns>A <see cref="SceneTransitionResponse"/></returns>
        public void AddGetSceneSceneTransitionOverrideRequest(string sceneName)
        {
            this._requests.Add(new(new { sceneName }));
        }

        /// <summary>
        /// Adds a request to get the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove.</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove.</param>
        public void AddSetSceneSceneTransitionOverrideRequest(string sceneName, string? transitionName, int? transitionDuration)
        {
            this._requests.Add(new(new { sceneName, transitionName, transitionDuration }));
        }
    }
}
