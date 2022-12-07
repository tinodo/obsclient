namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets an array of all scenes in OBS.
        /// </summary>
        /// <returns>A <see cref="SceneListResponseData"/></returns>
        public async Task<SceneListResponseData> GetSceneList()
        {
            return await this.SendRequestAsync< SceneListResponseData>();
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
            return (await this.SendRequestAsync<GroupsResponseData>()).Groups;
        }

        /// <summary>
        /// Gets the current program scene.
        /// </summary>
        /// <returns>Current program scene</returns>
        public async Task<string> GetCurrentProgramScene()
        {
            return (await this.SendRequestAsync<CurrentProgramSceneNameResponseData>()).CurrentProgramSceneName;
        }

        /// <summary>
        /// Sets the current program scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current program scene</param>
        public async Task SetCurrentProgramScene(string sceneName)
        {
            await this.SendRequestAsync(new { sceneName });
        }

        /// <summary>
        /// Gets the current preview scene.
        /// </summary>
        /// <returns>Current preview scene</returns>
        public async Task<string?> GetCurrentPreviewScene()
        {
            return (await this.SendRequestAsync<CurrentPreviewSceneNameResponseData>()).CurrentPreviewSceneName;
        }

        /// <summary>
        /// Sets the current preview scene.
        /// </summary>
        /// <param name="sceneName">Scene to set as the current preview scene</param>
        public async Task SetCurrentPreviewScene(string sceneName)
        {
            await this.SendRequestAsync(new { sceneName });
        }

        /// <summary>
        /// Creates a new scene in OBS.
        /// </summary>
        /// <param name="sceneName">Name for the new scene</param>
        public async Task CreateScene(string sceneName)
        {
            await this.SendRequestAsync(new { sceneName });
        }

        /// <summary>
        /// Removes a scene from OBS.
        /// </summary>
        /// <param name="sceneName">Name of the scene to remove</param>
        public async Task RemoveScene(string sceneName)
        {
            await this.SendRequestAsync(new { sceneName });
        }

        /// <summary>
        /// Sets the name of a scene (rename).
        /// </summary>
        /// <param name="sceneName">Name of the scene to be renamed</param>
        /// <param name="newSceneName">New name for the scene</param>
        public async Task SetSceneName(string sceneName, string newSceneName)
        {
            await this.SendRequestAsync(new { sceneName, newSceneName });
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <returns>A <see cref="SceneTransitionResponseData"/></returns>
        public async Task<SceneTransitionResponseData> GetSceneSceneTransitionOverride(string sceneName)
        {
            return await this.SendRequestAsync<SceneTransitionResponseData>(new { sceneName });
        }

        /// <summary>
        /// Gets the scene transition overridden for a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <param name="transitionName">Name of the scene transition to use as override. Specify null to remove</param>
        /// <param name="transitionDuration">Duration to use for any overridden transition. Specify null to remove (>= 50, <= 20000)</param>
        public async Task SetSceneSceneTransitionOverride(string sceneName, string? transitionName, int? transitionDuration)
        {
            await this.SendRequestAsync(new { sceneName, transitionName, transitionDuration });
        }
    }
}
