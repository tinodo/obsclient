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
        public SceneItem[] GetSceneItemList(string sceneName)
        {
            return this.SendRequest<SceneItemsResponseData>(new { sceneName }).SceneItems;
        }

        /// <summary>
        /// Basically GetSceneItemList, but for groups.
        /// </summary>
        /// <param name="sceneName">Name of the group to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the group</returns>
        /// <remarks>
        /// Using groups at all in OBS is discouraged, as they are very broken under the hood. Please use nested scenes instead.
        /// </remarks>
        public SceneItem[] GetGroupSceneItemList(string sceneName)
        {
            return this.SendRequest<SceneItemsResponseData>(new { sceneName }).SceneItems;
        }

        /// <summary>
        /// Searches a scene for a source, and returns its id.
        /// </summary>
        /// <param name="sceneName">Name of the scene or group to search in</param>
        /// <param name="sourceName">Name of the source to find</param>
        /// <param name="searchOffset">Number of matches to skip during search. >= 0 means first forward. -1 means last (top) item (>= -1)</param>
        /// <returns>Numeric ID of the scene item</returns>
        public int GetSceneItemId(string sceneName, string sourceName, int searchOffset = 0)
        {
            return this.SendRequest<SceneItemIdResponseData>(new { sceneName, sourceName, searchOffset }).SceneItemId;
        }

        /// <summary>
        /// Creates a new scene item using a source.
        /// </summary>
        /// <param name="sceneName">Name of the scene to create the new item in</param>
        /// <param name="sourceName">Name of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public int CreateSceneItem(string sceneName, string sourceName, bool sceneItemEnabled = true)
        {
            return this.SendRequest<SceneItemIdResponseData>(new { sceneName, sourceName, sceneItemEnabled }).SceneItemId;
        }

        /// <summary>
        /// Removes a scene item from a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public void RemoveSceneItem(string sceneName, int sceneItemId)
        {
            this.SendRequest(new { sceneName, sceneItemId });
        }

        /// <summary>
        /// Duplicates a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneName">Name of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public int DuplicateSceneItem(string sceneName, int sceneItemId, string? destinationSceneName = null)
        {
            return this.SendRequest<SceneItemIdResponseData>(new { sceneName, sceneItemId, destinationSceneName }).SceneItemId;
        }

        /// <summary>
        /// Gets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public SceneItemTransform GetSceneItemTransform(string sceneName, int sceneItemId)
        {
            return this.SendRequest<SceneItemTransformResponseData>(new { sceneName, sceneItemId }).SceneItemTransform;
        }

        /// <summary>
        /// Sets the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public void SetSceneItemTransform(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this.SendRequest(new { sceneName, sceneItemId, sceneItemTransform });
        }

        /// <summary>
        /// Gets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public bool GetSceneItemEnabled(string sceneName, int sceneItemId)
        {
            return this.SendRequest<SceneItemEnabledResponseData>(new { sceneName, sceneItemId }).SceneItemEnabled;
        }

        /// <summary>
        /// Sets the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">	New enable state of the scene item</param>
        public void SetSceneItemEnabled(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            this.SendRequest(new { sceneName, sceneItemId, sceneItemEnabled });
        }

        /// <summary>
        /// Gets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public bool GetSceneItemLocked(string sceneName, int sceneItemId)
        {
            return this.SendRequest<SceneItemlockedResponseData>(new { sceneName, sceneItemId }).SceneItemLocked;
        }

        /// <summary>
        /// Sets the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public void SetSceneItemLocked(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            this.SendRequest(new { sceneName, sceneItemId, sceneItemLocked });
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
        public int GetSceneItemIndex(string sceneName, int sceneItemId)
        {
            return this.SendRequest<SceneItemIndexResponseData>(new { sceneName, sceneItemId }).SceneItemIndex;
        }

        /// <summary>
        /// Sets the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public void SetSceneItemIndex(string sceneName, int sceneItemId, int sceneItemIndex)
        {
            this.SendRequest(new { sceneName, sceneItemId, sceneItemIndex });
        }

        /// <summary>
        /// Gets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public BlendMode GetSceneItemBlendMode(string sceneName, int sceneItemId)
        {
            return this.SendRequest<SceneItemBlendModeResponseData>(new { sceneName, sceneItemId }).SceneItemBlendMode;
        }

        /// <summary>
        /// Sets the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public void SetSceneItemBlendMode(string sceneName, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            this.SendRequest(new { sceneName, sceneItemId, sceneItemBlendMode });
        }
    }
}
