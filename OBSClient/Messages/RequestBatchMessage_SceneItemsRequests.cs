namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get a list of all scene items in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the scene</returns>
        public void AddGetSceneItemListRequest(string sceneName)
        {
            this._requests.Add(new(new { sceneName }));
        }

        /// <summary>
        /// Adds a request to basically do a GetSceneItemList, but for groups.
        /// </summary>
        /// <param name="sceneName">Name of the group to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the group</returns>
        /// <remarks>
        /// Using groups at all in OBS is discouraged, as they are very broken under the hood. Please use nested scenes instead.
        /// </remarks>
        public void AddGetGroupSceneItemListRequest(string sceneName)
        {
            this._requests.Add(new(new { sceneName }));
        }

        /// <summary>
        /// Adds a request to search a scene for a source, and returns its id.
        /// </summary>
        /// <param name="sceneName">Name of the scene or group to search in</param>
        /// <param name="sourceName">Name of the source to find</param>
        /// <param name="searchOffset">Number of matches to skip during search. >= 0 means first forward. -1 means last (top) item (>= -1)</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddGetSceneItemIdRequest(string sceneName, string sourceName, int searchOffset = 0)
        {
            this._requests.Add(new(new { sceneName, sourceName, searchOffset }));
        }

        /// <summary>
        /// Adds a request to create a new scene item using a source.
        /// </summary>
        /// <param name="sceneName">Name of the scene to create the new item in</param>
        /// <param name="sourceName">Name of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddCreateSceneItemRequest(string sceneName, string sourceName, bool sceneItemEnabled = true)
        {
            this._requests.Add(new(new { sceneName, sourceName, sceneItemEnabled }));
        }

        /// <summary>
        /// Adds a request to remove a scene item from a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public void AddRemoveSceneItemRequest(string sceneName, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to duplicate a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneName">Name of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public void AddDuplicateSceneItemRequest(string sceneName, int sceneItemId, string? destinationSceneName = null)
        {
            this._requests.Add(new(new { sceneName, sceneItemId, destinationSceneName }));
        }

        /// <summary>
        /// Adds a request to get the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public void AddGetSceneItemTransformRequest(string sceneName, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public void AddSetSceneItemTransformRequest(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this._requests.Add(new(new { sceneName, sceneItemId, sceneItemTransform }));
        }

        /// <summary>
        /// Adds a request to get the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public void AddGetSceneItemEnabledRequest(string sceneName, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">	New enable state of the scene item</param>
        public void AddSetSceneItemEnabledRequest(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            this._requests.Add(new(new { sceneName, sceneItemId, sceneItemEnabled }));
        }

        /// <summary>
        /// Adds a request to get the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public void AddGetSceneItemLockedRequest(string sceneName, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public void AddSetSceneItemLockedRequest(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            this._requests.Add(new(new { sceneName, sceneItemId, sceneItemLocked }));
        }

        /// <summary>
        /// Adds a request to get the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns></returns>
        /// <remarks>
        /// An index of 0 is at the bottom of the source list in the UI.
        /// Scenes and Groups
        /// </remarks>
        public void AddGetSceneItemIndexRequest(string sceneName, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public void AddSetSceneItemIndexRequest(string sceneName, int sceneItemId, int sceneItemIndex)
        {
            this._requests.Add(new(new { sceneName, sceneItemId, sceneItemIndex }));
        }

        /// <summary>
        /// Adds a request to get the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public void AddGetSceneItemBlendModeRequest(string sceneName, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public void AddSetSceneItemBlendModeRequest(string sceneName, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            this._requests.Add(new(new { sceneName, sceneItemId, sceneItemBlendMode }));
        }
    }
}
