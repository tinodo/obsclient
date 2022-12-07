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
        public Input[] GetInputList(string? inputKind = null)
        {
            return this.SendRequest<InputsResponseData>(new { inputKind }).Inputs;
        }

        /// <summary>
        /// Gets an array of all available input kinds in OBS.
        /// </summary>
        /// <param name="unversioned">True == Return all kinds as unversioned, False == Return with version suffixes (if available)</param>
        /// <returns>Array of input kinds</returns>
        public string[] GetInputKindList(bool unversioned = false)
        {
            return this.SendRequest<InputKindsResponseData>(new { unversioned }).InputKinds;
        }

        /// <summary>
        /// Gets the names of all special inputs.
        /// </summary>
        /// <returns>A <see cref="SpecialInputsResponseData"/></returns>
        public SpecialInputsResponseData GetSpecialInputs()
        {
            return this.SendRequest<SpecialInputsResponseData>();
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
        public int CreateInput(string sceneName, string inputName, string inputKind, Input? inputSettings, bool sceneItemEnabled = true)
        {
            return this.SendRequest<SceneItemIdResponseData>(new { sceneName, inputName, inputKind, inputSettings, sceneItemEnabled }).SceneItemId;
        }

        /// <summary>
        /// Removes an existing input.
        /// </summary>
        /// <param name="inputName">Name of the input to remove</param>
        /// <remarks>
        /// Note: Will immediately remove all associated scene items.
        /// </remarks>
        public void RemoveInput(string inputName)
        {
            this.SendRequest(new { inputName });
        }

        /// <summary>
        /// Sets the name of an input (rename).
        /// </summary>
        /// <param name="inputName">Current input name</param>
        /// <param name="newInputName">New name for the input</param>
        public void SetInputName(string inputName, string newInputName) 
        {
            this.SendRequest(new { inputName, newInputName });
        }

        /// <summary>
        /// Gets the default settings for an input kind.
        /// </summary>
        /// <param name="inputKind">Input kind to get the default settings for</param>
        /// <returns>A <see cref="Dictionary<string, object>"/> of default settings for the input kind</returns>
        public Dictionary<string, object> GetInputDefaultSettings(string inputKind)
        {
            return this.SendRequest<DefaultInputSettingsResponseData>(new{ inputKind }).DefaultInputSettings;
        }

        /// <summary>
        /// Gets the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the settings of</param>
        /// <returns>A <see cref="InputSettingsResponseData"/></returns>
        /// <remarks>
        /// Note: Does not include defaults. To create the entire settings object, overlay inputSettings over the defaultInputSettings provided by GetInputDefaultSettings.
        /// </remarks>
        public InputSettingsResponseData GetInputSettings(string inputName)
        {
            return this.SendRequest<InputSettingsResponseData>(new { inputName });
        }

        /// <summary>
        /// Sets the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the settings of</param>
        /// <param name="inputSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public void SetInputSettings(string inputName, Dictionary<string, object> inputSettings, bool overlay = true)
        {
            this.SendRequest(new { inputName, inputSettings, overlay });
        }

        /// <summary>
        /// Gets the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of input to get the mute state of</param>
        /// <returns>Whether the input is muted</returns>
        public bool GetInputMute(string inputName)
        {
            return this.SendRequest<InputMutedResponseData>(new { inputName }).InputMuted;
        }

        /// <summary>
        /// Sets the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the mute state of</param>
        /// <param name="inputMuted">Whether to mute the input or not</param>
        public void SetInputMute(string inputName, bool inputMuted)
        {
            this.SendRequest(new { inputName, inputMuted });
        }

        /// <summary>
        /// Toggles the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to toggle the mute state of</param>
        /// <returns>Whether the input has been muted or unmuted</returns>
        public bool ToggleInputMute(string inputName)
        {
            return this.SendRequest<InputMutedResponseData>(new { inputName }).InputMuted;
        }

        /// <summary>
        /// Gets the current volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the volume of</param>
        /// <returns>A <see cref="InputVolumeResponseData"/></returns>
        public InputVolumeResponseData GetInputVolume(string inputName)
        {
            return this.SendRequest<InputVolumeResponseData>(new { inputName });
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeMul">Volume setting in mul (>= 0, <= 20)</param>
        public void SetInputVolumeMul(string inputName, float inputVolumeMul)
        {
            this.SetInputVolume(inputName, inputVolumeMul, null);
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeDb">Volume setting in dB (>= -100, <= 26)</param>
        public void SetInputVolumeDb(string inputName, float inputVolumeDb)
        {
            this.SetInputVolume(inputName, null, inputVolumeDb);
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeMul">Volume setting in mul (>= 0, <= 20)</param>
        /// <param name="inputVolumeDb">Volume setting in dB (>= -100, <= 26)</param>
        /// <exception cref="ArgumentException"></exception>
        private void SetInputVolume(string inputName, float? inputVolumeMul, float? inputVolumeDb)
        {
            if ((inputVolumeMul.HasValue && inputVolumeDb.HasValue) || (!inputVolumeMul.HasValue && !inputVolumeDb.HasValue))
            {
                throw new ArgumentException($"Specify either {nameof(inputVolumeMul)} or {nameof(inputVolumeDb)}");
            }

            this.SendRequest(new { inputName, inputVolumeMul, inputVolumeDb });
        }

        /// <summary>
        /// Gets the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio balance of</param>
        /// <returns>Audio balance value from 0.0-1.0</returns>
        public float GetInputAudioBalance(string inputName)
        {
            return this.SendRequest<InputAudioBalanceResponseData>(new { inputName }).InputAudioBalance;
        }

        /// <summary>
        /// Sets the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio balance of</param>
        /// <param name="inputAudioBalance">New audio balance value (>= 0.0, <= 1.0)</param>
        public void SetInputAudioBalance(string inputName, float inputAudioBalance)
        {
            this.SendRequest(new { inputName, inputAudioBalance });
        }

        /// <summary>
        /// Gets the audio sync offset of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio sync offset of</param>
        /// <returns>Audio sync offset in milliseconds</returns>
        /// <remarks>
        /// Note: The audio sync offset can be negative too!
        /// </remarks>
        public int GetInputAudioSyncOffset(string inputName)
        {
            return this.SendRequest<InputAudioSyncOffsetResponseData>(new { inputName }).InputAudioSyncOffset;
        }

        /// <summary>
        /// Sets the audio sync offset of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio sync offset of</param>
        /// <param name="inputAudioSyncOffset">New audio sync offset in milliseconds (>= -950, <= 20000)</param>
        public void SetInputAudioSyncOffset(string inputName, int inputAudioSyncOffset)
        {
            this.SendRequest(new { inputName, inputAudioSyncOffset });
        }

        /// <summary>
        /// Gets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio monitor type of</param>
        /// <returns>Audio monitor type</returns>
        public MonitorType GetInputAudioMonitorType(string inputName)
        {
            return this.SendRequest<MonitorTypeResponseData>(new { inputName }).MonitorType;
        }

        /// <summary>
        /// Sets the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio monitor type of</param>
        /// <param name="monitorType">Audio monitor type</param>
        public void SetInputAudioMonitorType(string inputName, MonitorType monitorType)
        {
            this.SendRequest(new { inputName, monitorType });
        }

        /// <summary>
        /// Gets the enable state of all audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <returns>Object of audio tracks and associated enable states</returns>
        public AudioTracks GetInputAudioTracks(string inputName)
        {
            return this.SendRequest<InputAudioTracksResponseData>(new { inputName }).InputAudioTracks;
        }

        /// <summary>
        /// Sets the enable state of audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="inputAudioTracks">Track settings to apply</param>
        public void SetInputAudioTracks(string inputName, AudioTracks inputAudioTracks)
        {
            this.SendRequest(new { inputName, inputAudioTracks });
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
        public PropertyItem[] GetInputPropertiesListPropertyItems(string inputName, string propertyName)
        {
            return this.SendRequest<PropertyItemsResponseData>(new { inputName, propertyName }).PropertyItems;
        }

        /// <summary>
        /// Presses a button in the properties of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="propertyName">Name of the button property to press</param>
        /// <remarks>
        /// Note: Use this in cases where there is a button in the properties of an input that cannot be accessed in any other way. For example, browser sources, where there is a refresh button.
        /// </remarks>
        public void PressInputPropertiesButton(string inputName, string propertyName)
        {
            this.SendRequest(new { inputName, propertyName });
        }
    }
}
