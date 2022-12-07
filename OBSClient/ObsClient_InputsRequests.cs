namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets an array of all inputs in OBS.
        /// </summary>
        /// <param name="inputKind">Restrict the array to only inputs of the specified kind</param>
        /// <returns>An array of <see cref="Input"/></returns>
        public async Task<Input[]> GetInputList(string? inputKind = null)
        {
            return (await this.SendRequestAsync<InputsResponseData>(new { inputKind })).Inputs;
        }

        /// <summary>
        /// Gets an array of all available input kinds in OBS.
        /// </summary>
        /// <param name="unversioned">True == Return all kinds as unversioned, False == Return with version suffixes (if available)</param>
        /// <returns>Array of input kinds</returns>
        public async Task<string[]> GetInputKindList(bool unversioned = false)
        {
            return (await this.SendRequestAsync<InputKindsResponseData>(new { unversioned })).InputKinds;
        }

        /// <summary>
        /// Gets the names of all special inputs.
        /// </summary>
        /// <returns>A <see cref="SpecialInputsResponseData"/></returns>
        public async Task<SpecialInputsResponseData> GetSpecialInputs()
        {
            return await this.SendRequestAsync<SpecialInputsResponseData>();
        }

        /// <summary>
        /// Creates a new input, adding it as a scene item to the specified scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene to add the input to as a scene item</param>
        /// <param name="inputName">Name of the new input to created</param>
        /// <param name="inputKind">The kind of input to be created</param>
        /// <param name="inputSettings">Settings object to initialize the input with</param>
        /// <param name="sceneItemEnabled">Whether to set the created scene item to enabled or disabled</param>
        /// <returns>ID of the newly created scene item</returns>
        public async Task<int> CreateInput(string sceneName, string inputName, string inputKind, Input? inputSettings, bool sceneItemEnabled = true)
        {
            return (await this.SendRequestAsync<SceneItemIdResponseData>(new { sceneName, inputName, inputKind, inputSettings, sceneItemEnabled })).SceneItemId;
        }

        /// <summary>
        /// Removes an existing input.
        /// </summary>
        /// <param name="inputName">Name of the input to remove</param>
        /// <remarks>
        /// Note: Will immediately remove all associated scene items.
        /// </remarks>
        public async Task RemoveInput(string inputName)
        {
            await this.SendRequestAsync(new { inputName });
        }

        /// <summary>
        /// Sets the name of an input (rename).
        /// </summary>
        /// <param name="inputName">Current input name</param>
        /// <param name="newInputName">New name for the input</param>
        public async Task SetInputName(string inputName, string newInputName) 
        {
            await this.SendRequestAsync(new { inputName, newInputName });
        }

        /// <summary>
        /// Gets the default settings for an input kind.
        /// </summary>
        /// <param name="inputKind">Input kind to get the default settings for</param>
        /// <returns>A <see cref="Dictionary<string, object>"/> of default settings for the input kind</returns>
        public async Task<Dictionary<string, object>> GetInputDefaultSettings(string inputKind)
        {
            return (await this.SendRequestAsync<DefaultInputSettingsResponseData>(new{ inputKind })).DefaultInputSettings;
        }

        /// <summary>
        /// Gets the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the settings of</param>
        /// <returns>A <see cref="InputSettingsResponseData"/></returns>
        /// <remarks>
        /// Note: Does not include defaults. To create the entire settings object, overlay inputSettings over the defaultInputSettings provided by GetInputDefaultSettings.
        /// </remarks>
        public async Task<InputSettingsResponseData> GetInputSettings(string inputName)
        {
            return await this.SendRequestAsync<InputSettingsResponseData>(new { inputName });
        }

        /// <summary>
        /// Sets the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the settings of</param>
        /// <param name="inputSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public async Task SetInputSettings(string inputName, Dictionary<string, object> inputSettings, bool overlay = true)
        {
            await this.SendRequestAsync(new { inputName, inputSettings, overlay });
        }

        /// <summary>
        /// Gets the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of input to get the mute state of</param>
        /// <returns>Whether the input is muted</returns>
        public async Task<bool> GetInputMute(string inputName)
        {
            return (await this.SendRequestAsync<InputMutedResponseData>(new { inputName })).InputMuted;
        }

        /// <summary>
        /// Sets the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the mute state of</param>
        /// <param name="inputMuted">Whether to mute the input or not</param>
        public async Task SetInputMute(string inputName, bool inputMuted)
        {
            await this.SendRequestAsync(new { inputName, inputMuted });
        }

        /// <summary>
        /// Toggles the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to toggle the mute state of</param>
        /// <returns>Whether the input has been muted or unmuted</returns>
        public async Task<bool> ToggleInputMute(string inputName)
        {
            return (await this.SendRequestAsync<InputMutedResponseData>(new { inputName })).InputMuted;
        }

        /// <summary>
        /// Gets the current volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the volume of</param>
        /// <returns>A <see cref="InputVolumeResponseData"/></returns>
        public async Task<InputVolumeResponseData> GetInputVolume(string inputName)
        {
            return await this.SendRequestAsync<InputVolumeResponseData>(new { inputName });
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeMul">Volume setting in mul (>= 0, <= 20)</param>
        public async Task SetInputVolumeMul(string inputName, float inputVolumeMul)
        {
            await this.SetInputVolume(inputName, inputVolumeMul, null);
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeDb">Volume setting in dB (>= -100, <= 26)</param>
        public async Task SetInputVolumeDb(string inputName, float inputVolumeDb)
        {
            await this.SetInputVolume(inputName, null, inputVolumeDb);
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeMul">Volume setting in mul (>= 0, <= 20)</param>
        /// <param name="inputVolumeDb">Volume setting in dB (>= -100, <= 26)</param>
        /// <exception cref="ArgumentException"></exception>
        private async Task SetInputVolume(string inputName, float? inputVolumeMul, float? inputVolumeDb)
        {
            if ((inputVolumeMul.HasValue && inputVolumeDb.HasValue) || (!inputVolumeMul.HasValue && !inputVolumeDb.HasValue))
            {
                throw new ArgumentException($"Specify either {nameof(inputVolumeMul)} or {nameof(inputVolumeDb)}");
            }

            await this.SendRequestAsync(new { inputName, inputVolumeMul, inputVolumeDb });
        }

        /// <summary>
        /// Gets the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio balance of</param>
        /// <returns>Audio balance value from 0.0-1.0</returns>
        public async Task<float> GetInputAudioBalance(string inputName)
        {
            return (await this.SendRequestAsync<InputAudioBalanceResponseData>(new { inputName })).InputAudioBalance;
        }

        /// <summary>
        /// Sets the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio balance of</param>
        /// <param name="inputAudioBalance">New audio balance value (>= 0.0, <= 1.0)</param>
        public async Task SetInputAudioBalance(string inputName, float inputAudioBalance)
        {
            await this.SendRequestAsync(new { inputName, inputAudioBalance });
        }

        /// <summary>
        /// Gets the audio sync offset of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio sync offset of</param>
        /// <returns>Audio sync offset in milliseconds</returns>
        /// <remarks>
        /// Note: The audio sync offset can be negative too!
        /// </remarks>
        public async Task<int> GetInputAudioSyncOffset(string inputName)
        {
            return (await this.SendRequestAsync<InputAudioSyncOffsetResponseData>(new { inputName })).InputAudioSyncOffset;
        }

        /// <summary>
        /// Sets the audio sync offset of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio sync offset of</param>
        /// <param name="inputAudioSyncOffset">New audio sync offset in milliseconds (>= -950, <= 20000)</param>
        public async Task SetInputAudioSyncOffset(string inputName, int inputAudioSyncOffset)
        {
            await this.SendRequestAsync(new { inputName, inputAudioSyncOffset });
        }

        /// <summary>
        /// Gets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio monitor type of</param>
        /// <returns>Audio monitor type</returns>
        public async Task<MonitorType> GetInputAudioMonitorType(string inputName)
        {
            return (await this.SendRequestAsync<MonitorTypeResponseData>(new { inputName })).MonitorType;
        }

        /// <summary>
        /// Sets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio monitor type of</param>
        /// <param name="monitorType">Audio monitor type</param>
        public async Task SetInputAudioMonitorType(string inputName, MonitorType monitorType)
        {
            await this.SendRequestAsync(new { inputName, monitorType });
        }

        /// <summary>
        /// Gets the enable state of all audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <returns>Object of audio tracks and associated enable states</returns>
        public async Task<AudioTracks> GetInputAudioTracks(string inputName)
        {
            return (await this.SendRequestAsync<InputAudioTracksResponseData>(new { inputName })).InputAudioTracks;
        }

        /// <summary>
        /// Sets the enable state of audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="inputAudioTracks">Track settings to apply</param>
        public async Task SetInputAudioTracks(string inputName, AudioTracks inputAudioTracks)
        {
            await this.SendRequestAsync(new { inputName, inputAudioTracks });
        }

        /// <summary>
        /// Gets the items of a list property from an input's properties.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="propertyName">Name of the list property to get the items of</param>
        /// <returns>Array of items in the list property</returns>
        /// <remarks>
        /// Note: Use this in cases where an input provides a dynamic, selectable list of items. For example, display capture, where it provides a list of available displays.
        /// </remarks>
        public async Task<PropertyItem[]> GetInputPropertiesListPropertyItems(string inputName, string propertyName)
        {
            return (await this.SendRequestAsync<PropertyItemsResponseData>(new { inputName, propertyName })).PropertyItems;
        }

        /// <summary>
        /// Presses a button in the properties of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="propertyName">Name of the button property to press</param>
        /// <remarks>
        /// Note: Use this in cases where there is a button in the properties of an input that cannot be accessed in any other way. For example, browser sources, where there is a refresh button.
        /// </remarks>
        public async Task PressInputPropertiesButton(string inputName, string propertyName)
        {
            await this.SendRequestAsync(new { inputName, propertyName });
        }
    }
}
