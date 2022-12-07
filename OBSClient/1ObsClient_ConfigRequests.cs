namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the value of a "slot" from the selected persistent data realm.
        /// </summary>
        /// <param name="realm">The data realm to select. OBS_WEBSOCKET_DATA_REALM_GLOBAL or OBS_WEBSOCKET_DATA_REALM_PROFILE</param>
        /// <param name="slotName">The name of the slot to retrieve data from</param>
        /// <returns>Value associated with the slot. null if not set</returns>
        public object? GetPersistentData(Realm realm, string slotName)
        {
            return this.SendRequest<SlotValueResponseData>(new { realm, slotName }).SlotValue;
        }

        /// <summary>
        /// Sets the value of a "slot" from the selected persistent data realm.
        /// </summary>
        /// <param name="realm">The data realm to select. OBS_WEBSOCKET_DATA_REALM_GLOBAL or OBS_WEBSOCKET_DATA_REALM_PROFILE</param>
        /// <param name="slotName">The name of the slot to retrieve data from</param>
        /// <param name="slotValue">The value to apply to the slot</param>
        public void SetPersistentData(Realm realm, string slotName, object slotValue)
        {
            this.SendRequest(new { realm, slotName, slotValue });
        }

        /// <summary>
        /// Gets an array of all scene collections
        /// </summary>
        /// <returns>A <see cref="SceneCollectionListResponseData"/></returns>
        public SceneCollectionListResponseData GetSceneCollectionList()
        {
            return this.SendRequest<SceneCollectionListResponseData>();
        }

        /// <summary>
        /// Switches to a scene collection.
        /// </summary>
        /// <param name="sceneCollectionName">The name of the current scene collection</param>
        /// <remarks>
        /// Note: This will block until the collection has finished changing.
        /// </remarks>
        public void SetCurrentSceneCollection(string sceneCollectionName)
        {
            this.SendRequest(new { sceneCollectionName });
        }

        /// <summary>
        /// Creates a new scene collection, switching to it in the process.
        /// </summary>
        /// <param name="sceneCollectionName">Name for the new scene collection</param>
        /// <remarks>
        /// Note: This will block until the collection has finished changing.
        /// </remarks>
        public void CreateSceneCollection(string sceneCollectionName)
        {
            this.SendRequest(new { sceneCollectionName });
        }

        /// <summary>
        /// Gets an array of all profiles
        /// </summary>
        /// <returns>A <see cref="ProfileListResponseData"/></returns>
        public ProfileListResponseData GetProfileList()
        {
            return this.SendRequest<ProfileListResponseData>();
        }

        /// <summary>
        /// Switches to a profile.
        /// </summary>
        /// <param name="profileName">Name of the profile to switch to</param>
        public void SetCurrentProfile(string profileName)
        {
            this.SendRequest(new { profileName });
        }

        /// <summary>
        /// Creates a new profile, switching to it in the process
        /// </summary>
        /// <param name="profileName">Name for the new profile</param>
        public void CreateProfile(string profileName)
        {
            this.SendRequest(new { profileName });
        }

        /// <summary>
        /// Removes a profile. If the current profile is chosen, it will change to a different profile first.
        /// </summary>
        /// <param name="profileName">Name of the profile to remove</param>
        public void RemoveProfile(string profileName)
        {
            this.SendRequest(new { profileName });
        }

        /// <summary>
        /// Gets a parameter from the current profile's configuration.
        /// </summary>
        /// <param name="parameterCategory">Category of the parameter to get</param>
        /// <param name="parameterName">Name of the parameter to get</param>
        /// <returns>A <see cref="ProfileParameterResponseData"/></returns>
        public ProfileParameterResponseData GetProfileParameter(string parameterCategory, string parameterName)
        {
            return this.SendRequest<ProfileParameterResponseData>(new { parameterCategory, parameterName });
        }

        /// <summary>
        /// Sets the value of a parameter in the current profile's configuration.
        /// </summary>
        /// <param name="parameterCategory">Category of the parameter to set</param>
        /// <param name="parameterName">Name of the parameter to set</param>
        /// <param name="parameterValue">Value of the parameter to set. Use null to delete</param>
        public void SetProfileParameter(string parameterCategory, string parameterName, string? parameterValue)
        {
            this.SendRequest(new { parameterCategory, parameterName, parameterValue });
        }

        /// <summary>
        /// Gets the current video settings.
        /// </summary>
        /// <returns>A <see cref="VideoSettingsResponseData"/></returns>
        public VideoSettingsResponseData GetVideoSettings()
        {
            return this.SendRequest<VideoSettingsResponseData>();
        }

        /// <summary>
        /// Sets the current video settings.
        /// </summary>
        /// <param name="fpsNumerator">Numerator of the fractional FPS value (>= 1)</param>
        /// <param name="fpsDenominator">Denominator of the fractional FPS value (>= 1)</param>
        /// <param name="baseWidth">Width of the base (canvas) resolution in pixels (>= 1, <= 4096)</param>
        /// <param name="baseHeight">Height of the base (canvas) resolution in pixels (>= 1, <= 4096)</param>
        /// <param name="outputWidth">Width of the output resolution in pixels (>= 1, <= 4096)</param>
        /// <param name="outputHeight">Height of the output resolution in pixels (>= 1, <= 4096)</param>
        public void SetVideoSettings(float? fpsNumerator, float? fpsDenominator, int? baseWidth, int? baseHeight, int? outputWidth, int? outputHeight)
        {
            this.SendRequest(new { fpsNumerator, fpsDenominator, baseWidth, baseHeight, outputWidth, outputHeight });
        }

        /// <summary>
        /// Gets the current stream service settings (stream destination).
        /// </summary>
        /// <returns>A <see cref="StreamServiceSettingsResponseData"/></returns>
        public StreamServiceSettingsResponseData GetStreamServiceSettings()
        {
            return this.SendRequest<StreamServiceSettingsResponseData>();
        }

        /// <summary>
        /// Sets the current stream service settings (stream destination).
        /// </summary>
        /// <param name="streamServiceType">Type of stream service to apply. Example: rtmp_common or rtmp_custom</param>
        /// <param name="streamServiceSettings">Settings to apply to the service</param>
        /// <remarks>
        /// Note: Simple RTMP settings can be set with type rtmp_custom and the settings fields server and key.
        /// </remarks>
        public void SetStreamServiceSettings(string streamServiceType, object streamServiceSettings)
        {
            this.SendRequest(new { streamServiceType, streamServiceSettings });
        }

        /// <summary>
        /// Gets the current directory that the record output is set to.
        /// </summary>
        /// <returns>Output directory</returns>
        public string GetRecordDirectory()
        {
            return this.SendRequest<RecordDirectoryResponseData>().RecordDirectory;
        }
    }
}
