namespace OBSStudioClient
{
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets an array of all scenes in OBS.
        /// </summary>
        /// <returns>A <see cref="SceneListResponse"/></returns>
        public async Task<SceneListResponse> GetSceneList()
        {
            return await this.GetSceneList(null);
        }

        /// <summary>
        /// Gets an array of all scenes on a canvas in OBS.
        /// </summary>
        /// <param name="canvasUuid">UUID of the canvas the scenes are in</param>
        /// <returns>A <see cref="SceneListResponse"/></returns>
        public async Task<SceneListResponse> GetSceneList(Guid canvasUuid)
        {
            return await this.GetSceneList((Guid?)canvasUuid);
        }

        private async Task<SceneListResponse> GetSceneList(Guid? canvasUuid)
        {
            return await this.SendRequestAsync<SceneListResponse>(new { canvasUuid });
        }

        /// <summary>
        /// Gets an array of all groups in OBS.
        /// </summary>
        /// <returns>Array of group names</returns>
        /// <remarks>
        /// Groups in OBS are actually scenes, but renamed and modified. In obs-websocket, we treat them as scenes where we can.
        /// </remarks>
        public async Task<string[]> GetGroupList()
        {
            return (await this.SendRequestAsync<GroupsResponse>()).Groups;
        }

        /// <summary>
        /// Gets the current program scene.
        /// </summary>
        /// <returns>A <see cref="CurrentProgramSceneNameResponse"/></returns>
        public async Task<CurrentProgramSceneNameResponse> GetCurrentProgramScene()
        {
            return await this.SendRequestAsync<CurrentProgramSceneNameResponse>();
        }

        /// <summary>
        /// Sets the current program scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current program scene</param>
        public async Task SetCurrentProgramScene(string sceneName)
        {
            await this.SetCurrentProgramScene(sceneName, null);
        }

        /// <summary>
        /// Sets the current program scene.
        /// </summary>
        /// <param name="sceneUuid">Scene to set as the current program scene</param>
        public async Task SetCurrentProgramScene(Guid sceneUuid)
        {
            await this.SetCurrentProgramScene(null, sceneUuid);
        }

