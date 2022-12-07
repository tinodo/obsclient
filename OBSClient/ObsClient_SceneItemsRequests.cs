namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets a list of all scene items in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the scene</returns>
        public async Task<SceneItem[]> GetSceneItemList(string sceneName)
        {
            return (await this.SendRequestAsync<SceneItemsResponseData>(new { sceneName })).SceneItems;
        }

        /// <summary>
        /// Basically GetSceneItemList, but for groups.
        /// </summary>
        /// <param name="sceneName">Name of the group to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the group</returns>
        /// <remarks>
        /// Using groups at all in OBS is discouraged, as they are very broken under the hood. Please use nested scenes instead.
        /// </remarks>
        public async Task<SceneItem[]> GetGroupSceneItemList(string sceneName)
        {
            return (await this.SendRequestAsync<SceneItemsResponseData>(new { sceneName })).SceneItems;
        }

        /// <summary>
        /// Searches a scene for a source, and returns its id.
        /// </summary>
        /// <param name="sceneName">Name of the scene or group to search in</param>
        /// <param name="sourceName">Name of the source to find</param>
        /// <param name="searchOffset">Number of matches to skip during search. >= 0 means first forward. -1 means last (top) item (>= -1)</param>
        /// <returns>Numeric ID of the scene item</returns>
        public async Task<int> GetSceneItemId(string sceneName, string sourceName, int searchOffset = 0)
        {
            return (await this.SendRequestAsync<SceneItemIdResponseData>(new { sceneName, sourceName, searchOffset })).SceneItemId;
        }

        /// <summary>
        /// Creates a new scene item using a source.
        /// </summary>
        /// <param name="sceneName">Name of the scene to create the new item in</param>
        /// <param name="sourceName">Name of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public async Task<int> CreateSceneItem(string sceneName, string sourceName, bool sceneItemEnabled = true)
        {
            return (await this.SendRequestAsync<SceneItemIdResponseData>(new { sceneName, sourceName, sceneItemEnabled })).SceneItemId;
        }

        /// <summary>
        /// Removes a scene item from a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public async Task RemoveSceneItem(string sceneName, int sceneItemId)
        {
            await this.SendRequestAsync(new { sceneName, sceneItemId });
        }

        /// <summary>
        /// Duplicates a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneName">Name of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public async Task<int> DuplicateSceneItem(string sceneName, int sceneItemId, string? destinationSceneName = null)
        {
            return (await this.SendRequestAsync<SceneItemIdResponseData>(new { sceneName, sceneItemId, destinationSceneName })).SceneItemId;
        }

        /// <summary>
        /// Gets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public async Task<SceneItemTransform> GetSceneItemTransform(string sceneName, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemTransformResponseData>(new { sceneName, sceneItemId })).SceneItemTransform;
        }

        /// <summary>
        /// Sets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public async Task SetSceneItemTransform(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            await this.SendRequestAsync(new { sceneName, sceneItemId, sceneItemTransform });
        }

        /// <summary>
        /// Gets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public async Task<bool> GetSceneItemEnabled(string sceneName, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemEnabledResponseData>(new { sceneName, sceneItemId })).SceneItemEnabled;
        }

        /// <summary>
        /// Sets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">	New enable state of the scene item</param>
        public async Task SetSceneItemEnabled(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            await this.SendRequestAsync(new { sceneName, sceneItemId, sceneItemEnabled });
        }

        /// <summary>
        /// Gets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public async Task<bool> GetSceneItemLocked(string sceneName, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemlockedResponseData>(new { sceneName, sceneItemId })).SceneItemLocked;
        }

        /// <summary>
        /// Sets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public async Task SetSceneItemLocked(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            await this.SendRequestAsync(new { sceneName, sceneItemId, sceneItemLocked });
        }

        /// <summary>
        /// Gets the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns></returns>
        /// <remarks>
        /// An index of 0 is at the bottom of the source list in the UI.
        /// Scenes and Groups
        /// </remarks>
        public async Task<int> GetSceneItemIndex(string sceneName, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemIndexResponseData>(new { sceneName, sceneItemId })).SceneItemIndex;
        }

        /// <summary>
        /// Sets the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public async Task SetSceneItemIndex(string sceneName, int sceneItemId, int sceneItemIndex)
        {
            await this.SendRequestAsync(new { sceneName, sceneItemId, sceneItemIndex });
        }

        /// <summary>
        /// Gets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public async Task<BlendMode> GetSceneItemBlendMode(string sceneName, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemBlendModeResponseData>(new { sceneName, sceneItemId })).SceneItemBlendMode;
        }

        /// <summary>
        /// Sets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public async Task SetSceneItemBlendMode(string sceneName, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            await this.SendRequestAsync(new { sceneName, sceneItemId, sceneItemBlendMode });
        }
    }
}
