namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets an array of all inputs in OBS.
        /// </summary>
        /// <param name="inputKind">Restrict the array to only inputs of the specified kind</param>
        /// <returns>An array of <see cref="Input"/></returns>
        public async Task<Input[]> GetInputList(string? inputKind = null)
        {
            return (await this.SendRequestAsync<InputsResponse>(new { inputKind })).Inputs;
        }

        /// <summary>
        /// Gets an array of all available input kinds in OBS.
        /// </summary>
        /// <param name="unversioned">True == Return all kinds as unversioned, False == Return with version suffixes (if available)</param>
        /// <returns>Array of input kinds</returns>
        public async Task<string[]> GetInputKindList(bool unversioned = false)
        {
            return (await this.SendRequestAsync<InputKindsResponse>(new { unversioned })).InputKinds;
        }

        /// <summary>
        /// Gets the names of all special inputs.
        /// </summary>
        /// <returns>A <see cref="SpecialInputsResponse"/></returns>
        public async Task<SpecialInputsResponse> GetSpecialInputs()
        {
            return await this.SendRequestAsync<SpecialInputsResponse>();
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
        public async Task<CreateInputResponse> CreateInput(string sceneName, string inputName, string inputKind, Input? inputSettings, bool sceneItemEnabled = true)
        {
            return await this.CreateInput(sceneName, null, inputName, inputKind, inputSettings, sceneItemEnabled);
        }

        /// <summary>
        /// Creates a new input, adding it as a scene item to the specified scene.
        /// </summary>
        /// <param name="sceneUuid">Uuid of the scene to add the input to as a scene item</param>
        /// <param name="inputName">Name of the new input to created</param>
        /// <param name="inputKind">The kind of input to be created</param>
        /// <param name="inputSettings">Settings object to initialize the input with</param>
        /// <param name="sceneItemEnabled">Whether to set the created scene item to enabled or disabled</param>
        /// <returns>ID of the newly created scene item</returns>
        public async Task<CreateInputResponse> CreateInput(Guid sceneUuid, string inputName, string inputKind, Input? inputSettings, bool sceneItemEnabled = true)
        {
            return await this.CreateInput(null, sceneUuid, inputName, inputKind, inputSettings, sceneItemEnabled);
        }

        private async Task<CreateInputResponse> CreateInput(string? sceneName, Guid? sceneUuid, string inputName, string inputKind, Input? inputSettings, bool sceneItemEnabled = true)
        {
            return await this.SendRequestAsync<CreateInputResponse>(new { sceneName, sceneUuid, inputName, inputKind, inputSettings, sceneItemEnabled });
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
            await this.RemoveInput(inputName, null);
        }

        /// <summary>
        /// Removes an existing input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to remove</param>
        /// <remarks>
        /// Note: Will immediately remove all associated scene items.
        /// </remarks>
        public async Task RemoveInput(Guid inputUuid)
        {
            await this.RemoveInput(null, inputUuid);
        }

        private async Task RemoveInput(string? inputName, Guid? inputUuid)
        {
            await this.SendRequestAsync(new { inputName, inputUuid });
        }

        /// <summary>
        /// Sets the name of an input (rename).
        /// </summary>
        /// <param name="inputName">Current input name</param>
        /// <param name="newInputName">New name for the input</param>
        public async Task SetInputName(string inputName, string newInputName)
        {
            await this.SetInputName(inputName, null, newInputName);
        }

        /// <summary>
        /// Sets the name of an input (rename).
        /// </summary>
        /// <param name="inputUuid">Current input uuid</param>
        /// <param name="newInputName">New name for the input</param>
        public async Task SetInputName(Guid inputUuid, string newInputName)
        {
            await this.SetInputName(null, inputUuid, newInputName);
        }

        private async Task SetInputName(string? inputName, Guid? inputUuid, string newInputName)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, newInputName });
        }

        /// <summary>
        /// Gets the default settings for an input kind.
        /// </summary>
        /// <param name="inputKind">Input kind to get the default settings for</param>
        /// <returns>Default settings for the input kind.</returns>
        public async Task<Dictionary<string, object>> GetInputDefaultSettings(string inputKind)
        {
            return (await this.SendRequestAsync<DefaultInputSettingsResponse>(new { inputKind })).DefaultInputSettings;
        }

        /// <summary>
        /// Gets the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the settings of</param>
        /// <returns>A <see cref="InputSettingsResponse"/></returns>
        /// <remarks>
        /// Note: Does not include defaults. To create the entire settings object, overlay inputSettings over the defaultInputSettings provided by GetInputDefaultSettings.
        /// </remarks>
        public async Task<InputSettingsResponse> GetInputSettings(string inputName)
        {
            return await this.GetInputSettings(inputName, null);
        }

        /// <summary>
        /// Gets the settings of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to get the settings of</param>
        /// <returns>A <see cref="InputSettingsResponse"/></returns>
        /// <remarks>
        /// Note: Does not include defaults. To create the entire settings object, overlay inputSettings over the defaultInputSettings provided by GetInputDefaultSettings.
        /// </remarks>
        public async Task<InputSettingsResponse> GetInputSettings(Guid inputUuid)
        {
            return await this.GetInputSettings(null, inputUuid);
        }

        private async Task<InputSettingsResponse> GetInputSettings(string? inputName, Guid? inputUuid)
        {
            return await this.SendRequestAsync<InputSettingsResponse>(new { inputName, inputUuid });
        }

        /// <summary>
        /// Sets the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the settings of</param>
        /// <param name="inputSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public async Task SetInputSettings(string inputName, Dictionary<string, object> inputSettings, bool overlay = true)
        {
            await this.SetInputSettings(inputName, null, inputSettings, overlay);
        }

        /// <summary>
        /// Sets the settings of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to set the settings of</param>
        /// <param name="inputSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public async Task SetInputSettings(Guid inputUuid, Dictionary<string, object> inputSettings, bool overlay = true)
        {
            await this.SetInputSettings(null, inputUuid, inputSettings, overlay);
        }

        private async Task SetInputSettings(string? inputName, Guid? inputUuid, Dictionary<string, object> inputSettings, bool overlay = true)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, inputSettings, overlay });
        }

        /// <summary>
        /// Gets the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of input to get the mute state of</param>
        /// <returns>Whether the input is muted</returns>
        public async Task<bool> GetInputMute(string inputName)
        {
            return await this.GetInputMute(inputName, null);
        }

        /// <summary>
        /// Gets the audio mute state of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of input to get the mute state of</param>
        /// <returns>Whether the input is muted</returns>
        public async Task<bool> GetInputMute(Guid inputUuid)
        {
            return await this.GetInputMute(null, inputUuid);
        }

        private async Task<bool> GetInputMute(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<InputMutedResponse>(new { inputName, inputUuid })).InputMuted;
        }

        /// <summary>
        /// Sets the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the mute state of</param>
        /// <param name="inputMuted">Whether to mute the input or not</param>
        public async Task SetInputMute(string inputName, bool inputMuted)
        {
            await this.SetInputMute(inputName, null, inputMuted);
        }

        /// <summary>
        /// Sets the audio mute state of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to set the mute state of</param>
        /// <param name="inputMuted">Whether to mute the input or not</param>
        public async Task SetInputMute(Guid inputUuid, bool inputMuted)
        {
            await this.SetInputMute(null, inputUuid, inputMuted);
        }

        private async Task SetInputMute(string? inputName, Guid? inputGuid, bool inputMuted)
        {
            await this.SendRequestAsync(new { inputName, inputGuid, inputMuted });
        }

        /// <summary>
        /// Toggles the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to toggle the mute state of</param>
        /// <returns>Whether the input has been muted or unmuted</returns>
        public async Task<bool> ToggleInputMute(string inputName)
        {
            return await this.ToggleInputMute(inputName, null);
        }

        /// <summary>
        /// Toggles the audio mute state of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to toggle the mute state of</param>
        /// <returns>Whether the input has been muted or unmuted</returns>
        public async Task<bool> ToggleInputMute(Guid inputUuid)
        {
            return await this.ToggleInputMute(null, inputUuid);
        }

        private async Task<bool> ToggleInputMute(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<InputMutedResponse>(new { inputName, inputUuid })).InputMuted;
        }

        /// <summary>
        /// Gets the current volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the volume of</param>
        /// <returns>A <see cref="InputVolumeResponse"/></returns>
        public async Task<InputVolumeResponse> GetInputVolume(string inputName)
        {
            return await this.GetInputVolume(inputName, null);
        }

        /// <summary>
        /// Gets the current volume setting of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to get the volume of</param>
        /// <returns>A <see cref="InputVolumeResponse"/></returns>
        public async Task<InputVolumeResponse> GetInputVolume(Guid inputUuid)
        {
            return await this.GetInputVolume(null, inputUuid);
        }

        private async Task<InputVolumeResponse> GetInputVolume(string? inputName, Guid? inputUuid)
        {
            return await this.SendRequestAsync<InputVolumeResponse>(new { inputName, inputUuid });
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeMul">Volume setting in mul.</param>
        public async Task SetInputVolumeMul(string inputName, float inputVolumeMul)
        {
            await this.SetInputVolume(inputName, null, inputVolumeMul, null);
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to set the volume of</param>
        /// <param name="inputVolumeMul">Volume setting in mul.</param>
        public async Task SetInputVolumeMul(Guid inputUuid, float inputVolumeMul)
        {
            await this.SetInputVolume(null, inputUuid, inputVolumeMul, null);
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeDb">Volume setting in dB.</param>
        public async Task SetInputVolumeDb(string inputName, float inputVolumeDb)
        {
            await this.SetInputVolume(inputName, null, null, inputVolumeDb);
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to set the volume of</param>
        /// <param name="inputVolumeDb">Volume setting in dB.</param>
        public async Task SetInputVolumeDb(Guid inputUuid, float inputVolumeDb)
        {
            await this.SetInputVolume(null, inputUuid, null, inputVolumeDb);
        }

        private async Task SetInputVolume(string? inputName, Guid? inputUuid, float? inputVolumeMul, float? inputVolumeDb)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, inputVolumeMul, inputVolumeDb });
        }

        /// <summary>
        /// Gets the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio balance of</param>
        /// <returns>Audio balance value from 0.0-1.0</returns>
        public async Task<float> GetInputAudioBalance(string inputName)
        {
            return await this.GetInputAudioBalance(inputName, null);
        }

        /// <summary>
        /// Gets the audio balance of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to get the audio balance of</param>
        /// <returns>Audio balance value from 0.0-1.0</returns>
        public async Task<float> GetInputAudioBalance(Guid inputUuid)
        {
            return await this.GetInputAudioBalance(null, inputUuid);
        }

        private async Task<float> GetInputAudioBalance(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<InputAudioBalanceResponse>(new { inputName, inputUuid })).InputAudioBalance;
        }

        /// <summary>
        /// Sets the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio balance of</param>
        /// <param name="inputAudioBalance">New audio balance value.</param>
        public async Task SetInputAudioBalance(string inputName, float inputAudioBalance)
        {
            await this.SetInputAudioBalance(inputName, null, inputAudioBalance);
        }

        /// <summary>
        /// Sets the audio balance of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to set the audio balance of</param>
        /// <param name="inputAudioBalance">New audio balance value.</param>
        public async Task SetInputAudioBalance(Guid inputUuid, float inputAudioBalance)
        {
            await this.SetInputAudioBalance(null, inputUuid, inputAudioBalance);
        }

        private async Task SetInputAudioBalance(string? inputName, Guid? inputUuid, float inputAudioBalance)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, inputAudioBalance });
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
            return await this.GetInputAudioSyncOffset(inputName, null);
        }

        /// <summary>
        /// Gets the audio sync offset of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to get the audio sync offset of</param>
        /// <returns>Audio sync offset in milliseconds</returns>
        /// <remarks>
        /// Note: The audio sync offset can be negative too!
        /// </remarks>
        public async Task<int> GetInputAudioSyncOffset(Guid inputUuid)
        {
            return await this.GetInputAudioSyncOffset(null, inputUuid);
        }

        private async Task<int> GetInputAudioSyncOffset(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<InputAudioSyncOffsetResponse>(new { inputName, inputUuid })).InputAudioSyncOffset;
        }

        /// <summary>
        /// Sets the audio sync offset of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio sync offset of</param>
        /// <param name="inputAudioSyncOffset">New audio sync offset in milliseconds.</param>
        public async Task SetInputAudioSyncOffset(string inputName, int inputAudioSyncOffset)
        {
            await this.SetInputAudioSyncOffset(inputName, null, inputAudioSyncOffset);
        }

        /// <summary>
        /// Sets the audio sync offset of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to set the audio sync offset of</param>
        /// <param name="inputAudioSyncOffset">New audio sync offset in milliseconds.</param>
        public async Task SetInputAudioSyncOffset(Guid inputUuid, int inputAudioSyncOffset)
        {
            await this.SetInputAudioSyncOffset(null, inputUuid, inputAudioSyncOffset);
        }

        private async Task SetInputAudioSyncOffset(string? inputName, Guid? inputUuid, int inputAudioSyncOffset)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, inputAudioSyncOffset });
        }

        /// <summary>
        /// Gets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio monitor type of</param>
        /// <returns>Audio monitor type</returns>
        public async Task<MonitorType> GetInputAudioMonitorType(string inputName)
        {
            return await this.GetInputAudioMonitorType(inputName, null);
        }

        /// <summary>
        /// Gets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to get the audio monitor type of</param>
        /// <returns>Audio monitor type</returns>
        public async Task<MonitorType> GetInputAudioMonitorType(Guid inputUuid)
        {
            return await this.GetInputAudioMonitorType(null, inputUuid);
        }

        private async Task<MonitorType> GetInputAudioMonitorType(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<MonitorTypeResponse>(new { inputName, inputUuid })).MonitorType;
        }

        /// <summary>
        /// Sets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio monitor type of</param>
        /// <param name="monitorType">Audio monitor type</param>
        public async Task SetInputAudioMonitorType(string inputName, MonitorType monitorType)
        {
            await this.SetInputAudioMonitorType(inputName, null, monitorType);
        }

        /// <summary>
        /// Sets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to set the audio monitor type of</param>
        /// <param name="monitorType">Audio monitor type</param>
        public async Task SetInputAudioMonitorType(Guid inputUuid, MonitorType monitorType)
        {
            await this.SetInputAudioMonitorType(null, inputUuid, monitorType);
        }

        private async Task SetInputAudioMonitorType(string? inputName, Guid? inputUuid, MonitorType monitorType)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, monitorType });
        }

        /// <summary>
        /// Gets the enable state of all audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <returns>Object of audio tracks and associated enable states</returns>
        public async Task<AudioTracks> GetInputAudioTracks(string inputName)
        {
            return await this.GetInputAudioTracks(inputName, null);
        }

        /// <summary>
        /// Gets the enable state of all audio tracks of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input</param>
        /// <returns>Object of audio tracks and associated enable states</returns>
        public async Task<AudioTracks> GetInputAudioTracks(Guid inputUuid)
        {
            return await this.GetInputAudioTracks(null, inputUuid);
        }

        private async Task<AudioTracks> GetInputAudioTracks(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<InputAudioTracksResponse>(new { inputName, inputUuid })).InputAudioTracks;
        }

        /// <summary>
        /// Sets the enable state of audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="inputAudioTracks">Track settings to apply</param>
        public async Task SetInputAudioTracks(string inputName, AudioTracks inputAudioTracks)
        {
            await this.SetInputAudioTracks(inputName, null, inputAudioTracks);
        }

        /// <summary>
        /// Sets the enable state of audio tracks of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input</param>
        /// <param name="inputAudioTracks">Track settings to apply</param>
        public async Task SetInputAudioTracks(Guid inputUuid, AudioTracks inputAudioTracks)
        {
            await this.SetInputAudioTracks(null, inputUuid, inputAudioTracks);
        }

        private async Task SetInputAudioTracks(string? inputName, Guid? inputUuid, AudioTracks inputAudioTracks)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, inputAudioTracks });
        }

        /// <summary>
        /// Gets the deinterlace mode of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <returns>Deinterlace mode of the input</returns>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task<DeinterlaceMode> GetInputDeinterlaceMode(string inputName)
        {
            return await this.GetInputDeinterlaceMode(inputName, null);
        }

        /// <summary>
        /// Gets the deinterlace mode of an input.
        /// </summary>
        /// <param name="inputUuid">UUID of the input</param>
        /// <returns>Deinterlace mode of the input</returns>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task<DeinterlaceMode> GetInputDeinterlaceMode(Guid inputUuid)
        {
            return await this.GetInputDeinterlaceMode(null, inputUuid);
        }

        private async Task<DeinterlaceMode> GetInputDeinterlaceMode(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<InputDeinterlaceModeResponse>(new { inputName, inputUuid })).InputDeinterlaceMode;
        }

        /// <summary>
        /// Sets the deinterlace mode of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="inputDeinterlaceMode">Deinterlace mode for the input</param>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task SetInputDeinterlaceMode(string inputName, DeinterlaceMode inputDeinterlaceMode)
        {
            await this.SetInputDeinterlaceMode(inputName, null, inputDeinterlaceMode);
        }

        /// <summary>
        /// Sets the deinterlace mode of an input.
        /// </summary>
        /// <param name="inputUuid">UUID of the input</param>
        /// <param name="inputDeinterlaceMode">Deinterlace mode for the input</param>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task SetInputDeinterlaceMode(Guid inputUuid, DeinterlaceMode inputDeinterlaceMode)
        {
            await this.SetInputDeinterlaceMode(null, inputUuid, inputDeinterlaceMode);
        }

        private async Task SetInputDeinterlaceMode(string? inputName, Guid? inputUuid, DeinterlaceMode inputDeinterlaceMode)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, inputDeinterlaceMode });
        }

        /// <summary>
        /// Gets the deinterlace field order of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task<string> GetInputDeinterlaceFieldOrder(string inputName)
        {
            return await this.GetInputDeinterlaceFieldOrder(inputName, null);
        }

        /// <summary>
        /// Gets the deinterlace field order of an input.
        /// </summary>
        /// <param name="inputUuid">UUID of the input</param>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task<string> GetInputDeinterlaceFieldOrder(Guid inputUuid)
        {
            return await this.GetInputDeinterlaceFieldOrder(null, inputUuid);
        }

        private async Task<string> GetInputDeinterlaceFieldOrder(string? inputName, Guid? inputUuid)
        {
            return (await this.SendRequestAsync<InputDeinterlaceFieldOrderResponse>(new { inputName, inputUuid })).InputDeinterlaceFieldOrder;
        }

        /// <summary>
        /// Sets the deinterlace field order of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="inputDeinterlaceFieldOrder">Deinterlace field order for the input</param>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task SetInputDeinterlaceFieldOrder(string inputName, string inputDeinterlaceFieldOrder)
        {
            await this.SetInputDeinterlaceFieldOrder(inputName, null, inputDeinterlaceFieldOrder);
        }

        /// <summary>
        /// Sets the deinterlace field order of an input.
        /// </summary>
        /// <param name="inputUuid">UUID of the input</param>
        /// <param name="inputDeinterlaceFieldOrder">Deinterlace field order for the input</param>
        /// <remarks>
        /// Deinterlacing functionality is restricted to async inputs only.
        /// </remarks>
        public async Task SetInputDeinterlaceFieldOrder(Guid inputUuid, string inputDeinterlaceFieldOrder)
        {
            await this.SetInputDeinterlaceFieldOrder(null, inputUuid, inputDeinterlaceFieldOrder);
        }

        private async Task SetInputDeinterlaceFieldOrder(string? inputName, Guid? inputUuid, string inputDeinterlaceFieldOrder)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, inputDeinterlaceFieldOrder });
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
            return await this.GetInputPropertiesListPropertyItems(inputName, null, propertyName);
        }

        /// <summary>
        /// Gets the items of a list property from an input's properties.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input</param>
        /// <param name="propertyName">Name of the list property to get the items of</param>
        /// <returns>Array of items in the list property</returns>
        /// <remarks>
        /// Note: Use this in cases where an input provides a dynamic, selectable list of items. For example, display capture, where it provides a list of available displays.
        /// </remarks>
        public async Task<PropertyItem[]> GetInputPropertiesListPropertyItems(Guid inputUuid, string propertyName)
        {
            return await this.GetInputPropertiesListPropertyItems(null, inputUuid, propertyName);
        }

        private async Task<PropertyItem[]> GetInputPropertiesListPropertyItems(string? inputName, Guid? inputUuid, string propertyName)
        {
            return (await this.SendRequestAsync<PropertyItemsResponse>(new { inputName, inputUuid, propertyName })).PropertyItems;
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
            await this.PressInputPropertiesButton(inputName, null, propertyName);
        }

        /// <summary>
        /// Presses a button in the properties of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input</param>
        /// <param name="propertyName">Name of the button property to press</param>
        /// <remarks>
        /// Note: Use this in cases where there is a button in the properties of an input that cannot be accessed in any other way. For example, browser sources, where there is a refresh button.
        /// </remarks>
        public async Task PressInputPropertiesButton(Guid inputUuid, string propertyName)
        {
            await this.PressInputPropertiesButton(null, inputUuid, propertyName);
        }

        private async Task PressInputPropertiesButton(string? inputName, Guid? inputUuid, string propertyName)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, propertyName });
        }
    }
}