        private async Task SetCurrentProgramScene(string? sceneName, Guid? sceneUuid)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid });
        }

        /// <summary>
        /// Gets the current preview scene.
        /// </summary>
        /// <returns>Current preview scene</returns>
        public async Task<CurrentPreviewSceneNameResponse> GetCurrentPreviewScene()
        {
            return await this.SendRequestAsync<CurrentPreviewSceneNameResponse>();
        }

        /// <summary>
        /// Sets the current preview scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current preview scene</param>
        public async Task SetCurrentPreviewScene(string sceneName)
        {
            await this.SetCurrentPreviewScene(sceneName, null);
        }

        /// <summary>
        /// Sets the current preview scene.
        /// </summary>
        /// <param name="sceneUuid">Scene to set as the current preview scene</param>
        public async Task SetCurrentPreviewScene(Guid sceneUuid)
        {
            await this.SetCurrentPreviewScene(null, sceneUuid);
        }

        private async Task SetCurrentPreviewScene(string? sceneName, Guid? sceneUuid)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid });
        }

        /// <summary>
        /// Creates a new scene in OBS.
        /// </summary>
        /// <param name="sceneName">Name for the new scene</param>
        public async Task<Guid> CreateScene(string sceneName)
        {
            return await this.CreateScene(sceneName, null);
        }

        /// <summary>
        /// Creates a new scene on a canvas in OBS.
        /// </summary>
        /// <param name="sceneName">Name for the new scene</param>
        /// <param name="canvasUuid">UUID of the canvas to create the new scene in</param>
        public async Task<Guid> CreateScene(string sceneName, Guid canvasUuid)
        {
            return await this.CreateScene(sceneName, (Guid?)canvasUuid);
        }

        private async Task<Guid> CreateScene(string sceneName, Guid? canvasUuid)
        {
            return (await this.SendRequestAsync<CreateSceneResponse>(new { sceneName, canvasUuid })).SceneUuid;
        }

        /// <summary>
        /// Removes a scene from OBS.
        /// </summary>
        /// <param name="sceneName">Name of the scene to remove</param>
        public async Task RemoveScene(string sceneName)
        {
            await this.RemoveScene(sceneName, null, null);
        }

        /// <summary>
        /// Removes a scene from a canvas in OBS.
        /// </summary>
        /// <param name="sceneName">Name of the scene to remove</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        public async Task RemoveScene(string sceneName, Guid canvasUuid)
        {
            await this.RemoveScene(sceneName, canvasUuid, null);
        }

        /// <summary>
        /// Removes a scene from OBS.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene to remove</param>
        public async Task RemoveScene(Guid sceneUuid)
        {
            await this.RemoveScene(null, null, sceneUuid);
        }

        private async Task RemoveScene(string? sceneName, Guid? canvasUuid, Guid? sceneUuid)
        {
            await this.SendRequestAsync(new { sceneName, canvasUuid, sceneUuid });
        }

        /// <summary>
        /// Sets the name of a scene (rename).
        /// </summary>
        /// <param name="sceneName">Name of the scene to be renamed</param>
        /// <param name="newSceneName">New name for the scene</param>
        public async Task SetSceneName(string sceneName, string newSceneName)
        {
            await this.SetSceneName(sceneName, null, null, newSceneName);
        }

        /// <summary>
        /// Sets the name of a scene on a canvas (rename).
        /// </summary>
        /// <param name="sceneName">Name of the scene to be renamed</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="newSceneName">New name for the scene</param>
        public async Task SetSceneName(string sceneName, Guid canvasUuid, string newSceneName)
        {
            await this.SetSceneName(sceneName, canvasUuid, null, newSceneName);
        }

        /// <summary>
        /// Sets the name of a scene (rename).
        /// </summary>
        /// <param name="sceneUuid">uuid of the scene to be renamed</param>
        /// <param name="newSceneName">New name for the scene</param>
        public async Task SetSceneName(Guid sceneUuid, string newSceneName)
        {
            await this.SetSceneName(null, null, sceneUuid, newSceneName);
        }

        private async Task SetSceneName(string? sceneName, Guid? canvasUuid, Guid? sceneUuid, string newSceneName)
        {
            await this.SendRequestAsync(new { sceneName, canvasUuid, sceneUuid, newSceneName });
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <returns>A <see cref="SceneTransitionResponse"/></returns>
        public async Task<SceneTransitionResponse> GetSceneSceneTransitionOverride(string sceneName)
        {
            return await this.GetSceneSceneTransitionOverride(sceneName, null, null);
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <returns>A <see cref="SceneTransitionResponse"/></returns>
        public async Task<SceneTransitionResponse> GetSceneSceneTransitionOverride(string sceneName, Guid canvasUuid)
        {
            return await this.GetSceneSceneTransitionOverride(sceneName, canvasUuid, null);
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene</param>
        /// <returns>A <see cref="SceneTransitionResponse"/></returns>
        public async Task<SceneTransitionResponse> GetSceneSceneTransitionOverride(Guid sceneUuid)
        {
            return await this.GetSceneSceneTransitionOverride(null, null, sceneUuid);
        }

        private async Task<SceneTransitionResponse> GetSceneSceneTransitionOverride(string? sceneName, Guid? canvasUuid, Guid? sceneUuid)
        {
            return await this.SendRequestAsync<SceneTransitionResponse>(new { sceneName, canvasUuid, sceneUuid });
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove.</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove.</param>
        public async Task SetSceneSceneTransitionOverride(string sceneName, string? transitionName, int? transitionDuration)
        {
            await this.SetSceneSceneTransitionOverride(sceneName, null, null, transitionName, transitionDuration);
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove.</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove.</param>
        public async Task SetSceneSceneTransitionOverride(string sceneName, Guid canvasUuid, string? transitionName, int? transitionDuration)
        {
            await this.SetSceneSceneTransitionOverride(sceneName, canvasUuid, null, transitionName, transitionDuration);
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove.</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove.</param>
        public async Task SetSceneSceneTransitionOverride(Guid sceneUuid, string? transitionName, int? transitionDuration)
        {
            await this.SetSceneSceneTransitionOverride(null, null, sceneUuid, transitionName, transitionDuration);
        }

        private async Task SetSceneSceneTransitionOverride(string? sceneName, Guid? canvasUuid, Guid? sceneUuid, string? transitionName, int? transitionDuration)
        {
            await this.SendRequestAsync(new { sceneName, canvasUuid, sceneUuid, transitionName, transitionDuration });
        }
    }
}
