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
        public async Task<object?> GetPersistentData(Realm realm, string slotName)
        {
            return (await this.SendRequestAsync<SlotValueResponse>(new { realm, slotName })).SlotValue;
        }

        /// <summary>
        /// Sets the value of a "slot" from the selected persistent data realm.
        /// </summary>
        /// <param name="realm">The data realm to select. OBS_WEBSOCKET_DATA_REALM_GLOBAL or OBS_WEBSOCKET_DATA_REALM_PROFILE</param>
        /// <param name="slotName">The name of the slot to retrieve data from</param>
        /// <param name="slotValue">The value to apply to the slot</param>
        public async Task SetPersistentData(Realm realm, string slotName, object slotValue)
        {
            await this.SendRequestAsync(new { realm, slotName, slotValue });
        }

        /// <summary>
        /// Gets an array of all scene collections
        /// </summary>
        /// <returns>A <see cref="SceneCollectionListResponse"/></returns>
        public async Task<SceneCollectionListResponse> GetSceneCollectionList()
        {
            return await this.SendRequestAsync<SceneCollectionListResponse>();
        }

        /// <summary>
        /// Switches to a scene collection.
        /// </summary>
        /// <param name="sceneCollectionName">The name of the current scene collection</param>
        /// <remarks>
        /// Note: This will block until the collection has finished changing.
        /// </remarks>
        public async Task SetCurrentSceneCollection(string sceneCollectionName)
        {
            await this.SendRequestAsync(new { sceneCollectionName });
        }

        /// <summary>
        /// Creates a new scene collection, switching to it in the process.
        /// </summary>
        /// <param name="sceneCollectionName">Name for the new scene collection</param>
        /// <remarks>
        /// Note: This will block until the collection has finished changing.
        /// </remarks>
        public async Task CreateSceneCollection(string sceneCollectionName)
        {
            await this.SendRequestAsync(new { sceneCollectionName });
        }

        /// <summary>
        /// Gets an array of all profiles
        /// </summary>
        /// <returns>A <see cref="ProfileListResponse"/></returns>
        public async Task<ProfileListResponse> GetProfileList()
        {
            return await this.SendRequestAsync<ProfileListResponse>();
        }

        /// <summary>
        /// Switches to a profile.
        /// </summary>
        /// <param name="profileName">Name of the profile to switch to</param>
        public async Task SetCurrentProfile(string profileName)
        {
            await this.SendRequestAsync(new { profileName });
        }

        /// <summary>
        /// Creates a new profile, switching to it in the process
        /// </summary>
        /// <param name="profileName">Name for the new profile</param>
        public async Task CreateProfile(string profileName)
        {
            await this.SendRequestAsync(new { profileName });
        }

        /// <summary>
        /// Removes a profile. If the current profile is chosen, it will change to a different profile first.
        /// </summary>
        /// <param name="profileName">Name of the profile to remove</param>
        public async Task RemoveProfile(string profileName)
        {
            await this.SendRequestAsync(new { profileName });
        }

        /// <summary>
        /// Gets a parameter from the current profile's configuration.
        /// </summary>
        /// <param name="parameterCategory">Category of the parameter to get</param>
        /// <param name="parameterName">Name of the parameter to get</param>
        /// <returns>A <see cref="ProfileParameterResponse"/></returns>
        public async Task<ProfileParameterResponse> GetProfileParameter(string parameterCategory, string parameterName)
        {
            return await this.SendRequestAsync<ProfileParameterResponse>(new { parameterCategory, parameterName });
        }

        /// <summary>
        /// Sets the value of a parameter in the current profile's configuration.
        /// </summary>
        /// <param name="parameterCategory">Category of the parameter to set</param>
        /// <param name="parameterName">Name of the parameter to set</param>
        /// <param name="parameterValue">Value of the parameter to set. Use null to delete</param>
        public async Task SetProfileParameter(string parameterCategory, string parameterName, string? parameterValue)
        {
            await this.SendRequestAsync(new { parameterCategory, parameterName, parameterValue });
        }

        /// <summary>
        /// Gets the current video settings.
        /// </summary>
        /// <returns>A <see cref="VideoSettingsResponse"/></returns>
        public async Task<VideoSettingsResponse> GetVideoSettings()
        {
            return await this.SendRequestAsync<VideoSettingsResponse>();
        }

        /// <summary>
        /// Sets the current video settings.
        /// </summary>
        /// <param name="fpsNumerator">Numerator of the fractional FPS value.</param>
        /// <param name="fpsDenominator">Denominator of the fractional FPS value.</param>
        /// <param name="baseWidth">Width of the base (canvas) resolution in pixels.</param>
        /// <param name="baseHeight">Height of the base (canvas) resolution in pixels.</param>
        /// <param name="outputWidth">Width of the output resolution in pixels.</param>
        /// <param name="outputHeight">Height of the output resolution in pixels.</param>
        public async Task SetVideoSettings(float? fpsNumerator, float? fpsDenominator, int? baseWidth, int? baseHeight, int? outputWidth, int? outputHeight)
        {
            await this.SendRequestAsync(new { fpsNumerator, fpsDenominator, baseWidth, baseHeight, outputWidth, outputHeight });
        }

        /// <summary>
        /// Gets the current stream service settings (stream destination).
        /// </summary>
        /// <returns>A <see cref="StreamServiceSettingsResponse"/></returns>
        public async Task<StreamServiceSettingsResponse> GetStreamServiceSettings()
        {
            return await this.SendRequestAsync<StreamServiceSettingsResponse>();
        }

        /// <summary>
        /// Sets the current stream service settings (stream destination).
        /// </summary>
        /// <param name="streamServiceType">Type of stream service to apply. Example: rtmp_common or rtmp_custom</param>
        /// <param name="streamServiceSettings">Settings to apply to the service</param>
        /// <remarks>
        /// Note: Simple RTMP settings can be set with type rtmp_custom and the settings fields server and key.
        /// </remarks>
        public async Task SetStreamServiceSettings(string streamServiceType, object streamServiceSettings)
        {
            await this.SendRequestAsync(new { streamServiceType, streamServiceSettings });
        }

        /// <summary>
        /// Gets the current directory that the record output is set to.
        /// </summary>
        /// <returns>Output directory</returns>
        public async Task<string> GetRecordDirectory()
        {
            return (await this.SendRequestAsync<RecordDirectoryResponse>()).RecordDirectory;
        }

        /// <summary>
        /// Sets the current directory that the record output writes files to.
        /// </summary>
        /// <param name="recordDirectory">Output directory</param>
        public async Task SetRecordDirectory(string recordDirectory)
        {
            await this.SendRequestAsync(new { recordDirectory });
        }
    }
}
