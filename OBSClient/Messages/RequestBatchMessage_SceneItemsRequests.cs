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
            this.AddGetSceneItemListRequest(sceneName, null);
        }

        /// <summary>
        /// Adds a request to get a list of all scene items in a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene to get the items of</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <returns>Array of <see cref="SceneItem"/> in the scene</returns>
        public void AddGetSceneItemListRequest(string sceneName, Guid canvasUuid)
        {
            this.AddGetSceneItemListRequest(sceneName, (Guid?)canvasUuid);
        }

        /// <summary>
        /// Adds a request to get a list of all scene items in a scene.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the scene</returns>
        public void AddGetSceneItemListRequest(Guid sceneUuid)
        {
            this._requests.Add(new(new { sceneUuid }));
        }

        private void AddGetSceneItemListRequest(string sceneName, Guid? canvasUuid)
        {
            this._requests.Add(new(new { sceneName, canvasUuid }));
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
            this.AddGetGroupSceneItemListRequest(sceneName, null);
        }

        /// <summary>
        /// Adds a request to basically do a GetSceneItemList, but for groups on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the group to get the items of</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <returns>Array of <see cref="SceneItem"/> in the group</returns>
        /// <remarks>
        /// Using groups at all in OBS is discouraged, as they are very broken under the hood. Please use nested scenes instead.
        /// </remarks>
        public void AddGetGroupSceneItemListRequest(string sceneName, Guid canvasUuid)
        {
            this.AddGetGroupSceneItemListRequest(sceneName, (Guid?)canvasUuid);
        }

        /// <summary>
        /// Adds a request to basically do a GetSceneItemList, but for groups.
        /// </summary>
        /// <param name="sceneUuid">UUID of the group to get the items of</param>
        /// <returns>Array of <see cref="SceneItem"/> in the group</returns>
        /// <remarks>
        /// Using groups at all in OBS is discouraged, as they are very broken under the hood. Please use nested scenes instead.
        /// </remarks>
        public void AddGetGroupSceneItemListRequest(Guid sceneUuid)
        {
            this._requests.Add(new(new { sceneUuid }));
        }

        private void AddGetGroupSceneItemListRequest(string sceneName, Guid? canvasUuid)
        {
            this._requests.Add(new(new { sceneName, canvasUuid }));
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
            this.AddGetSceneItemIdRequest(sceneName, null, sourceName, searchOffset);
        }

        /// <summary>
        /// Adds a request to search a scene on a canvas for a source, and returns its id.
        /// </summary>
        /// <param name="sceneName">Name of the scene or group to search in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sourceName">Name of the source to find</param>
        /// <param name="searchOffset">Number of matches to skip during search. >= 0 means first forward. -1 means last (top) item (>= -1)</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddGetSceneItemIdRequest(string sceneName, Guid canvasUuid, string sourceName, int searchOffset = 0)
        {
            this.AddGetSceneItemIdRequest(sceneName, (Guid?)canvasUuid, sourceName, searchOffset);
        }

        /// <summary>
        /// Adds a request to search a scene for a source, and returns its id.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene or group to search in</param>
        /// <param name="sourceName">Name of the source to find</param>
        /// <param name="searchOffset">Number of matches to skip during search. >= 0 means first forward. -1 means last (top) item (>= -1)</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddGetSceneItemIdRequest(Guid sceneUuid, string sourceName, int searchOffset = 0)
        {
            this._requests.Add(new(new { sceneUuid, sourceName, searchOffset }));
        }

        private void AddGetSceneItemIdRequest(string sceneName, Guid? canvasUuid, string sourceName, int searchOffset = 0)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sourceName, searchOffset }));
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
            this.AddCreateSceneItemRequest(sceneName, null, null, sourceName, null, sceneItemEnabled);
        }

        /// <summary>
        /// Adds a request to create a new scene item using a source on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene to create the new item in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sourceName">Name of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddCreateSceneItemRequest(string sceneName, Guid canvasUuid, string sourceName, bool sceneItemEnabled = true)
        {
            this.AddCreateSceneItemRequest(sceneName, canvasUuid, null, sourceName, null, sceneItemEnabled);
        }

        /// <summary>
        /// Adds a request to create a new scene item using a source.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene to create the new item in</param>
        /// <param name="sourceName">Name of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddCreateSceneItemRequest(Guid sceneUuid, string sourceName, bool sceneItemEnabled = true)
        {
            this.AddCreateSceneItemRequest(null, null, sceneUuid, sourceName, null, sceneItemEnabled);
        }

        /// <summary>
        /// Adds a request to create a new scene item using a source.
        /// </summary>
        /// <param name="sceneName">Name of the scene to create the new item in</param>
        /// <param name="sourceUuid">UUID of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddCreateSceneItemRequest(string sceneName, Guid sourceUuid, bool sceneItemEnabled = true)
        {
            this.AddCreateSceneItemRequest(sceneName, null, null, null, sourceUuid, sceneItemEnabled);
        }

        /// <summary>
        /// Adds a request to create a new scene item using a source on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene to create the new item in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sourceUuid">UUID of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddCreateSceneItemRequest(string sceneName, Guid canvasUuid, Guid sourceUuid, bool sceneItemEnabled = true)
        {
            this.AddCreateSceneItemRequest(sceneName, canvasUuid, null, null, sourceUuid, sceneItemEnabled);
        }

        /// <summary>
        /// Adds a request to create a new scene item using a source.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene to create the new item in</param>
        /// <param name="sourceUuid">UUID of the source to add to the scene</param>
        /// <param name="sceneItemEnabled">Enable state to apply to the scene item on creation</param>
        /// <returns>Numeric ID of the scene item</returns>
        public void AddCreateSceneItemRequest(Guid sceneUuid, Guid sourceUuid, bool sceneItemEnabled = true)
        {
            this.AddCreateSceneItemRequest(null, null, sceneUuid, null, sourceUuid, sceneItemEnabled);
        }

        private void AddCreateSceneItemRequest(string? sceneName, Guid? canvasUuid, Guid? sceneUuid, string? sourceName, Guid? sourceUuid, bool sceneItemEnabled)
        {
            this._requests.Add(new(new { canvasUuid, sceneName, sceneUuid, sourceName, sourceUuid, sceneItemEnabled }));
        }

        /// <summary>
        /// Adds a request to remove a scene item from a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public void AddRemoveSceneItemRequest(string sceneName, int sceneItemId)
        {
            this.AddRemoveSceneItemRequest(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to remove a scene item from a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public void AddRemoveSceneItemRequest(string sceneName, Guid canvasUuid, int sceneItemId)
        {
            this.AddRemoveSceneItemRequest(sceneName, (Guid?)canvasUuid, sceneItemId);
        }

        /// <summary>
        /// Adds a request to remove a scene item from a scene.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        public void AddRemoveSceneItemRequest(Guid sceneUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId }));
        }

        private void AddRemoveSceneItemRequest(string sceneName, Guid? canvasUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId }));
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
            this.AddDuplicateSceneItemRequest(sceneName, null, null, sceneItemId, destinationSceneName, null);
        }

        /// <summary>
        /// Adds a request to duplicate a scene item on a canvas, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneName">Name of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public void AddDuplicateSceneItemRequest(string sceneName, Guid canvasUuid, int sceneItemId, string? destinationSceneName = null)
        {
            this.AddDuplicateSceneItemRequest(sceneName, canvasUuid, null, sceneItemId, destinationSceneName, null);
        }

        /// <summary>
        /// Adds a request to duplicate a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneName">Name of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public void AddDuplicateSceneItemRequest(Guid sceneUuid, int sceneItemId, string? destinationSceneName = null)
        {
            this.AddDuplicateSceneItemRequest(null, null, sceneUuid, sceneItemId, destinationSceneName, null);
        }

        /// <summary>
        /// Adds a request to duplicate a scene item, copying all transform and crop info.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="destinationSceneUuid">UUID of the scene to create the duplicated item in</param>
        /// <returns>Numeric ID of the duplicated scene item</returns>
        public void AddDuplicateSceneItemRequest(Guid sceneUuid, int sceneItemId, Guid destinationSceneUuid)
        {
            this.AddDuplicateSceneItemRequest(null, null, sceneUuid, sceneItemId, null, destinationSceneUuid);
        }

        private void AddDuplicateSceneItemRequest(string? sceneName, Guid? canvasUuid, Guid? sceneUuid, int sceneItemId, string? destinationSceneName, Guid? destinationSceneUuid)
        {
            this._requests.Add(new(new { canvasUuid, sceneName, sceneUuid, sceneItemId, destinationSceneName, destinationSceneUuid }));
        }

        /// <summary>
        /// Adds a request to get the source associated with a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>A <see cref="OBSStudioClient.Responses.SceneItemSourceResponse"/></returns>
        public void AddGetSceneItemSourceRequest(string sceneName, int sceneItemId)
        {
            this.AddGetSceneItemSourceRequest(sceneName, null, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the source associated with a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>A <see cref="OBSStudioClient.Responses.SceneItemSourceResponse"/></returns>
        public void AddGetSceneItemSourceRequest(string sceneName, Guid canvasUuid, int sceneItemId)
        {
            this.AddGetSceneItemSourceRequest(sceneName, canvasUuid, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the source associated with a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>A <see cref="OBSStudioClient.Responses.SceneItemSourceResponse"/></returns>
        public void AddGetSceneItemSourceRequest(Guid sceneUuid, int sceneItemId)
        {
            this.AddGetSceneItemSourceRequest(null, null, sceneUuid, sceneItemId);
        }

        private void AddGetSceneItemSourceRequest(string? sceneName, Guid? canvasUuid, Guid? sceneUuid, int sceneItemId)
        {
            this._requests.Add(new(new { canvasUuid, sceneName, sceneUuid, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to get the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public void AddGetSceneItemTransformRequest(string sceneName, int sceneItemId)
        {
            this.AddGetSceneItemTransformRequest(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the transform and crop info of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public void AddGetSceneItemTransformRequest(string sceneName, Guid canvasUuid, int sceneItemId)
        {
            this.AddGetSceneItemTransformRequest(sceneName, (Guid?)canvasUuid, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Object containing scene item transform info</returns>
        public void AddGetSceneItemTransformRequest(Guid sceneUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId }));
        }

        private void AddGetSceneItemTransformRequest(string sceneName, Guid? canvasUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public void AddSetSceneItemTransformRequest(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this.AddSetSceneItemTransformRequest(sceneName, null, sceneItemId, sceneItemTransform);
        }

        /// <summary>
        /// Adds a request to set the transform and crop info of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public void AddSetSceneItemTransformRequest(string sceneName, Guid canvasUuid, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this.AddSetSceneItemTransformRequest(sceneName, (Guid?)canvasUuid, sceneItemId, sceneItemTransform);
        }

        /// <summary>
        /// Adds a request to set the transform and crop info of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemTransform">Object containing scene item transform info to update</param>
        public void AddSetSceneItemTransformRequest(Guid sceneUuid, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId, sceneItemTransform }));
        }

        private void AddSetSceneItemTransformRequest(string sceneName, Guid? canvasUuid, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId, sceneItemTransform }));
        }

        /// <summary>
        /// Adds a request to get the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public void AddGetSceneItemEnabledRequest(string sceneName, int sceneItemId)
        {
            this.AddGetSceneItemEnabledRequest(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the enable state of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public void AddGetSceneItemEnabledRequest(string sceneName, Guid canvasUuid, int sceneItemId)
        {
            this.AddGetSceneItemEnabledRequest(sceneName, (Guid?)canvasUuid, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the enable state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is enabled. true for enabled, false for disabled</returns>
        public void AddGetSceneItemEnabledRequest(Guid sceneUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId }));
        }

        private void AddGetSceneItemEnabledRequest(string sceneName, Guid? canvasUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the enable state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">	New enable state of the scene item</param>
        public void AddSetSceneItemEnabledRequest(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            this.AddSetSceneItemEnabledRequest(sceneName, null, sceneItemId, sceneItemEnabled);
        }

        /// <summary>
        /// Adds a request to set the enable state of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">New enable state of the scene item</param>
        public void AddSetSceneItemEnabledRequest(string sceneName, Guid canvasUuid, int sceneItemId, bool sceneItemEnabled)
        {
            this.AddSetSceneItemEnabledRequest(sceneName, (Guid?)canvasUuid, sceneItemId, sceneItemEnabled);
        }

        /// <summary>
        /// Adds a request to set the enable state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemEnabled">New enable state of the scene item</param>
        public void AddSetSceneItemEnabledRequest(Guid sceneUuid, int sceneItemId, bool sceneItemEnabled)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId, sceneItemEnabled }));
        }

        private void AddSetSceneItemEnabledRequest(string sceneName, Guid? canvasUuid, int sceneItemId, bool sceneItemEnabled)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId, sceneItemEnabled }));
        }

        /// <summary>
        /// Adds a request to get the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public void AddGetSceneItemLockedRequest(string sceneName, int sceneItemId)
        {
            this.AddGetSceneItemLockedRequest(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the lock state of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public void AddGetSceneItemLockedRequest(string sceneName, Guid canvasUuid, int sceneItemId)
        {
            this.AddGetSceneItemLockedRequest(sceneName, (Guid?)canvasUuid, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the lock state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Whether the scene item is locked. true for locked, false for unlocked</returns>
        public void AddGetSceneItemLockedRequest(Guid sceneUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId }));
        }

        private void AddGetSceneItemLockedRequest(string sceneName, Guid? canvasUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the lock state of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public void AddSetSceneItemLockedRequest(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            this.AddSetSceneItemLockedRequest(sceneName, null, sceneItemId, sceneItemLocked);
        }

        /// <summary>
        /// Adds a request to set the lock state of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public void AddSetSceneItemLockedRequest(string sceneName, Guid canvasUuid, int sceneItemId, bool sceneItemLocked)
        {
            this.AddSetSceneItemLockedRequest(sceneName, (Guid?)canvasUuid, sceneItemId, sceneItemLocked);
        }

        /// <summary>
        /// Adds a request to set the lock state of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemLocked">New lock state of the scene item</param>
        public void AddSetSceneItemLockedRequest(Guid sceneUuid, int sceneItemId, bool sceneItemLocked)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId, sceneItemLocked }));
        }

        private void AddSetSceneItemLockedRequest(string sceneName, Guid? canvasUuid, int sceneItemId, bool sceneItemLocked)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId, sceneItemLocked }));
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
            this.AddGetSceneItemIndexRequest(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the index position of a scene item in a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns></returns>
        /// <remarks>
        /// An index of 0 is at the bottom of the source list in the UI.
        /// Scenes and Groups
        /// </remarks>
        public void AddGetSceneItemIndexRequest(string sceneName, Guid canvasUuid, int sceneItemId)
        {
            this.AddGetSceneItemIndexRequest(sceneName, (Guid?)canvasUuid, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns></returns>
        /// <remarks>
        /// An index of 0 is at the bottom of the source list in the UI.
        /// Scenes and Groups
        /// </remarks>
        public void AddGetSceneItemIndexRequest(Guid sceneUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId }));
        }

        private void AddGetSceneItemIndexRequest(string sceneName, Guid? canvasUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public void AddSetSceneItemIndexRequest(string sceneName, int sceneItemId, int sceneItemIndex)
        {
            this.AddSetSceneItemIndexRequest(sceneName, null, sceneItemId, sceneItemIndex);
        }

        /// <summary>
        /// Adds a request to set the index position of a scene item in a scene on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public void AddSetSceneItemIndexRequest(string sceneName, Guid canvasUuid, int sceneItemId, int sceneItemIndex)
        {
            this.AddSetSceneItemIndexRequest(sceneName, (Guid?)canvasUuid, sceneItemId, sceneItemIndex);
        }

        /// <summary>
        /// Adds a request to set the index position of a scene item in a scene.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemIndex">New index position of the scene item (>= 0)</param>
        public void AddSetSceneItemIndexRequest(Guid sceneUuid, int sceneItemId, int sceneItemIndex)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId, sceneItemIndex }));
        }

        private void AddSetSceneItemIndexRequest(string sceneName, Guid? canvasUuid, int sceneItemId, int sceneItemIndex)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId, sceneItemIndex }));
        }

        /// <summary>
        /// Adds a request to get the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public void AddGetSceneItemBlendModeRequest(string sceneName, int sceneItemId)
        {
            this.AddGetSceneItemBlendModeRequest(sceneName, null, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the blend mode of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public void AddGetSceneItemBlendModeRequest(string sceneName, Guid canvasUuid, int sceneItemId)
        {
            this.AddGetSceneItemBlendModeRequest(sceneName, (Guid?)canvasUuid, sceneItemId);
        }

        /// <summary>
        /// Adds a request to get the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <returns>Current blend mode</returns>
        public void AddGetSceneItemBlendModeRequest(Guid sceneUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId }));
        }

        private void AddGetSceneItemBlendModeRequest(string sceneName, Guid? canvasUuid, int sceneItemId)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId }));
        }

        /// <summary>
        /// Adds a request to set the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public void AddSetSceneItemBlendModeRequest(string sceneName, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            this.AddSetSceneItemBlendModeRequest(sceneName, null, sceneItemId, sceneItemBlendMode);
        }

        /// <summary>
        /// Adds a request to set the blend mode of a scene item on a canvas.
        /// </summary>
        /// <param name="sceneName">Name of the scene the item is in</param>
        /// <param name="canvasUuid">UUID of the canvas the scene is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public void AddSetSceneItemBlendModeRequest(string sceneName, Guid canvasUuid, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            this.AddSetSceneItemBlendModeRequest(sceneName, (Guid?)canvasUuid, sceneItemId, sceneItemBlendMode);
        }

        /// <summary>
        /// Adds a request to set the blend mode of a scene item.
        /// </summary>
        /// <param name="sceneUuid">UUID of the scene the item is in</param>
        /// <param name="sceneItemId">Numeric ID of the scene item (>= 0)</param>
        /// <param name="sceneItemBlendMode">New blend mode</param>
        public void AddSetSceneItemBlendModeRequest(Guid sceneUuid, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            this._requests.Add(new(new { sceneUuid, sceneItemId, sceneItemBlendMode }));
        }

        private void AddSetSceneItemBlendModeRequest(string sceneName, Guid? canvasUuid, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            this._requests.Add(new(new { sceneName, canvasUuid, sceneItemId, sceneItemBlendMode }));
        }
    }
}
