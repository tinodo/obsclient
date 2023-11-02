namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Responses;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to gets the value of a "slot" from the selected persistent data realm.
        /// </summary>
        /// <param name="realm">The data realm to select. OBS_WEBSOCKET_DATA_REALM_GLOBAL or OBS_WEBSOCKET_DATA_REALM_PROFILE</param>
        /// <param name="slotName">The name of the slot to retrieve data from</param>
        /// <returns>Value associated with the slot. null if not set</returns>
        public void AddGetPersistentDataRequest(Realm realm, string slotName)
        {
            this._requests.Add(new(new { realm, slotName }));
        }

        /// <summary>
        /// Adds a request to set the value of a "slot" from the selected persistent data realm.
        /// </summary>
        /// <param name="realm">The data realm to select. OBS_WEBSOCKET_DATA_REALM_GLOBAL or OBS_WEBSOCKET_DATA_REALM_PROFILE</param>
        /// <param name="slotName">The name of the slot to retrieve data from</param>
        /// <param name="slotValue">The value to apply to the slot</param>
        public void AddSetPersistentDataRequest(Realm realm, string slotName, object slotValue)
        {
            this._requests.Add(new(new { realm, slotName, slotValue }));
        }

        /// <summary>
        /// Adds a request to get an array of all scene collections
        /// </summary>
        /// <returns>A <see cref="SceneCollectionListResponse"/></returns>
        public void AddGetSceneCollectionListRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to switch to a scene collection.
        /// </summary>
        /// <param name="sceneCollectionName">The name of the current scene collection</param>
        /// <remarks>
        /// Note: This will block until the collection has finished changing.
        /// </remarks>
        public void AddSetCurrentSceneCollectionRequest(string sceneCollectionName)
        {
            this._requests.Add(new(new { sceneCollectionName }));
        }

        /// <summary>
        /// Adds a request to create a new scene collection, switching to it in the process.
        /// </summary>
        /// <param name="sceneCollectionName">Name for the new scene collection</param>
        /// <remarks>
        /// Note: This will block until the collection has finished changing.
        /// </remarks>
        public void AddCreateSceneCollectionRequest(string sceneCollectionName)
        {
            this._requests.Add(new(new { sceneCollectionName }));
        }

        /// <summary>
        /// Adds a request to get an array of all profiles
        /// </summary>
        /// <returns>A <see cref="ProfileListResponse"/></returns>
        public void AddGetProfileListRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to switch to a profile.
        /// </summary>
        /// <param name="profileName">Name of the profile to switch to</param>
        public void AddSetCurrentProfileRequest(string profileName)
        {
            this._requests.Add(new(new { profileName }));
        }

        /// <summary>
        /// Adds a request to creates a new profile, switching to it in the process
        /// </summary>
        /// <param name="profileName">Name for the new profile</param>
        public void AddCreateProfileRequest(string profileName)
        {
            this._requests.Add(new(new { profileName }));
        }

        /// <summary>
        /// Adds a request to removes a profile. If the current profile is chosen, it will change to a different profile first.
        /// </summary>
        /// <param name="profileName">Name of the profile to remove</param>
        public void AddRemoveProfileRequest(string profileName)
        {
            this._requests.Add(new(new { profileName }));
        }

        /// <summary>
        /// Adds a request to get a parameter from the current profile's configuration.
        /// </summary>
        /// <param name="parameterCategory">Category of the parameter to get</param>
        /// <param name="parameterName">Name of the parameter to get</param>
        /// <returns>A <see cref="ProfileParameterResponse"/></returns>
        public void AddGetProfileParameterRequest(string parameterCategory, string parameterName)
        {
            this._requests.Add(new(new { parameterCategory, parameterName }));
        }

        /// <summary>
        /// Adds a request to get the value of a parameter in the current profile's configuration.
        /// </summary>
        /// <param name="parameterCategory">Category of the parameter to set</param>
        /// <param name="parameterName">Name of the parameter to set</param>
        /// <param name="parameterValue">Value of the parameter to set. Use null to delete</param>
        public void AddSetProfileParameterRequest(string parameterCategory, string parameterName, string? parameterValue)
        {
            this._requests.Add(new(new { parameterCategory, parameterName, parameterValue }));
        }

        /// <summary>
        /// Adds a request to get the current video settings.
        /// </summary>
        /// <returns>A <see cref="VideoSettingsResponse"/></returns>
        public void AddGetVideoSettingsRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to set the current video settings.
        /// </summary>
        /// <param name="fpsNumerator">Numerator of the fractional FPS value.</param>
        /// <param name="fpsDenominator">Denominator of the fractional FPS value.</param>
        /// <param name="baseWidth">Width of the base (canvas) resolution in pixels.</param>
        /// <param name="baseHeight">Height of the base (canvas) resolution in pixels.</param>
        /// <param name="outputWidth">Width of the output resolution in pixels.</param>
        /// <param name="outputHeight">Height of the output resolution in pixels.</param>
        public void AddSetVideoSettingsRequest(float? fpsNumerator, float? fpsDenominator, int? baseWidth, int? baseHeight, int? outputWidth, int? outputHeight)
        {
            this._requests.Add(new(new { fpsNumerator, fpsDenominator, baseWidth, baseHeight, outputWidth, outputHeight }));
        }

        /// <summary>
        /// Adds a request to get the current stream service settings (stream destination).
        /// </summary>
        /// <returns>A <see cref="StreamServiceSettingsResponse"/></returns>
        public void AddGetStreamServiceSettingsRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to set the current stream service settings (stream destination).
        /// </summary>
        /// <param name="streamServiceType">Type of stream service to apply. Example: rtmp_common or rtmp_custom</param>
        /// <param name="streamServiceSettings">Settings to apply to the service</param>
        /// <remarks>
        /// Note: Simple RTMP settings can be set with type rtmp_custom and the settings fields server and key.
        /// </remarks>
        public void AddSetStreamServiceSettingsRequest(string streamServiceType, object streamServiceSettings)
        {
            this._requests.Add(new(new { streamServiceType, streamServiceSettings }));
        }

        /// <summary>
        /// Adds a request to get the current directory that the record output is set to.
        /// </summary>
        /// <returns>Output directory</returns>
        public void AddGetRecordDirectoryRequest()
        {
            this._requests.Add(new());
        }
    }
}
