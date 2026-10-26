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
            this.AddGetSceneListRequest(null);
        }

        /// <summary>
        /// Adds a request to get an array of all scenes on a canvas in OBS.
        /// </summary>
        /// <param name="canvasUuid">UUID of the canvas the scenes are in</param>
        /// <returns>A <see cref="SceneListResponse"/></returns>
        public void AddGetSceneListRequest(Guid canvasUuid)
        {
            this.AddGetSceneListRequest((Guid?)canvasUuid);
        }

        private void AddGetSceneListRequest(Guid? canvasUuid)
        {
            this._requests.Add(new(new { canvasUuid }));
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
            this.AddSetCurrentProgramSceneRequest(sceneName, null);
        }

        /// <summary>
        /// Adds a request to set the current program scene.
        /// </summary>
        /// <param name="sceneUuid">Scene to set as the current program scene</param>
        public void AddSetCurrentProgramSceneRequest(Guid sceneUuid)
        {
            this.AddSetCurrentProgramSceneRequest(null, sceneUuid);
        }

        private void AddSetCurrentProgramSceneRequest(string? sceneName, Guid? sceneUuid)
        {
            this._requests.Add(new(new { sceneName, sceneUuid }));
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
            this.AddSetCurrentPreviewSceneRequest(sceneName, null);
        }

        /// <summary>
        /// Adds a request to set the current preview scene.
        /// </summary>
        /// <param name="sceneUuid">Scene to set as the current preview scene</param>
        public void AddSetCurrentPreviewSceneRequest(Guid sceneUuid)
        {
            this.AddSetCurrentPreviewSceneRequest(null, sceneUuid);
        }

        private void AddSetCurrentPreviewSceneRequest(string? sceneName, Guid? sceneUuid)
        {
            this._requests.Add(new(new { sceneName, sceneUuid }));
        }

        /// <summary>
        ///Adds a request to create a new scene in OBS.
        /// </summary>
        /// <param name="sceneName">Name for the new scene</param>
        public void AddCreateSceneRequest(string sceneName)
        {
            this.AddCreateSceneRequest(sceneName, null);
        }

        /// <summary>
        /// Adds a request to create a new scene on a canvas in OBS.
        /// </summary>
        /// <param name="sceneName">Name for the new scene</param>
        /// <param name="canvasUuid">UUID of the canvas to create the new scene in</param>
        public void AddCreateSceneRequest(string sceneName, Guid canvasUuid)
        {
            this.AddCreateSceneRequest(sceneName, (Guid?)canvasUuid);
        }

        private void AddCreateSceneRequest(string sceneName, Guid? canvasUuid)
        {
            this._requests.Add(new(new { sceneName, canvasUuid }));
        }

        /// <summary>
        /// Adds a request to remove a scene from OBS.
        /// </summary>
        /// <param name="sceneName">Name of the scene to remove</param>
        public void AddRemoveSceneRequest(string sceneName)
        {
            this.AddRemoveSceneRequest(sceneName, null);
        }

        /// <summary>
        /// Adds a request to remove a scene from a canvas in OBS.
        /// </summary>
        /// <param name="sceneName">Name of the scene to remove</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        public void AddRemoveSceneRequest(string sceneName, Guid canvasUuid)
        {
            this.AddRemoveSceneRequest(sceneName, (Guid?)canvasUuid);
        }

        private void AddRemoveSceneRequest(string sceneName, Guid? canvasUuid)
        {
            this._requests.Add(new(new { sceneName, canvasUuid }));
        }

        /// <summary>
        /// Adds a request to set the name of a scene (rename).
        /// </summary>
        /// <param name="sceneName">Name of the scene to be renamed</param>
        /// <param name="newSceneName">New name for the scene</param>
        public void AddSetSceneNameRequest(string sceneName, string newSceneName)
        {
            this.AddSetSceneNameRequest(sceneName, null, newSceneName);
        }

        /// <summary>
        /// Adds a request to set the name of a scene on a canvas (rename).
        /// </summary>
        /// <param name="sceneName">Name of the scene to be renamed</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="newSceneName">New name for the scene</param>
        public void AddSetSceneNameRequest(string sceneName, Guid canvasUuid, string newSceneName)
        {
            this.AddSetSceneNameRequest(sceneName, (Guid?)canvasUuid, newSceneName);
        }

        private void AddSetSceneNameRequest(string sceneName, Guid? canvasUuid, string newSceneName)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, newSceneName }));
        }

        /// <summary>
        /// Adds a request to get the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <returns>A <see cref="SceneTransitionResponse"/></returns>
        public void AddGetSceneSceneTransitionOverrideRequest(string sceneName)
        {
            this.AddGetSceneSceneTransitionOverrideRequest(sceneName, null);
        }

        /// <summary>
        /// Adds a request to get the scene transition overridden for a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <returns>A <see cref="SceneTransitionResponse"/></returns>
        public void AddGetSceneSceneTransitionOverrideRequest(string sceneName, Guid canvasUuid)
        {
            this.AddGetSceneSceneTransitionOverrideRequest(sceneName, (Guid?)canvasUuid);
        }

        private void AddGetSceneSceneTransitionOverrideRequest(string sceneName, Guid? canvasUuid)
        {
            this._requests.Add(new(new { sceneName, canvasUuid }));
        }

        /// <summary>
        /// Adds a request to get the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove.</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove.</param>
        public void AddSetSceneSceneTransitionOverrideRequest(string sceneName, string? transitionName, int? transitionDuration)
        {
            this.AddSetSceneSceneTransitionOverrideRequest(sceneName, null, transitionName, transitionDuration);
        }

        /// <summary>
        /// Adds a request to get the scene transition overridden for a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove.</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove.</param>
        public void AddSetSceneSceneTransitionOverrideRequest(string sceneName, Guid canvasUuid, string? transitionName, int? transitionDuration)
        {
            this.AddSetSceneSceneTransitionOverrideRequest(sceneName, (Guid?)canvasUuid, transitionName, transitionDuration);
        }

        private void AddSetSceneSceneTransitionOverrideRequest(string sceneName, Guid? canvasUuid, string? transitionName, int? transitionDuration)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, transitionName, transitionDuration }));
        }
    }
}

