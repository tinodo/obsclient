namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets a list of all scene items in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the scene</returns>
        public async Task<SceneItem[]> GetSceneItemList(string sceneName)
        {
            return await this.GetSceneItemList(sceneName, null);
        }

        /// <summary>
        /// Gets a list of all scene items in a scene.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the scene</returns>
        public async Task<SceneItem[]> GetSceneItemList(Guid sceneUuid)
        {
            return await this.GetSceneItemList(null, sceneUuid);
        }

        private async Task<SceneItem[]> GetSceneItemList(string? sceneName, Guid? sceneUuid)
        {
            return (await this.SendRequestAsync<SceneItemsResponse>(new { sceneName, sceneUuid })).SceneItems;
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
            return await this.GetGroupSceneItemList(sceneName, null);
        }

        /// <summary>
        /// Basically GetSceneItemList, but for groups.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the group to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the group</returns>
        /// <remarks>
        /// Using groups at all in OBS is discouraged, as they are very broken under the hood. Please use nested scenes instead.
        /// </remarks>
        public async Task<SceneItem[]> GetGroupSceneItemList(Guid sceneUuid)
        {
            return await this.GetGroupSceneItemList(null, sceneUuid);
        }

        private async Task<SceneItem[]> GetGroupSceneItemList(string? sceneName, Guid? sceneUuid)
        {
            return (await this.SendRequestAsync<SceneItemsResponse>(new { sceneName, sceneUuid })).SceneItems;
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
            return await this.GetSceneItemId(sceneName, null, sourceName, searchOffset);
        }

        /// <summary>
        /// Searches a scene for a source, and returns its id.
        /// </summary>
        /// <param name="sceneUuid">Name of the scene or group to search in</param>
        /// <param name="sourceName">Name of the source to find</param>
        /// <param name="searchOffset">Number of matches to skip during search. >= 0 means first forward. -1 means last (top) item (>= -1)</param>
        /// <returns>Numeric ID of the scene item</returns>
        public async Task<int> GetSceneItemId(Guid sceneUuid, string sourceName, int searchOffset = 0)
        {
            return await this.GetSceneItemId(null, sceneUuid, sourceName, searchOffset);
        }

        private async Task<int> GetSceneItemId(string? sceneName, Guid? sceneUuid, string sourceName, int searchOffset = 0)
        {
            return (await this.SendRequestAsync<SceneItemIdResponse>(new { sceneName, sceneUuid, sourceName, searchOffset })).SceneItemId;
        }

        /// <summary>
        /// Gets the source associated with a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item</param>
        /// <returns>The details of the source.</returns>
        public async Task<SceneItemSourceResponse> GetSceneItemSource(string sceneName, int sceneItemId)
        {
            return await this.GetSceneItemSource(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Gets the source associated with a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item</param>
        /// <returns>The details of the source.</returns>
        public async Task<SceneItemSourceResponse> GetSceneItemSource(Guid sceneUuid, int sceneItemId)
        {
            return await this.GetSceneItemSource(null, sceneUuid, sceneItemId);
        }

        private async Task<SceneItemSourceResponse> GetSceneItemSource(string? sceneName, Guid? sceneUuid, int sceneItemId)
        {
            return await this.SendRequestAsync<SceneItemSourceResponse>(new { sceneName, sceneUuid, sceneItemId });
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
            return await this.CreateSceneItem(sceneName, null, sourceName, null, sceneItemEnabled);
        }

        /// <summary>
        /// Creates a new scene item using a source.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene to create the new item in</param>
        /// <param name="sourceName">Name of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public async Task<int> CreateSceneItem(Guid sceneUuid, string sourceName, bool sceneItemEnabled = true)
        {
            return await this.CreateSceneItem(null, sceneUuid, sourceName, null, sceneItemEnabled);
        }

        /// <summary>
        /// Creates a new scene item using a source.
        /// </summary>
        /// <param name="sceneName">Name of the scene to create the new item in</param>
        /// <param name="sourceUuid">Uuid of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public async Task<int> CreateSceneItem(string sceneName, Guid sourceUuid, bool sceneItemEnabled = true)
        {
            return await this.CreateSceneItem(sceneName, null, null, sourceUuid, sceneItemEnabled);
        }

        /// <summary>
        /// Creates a new scene item using a source.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene to create the new item in</param>
        /// <param name="sourceUuid">Uuid of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public async Task<int> CreateSceneItem(Guid sceneUuid, Guid sourceUuid, bool sceneItemEnabled = true)
        {
            return await this.CreateSceneItem(null, sceneUuid, null, sourceUuid, sceneItemEnabled);
        }

        private async Task<int> CreateSceneItem(string? sceneName, Guid? sceneUuid, string? sourceName, Guid? sourceUuid, bool sceneItemEnabled = true)
        {
            return (await this.SendRequestAsync<SceneItemIdResponse>(new { sceneName, sceneUuid, sourceName, sourceUuid, sceneItemEnabled })).SceneItemId;
        }

        /// <summary>
        /// Removes a scene item from a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public async Task RemoveSceneItem(string sceneName, int sceneItemId)
        {
            await this.RemoveSceneItem(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Removes a scene item from a scene.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public async Task RemoveSceneItem(Guid sceneUuid, int sceneItemId)
        {
            await this.RemoveSceneItem(null, sceneUuid, sceneItemId);
        }

        private async Task RemoveSceneItem(string? sceneName, Guid? sceneUuid, int sceneItemId)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid, sceneItemId });
        }

        /// <summary>
        /// Duplicates a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneName">Name of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public async Task<int> DuplicateSceneItem(string sceneName, int sceneItemId, string destinationSceneName)
        {
            return await this.DuplicateSceneItem(sceneName, null, sceneItemId, destinationSceneName, null);
        }

        /// <summary>
        /// Duplicates a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public async Task<int> DuplicateSceneItem(string sceneName, int sceneItemId)
        {
            return await this.DuplicateSceneItem(sceneName, null, sceneItemId, null, null);
        }

        /// <summary>
        /// Duplicates a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneUuid">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneName">Name of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public async Task<int> DuplicateSceneItem(Guid sceneUuid, int sceneItemId, string destinationSceneName)
        {
            return await this.DuplicateSceneItem(null, sceneUuid, sceneItemId, destinationSceneName, null);
        }

        /// <summary>
        /// Duplicates a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneUuid">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public async Task<int> DuplicateSceneItem(Guid sceneUuid, int sceneItemId)
        {
            return await this.DuplicateSceneItem(null, sceneUuid, sceneItemId, null, null);
        }

        /// <summary>
        /// Duplicates a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneUuid">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneUuid">Uuid of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public async Task<int> DuplicateSceneItem(Guid sceneUuid, int sceneItemId, Guid destinationSceneUuid)
        {
            return await this.DuplicateSceneItem(null, sceneUuid, sceneItemId, null, destinationSceneUuid);
        }

        private async Task<int> DuplicateSceneItem(string? sceneName, Guid? sceneUuid, int sceneItemId, string? destinationSceneName, Guid? destinationSceneUuid)
        {
            return (await this.SendRequestAsync<SceneItemIdResponse>(new { sceneName, sceneUuid, sceneItemId, destinationSceneName, destinationSceneUuid })).SceneItemId;
        }

        /// <summary>
        /// Gets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public async Task<SceneItemTransform> GetSceneItemTransform(string sceneName, int sceneItemId)
        {
            return await this.GetSceneItemTransform(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Gets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public async Task<SceneItemTransform> GetSceneItemTransform(Guid sceneUuid, int sceneItemId)
        {
            return await this.GetSceneItemTransform(null, sceneUuid, sceneItemId);
        }

        private async Task<SceneItemTransform> GetSceneItemTransform(string? sceneName, Guid? sceneUuid, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemTransformResponse>(new { sceneName, sceneUuid, sceneItemId })).SceneItemTransform;
        }

        /// <summary>
        /// Sets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public async Task SetSceneItemTransform(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            await this.SetSceneItemTransform(sceneName, null, sceneItemId, sceneItemTransform);
        }

        /// <summary>
        /// Sets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public async Task SetSceneItemTransform(Guid sceneUuid, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            await this.SetSceneItemTransform(null, sceneUuid, sceneItemId, sceneItemTransform);
        }

        private async Task SetSceneItemTransform(string? sceneName, Guid? sceneUuid, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid, sceneItemId, sceneItemTransform });
        }

        /// <summary>
        /// Gets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public async Task<bool> GetSceneItemEnabled(string sceneName, int sceneItemId)
        {
            return await this.GetSceneItemEnabled(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Gets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public async Task<bool> GetSceneItemEnabled(Guid sceneUuid, int sceneItemId)
        {
            return await this.GetSceneItemEnabled(null, sceneUuid, sceneItemId);
        }

        private async Task<bool> GetSceneItemEnabled(string? sceneName, Guid? sceneUuid, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemEnabledResponse>(new { sceneName, sceneUuid, sceneItemId })).SceneItemEnabled;
        }

        /// <summary>
        /// Sets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">	New enable state of the scene item</param>
        public async Task SetSceneItemEnabled(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            await this.SetSceneItemEnabled(sceneName, null, sceneItemId, sceneItemEnabled);
        }

        /// <summary>
        /// Sets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">	New enable state of the scene item</param>
        public async Task SetSceneItemEnabled(Guid sceneUuid, int sceneItemId, bool sceneItemEnabled)
        {
            await this.SetSceneItemEnabled(null, sceneUuid, sceneItemId, sceneItemEnabled);
        }

        private async Task SetSceneItemEnabled(string? sceneName, Guid? sceneUuid, int sceneItemId, bool sceneItemEnabled)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid, sceneItemId, sceneItemEnabled });
        }

        /// <summary>
        /// Gets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public async Task<bool> GetSceneItemLocked(string sceneName, int sceneItemId)
        {
            return await this.GetSceneItemLocked(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Gets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public async Task<bool> GetSceneItemLocked(Guid sceneUuid, int sceneItemId)
        {
            return await this.GetSceneItemLocked(null, sceneUuid, sceneItemId);
        }

        private async Task<bool> GetSceneItemLocked(string? sceneName, Guid? sceneUuid, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemlockedResponse>(new { sceneName, sceneUuid, sceneItemId })).SceneItemLocked;
        }

        /// <summary>
        /// Sets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public async Task SetSceneItemLocked(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            await this.SetSceneItemLocked(sceneName, null, sceneItemId, sceneItemLocked);
        }

        /// <summary>
        /// Sets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public async Task SetSceneItemLocked(Guid sceneUuid, int sceneItemId, bool sceneItemLocked)
        {
            await this.SetSceneItemLocked(null, sceneUuid, sceneItemId, sceneItemLocked);
        }

        private async Task SetSceneItemLocked(string? sceneName, Guid? sceneUuid, int sceneItemId, bool sceneItemLocked)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid, sceneItemId, sceneItemLocked });
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
            return await this.GetSceneItemIndex(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Gets the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns></returns>
        /// <remarks>
        /// An index of 0 is at the bottom of the source list in the UI.
        /// Scenes and Groups
        /// </remarks>
        public async Task<int> GetSceneItemIndex(Guid sceneUuid, int sceneItemId)
        {
            return await this.GetSceneItemIndex(null, sceneUuid, sceneItemId);
        }

        private async Task<int> GetSceneItemIndex(string? sceneName, Guid? sceneUuid, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemIndexResponse>(new { sceneName, sceneUuid, sceneItemId })).SceneItemIndex;
        }

        /// <summary>
        /// Sets the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public async Task SetSceneItemIndex(string sceneName, int sceneItemId, int sceneItemIndex)
        {
            await this.SetSceneItemIndex(sceneName, null, sceneItemId, sceneItemIndex);
        }

        /// <summary>
        /// Sets the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public async Task SetSceneItemIndex(Guid sceneUuid, int sceneItemId, int sceneItemIndex)
        {
            await this.SetSceneItemIndex(null, sceneUuid, sceneItemId, sceneItemIndex);
        }

        private async Task SetSceneItemIndex(string? sceneName, Guid? sceneUuid, int sceneItemId, int sceneItemIndex)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid, sceneItemId, sceneItemIndex });
        }

        /// <summary>
        /// Gets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public async Task<BlendMode> GetSceneItemBlendMode(string sceneName, int sceneItemId)
        {
            return await this.GetSceneItemBlendMode(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Gets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public async Task<BlendMode> GetSceneItemBlendMode(Guid sceneUuid, int sceneItemId)
        {
            return await this.GetSceneItemBlendMode(null, sceneUuid, sceneItemId);  
        }

        private async Task<BlendMode> GetSceneItemBlendMode(string? sceneName, Guid? sceneUuid, int sceneItemId)
        {
            return (await this.SendRequestAsync<SceneItemBlendModeResponse>(new { sceneName, sceneUuid, sceneItemId })).SceneItemBlendMode;
        }

        /// <summary>
        /// Sets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public async Task SetSceneItemBlendMode(string sceneName, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            await this.SetSceneItemBlendMode(sceneName, null, sceneItemId, sceneItemBlendMode);
        }

        /// <summary>
        /// Sets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public async Task SetSceneItemBlendMode(Guid sceneUuid, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            await this.SetSceneItemBlendMode(null, sceneUuid, sceneItemId, sceneItemBlendMode);
        }

        private async Task SetSceneItemBlendMode(string? sceneName, Guid? sceneUuid, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            await this.SendRequestAsync(new { sceneName, sceneUuid, sceneItemId, sceneItemBlendMode });
        }
    }
}
