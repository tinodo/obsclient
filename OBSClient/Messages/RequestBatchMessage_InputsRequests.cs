namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Responses;
    using System;
    using System.Collections.Generic;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get an array of all inputs in OBS.
        /// </summary>
        /// <param name="inputKind">Restrict the array to only inputs of the specified kind</param>
        /// <returns>An array of <see cref="Input"/></returns>
        public void AddGetInputListRequest(string? inputKind = null)
        {
            this._requests.Add(new(new { inputKind }));
        }

        /// <summary>
        /// Adds a request to get an array of all available input kinds in OBS.
        /// </summary>
        /// <param name="unversioned">True == Return all kinds as unversioned, False == Return with version suffixes (if available)</param>
        /// <returns>Array of input kinds</returns>
        public void AddGetInputKindListRequest(bool unversioned = false)
        {
            this._requests.Add(new(new { unversioned }));
        }

        /// <summary>
        /// Adds a request to get the names of all special inputs.
        /// </summary>
        /// <returns>A <see cref="SpecialInputsResponse"/></returns>
        public void AddGetSpecialInputsRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to create a new input, adding it as a scene item to the specified scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene to add the input to as a scene item</param>
        /// <param name="inputName">Name of the new input to created</param>
        /// <param name="inputKind">The kind of input to be created</param>
        /// <param name="inputSettings">Settings object to initialize the input with</param>
        /// <param name="sceneItemEnabled">Whether to set the created scene item to enabled or disabled</param>
        /// <returns>ID of the newly created scene item</returns>
        public void AddCreateInputRequest(string sceneName, string inputName, string inputKind, Input? inputSettings, bool sceneItemEnabled = true)
        {
            this._requests.Add(new(new { sceneName, inputName, inputKind, inputSettings, sceneItemEnabled }));
        }

        /// <summary>
        /// Adds a request to remove an existing input.
        /// </summary>
        /// <param name="inputName">Name of the input to remove</param>
        /// <remarks>
        /// Note: Will immediately remove all associated scene items.
        /// </remarks>
        public void AddRemoveInputRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to set the name of an input (rename).
        /// </summary>
        /// <param name="inputName">Current input name</param>
        /// <param name="newInputName">New name for the input</param>
        public void AddSetInputNameRequest(string inputName, string newInputName)
        {
            this._requests.Add(new(new { inputName, newInputName }));
        }

        /// <summary>
        /// Adds a request to get the default settings for an input kind.
        /// </summary>
        /// <param name="inputKind">Input kind to get the default settings for</param>
        /// <returns>Default settings for the input kind.</returns>
        public void AddGetInputDefaultSettingsRequest(string inputKind)
        {
            this._requests.Add(new(new { inputKind }));
        }

        /// <summary>
        /// Adds a request to get the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the settings of</param>
        /// <returns>A <see cref="InputSettingsResponse"/></returns>
        /// <remarks>
        /// Note: Does not include defaults. To create the entire settings object, overlay inputSettings over the defaultInputSettings provided by GetInputDefaultSettings.
        /// </remarks>
        public void AddGetInputSettingsRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to set the settings of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the settings of</param>
        /// <param name="inputSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public void AddSetInputSettingsRequest(string inputName, Dictionary<string, object> inputSettings, bool overlay = true)
        {
            this._requests.Add(new(new { inputName, inputSettings, overlay }));
        }

        /// <summary>
        /// Adds a request to get the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of input to get the mute state of</param>
        /// <returns>Whether the input is muted</returns>
        public void AddGetInputMuteRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to set the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the mute state of</param>
        /// <param name="inputMuted">Whether to mute the input or not</param>
        public void AddSetInputMuteRequest(string inputName, bool inputMuted)
        {
            this._requests.Add(new(new { inputName, inputMuted }));
        }

        /// <summary>
        /// Adds a request to toggle the audio mute state of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to toggle the mute state of</param>
        /// <returns>Whether the input has been muted or unmuted</returns>
        public void AddToggleInputMuteRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to add a request to get the current volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the volume of</param>
        /// <returns>A <see cref="InputVolumeResponse"/></returns>
        public void AddGetInputVolumeRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Sets the volume setting of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the volume of</param>
        /// <param name="inputVolumeMul">Volume setting in mul.</param>
        /// <param name="inputVolumeDb">Volume setting in dB.</param>
        /// <remarks>Use either inputVolumeMul or inputVolumeDb.</remarks>
        /// <exception cref="ArgumentException"></exception>
        public void AddSetInputVolumeRequest(string inputName, float? inputVolumeMul, float? inputVolumeDb)
        {
            if (inputVolumeMul is null && inputVolumeDb is null)
            {
                throw new ArgumentException("Either inputVolumeMul or inputVolumeDb must be set.");
            }

            if (inputVolumeMul is not null && inputVolumeDb is not null)
            {
                throw new ArgumentException("Only one of inputVolumeMul or inputVolumeDb can be set.");
            }

            this._requests.Add(new(new { inputName, inputVolumeMul, inputVolumeDb }));
        }

        /// <summary>
        /// Adds a request to get the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio balance of</param>
        /// <returns>Audio balance value from 0.0-1.0</returns>
        public void AddGetInputAudioBalanceRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to set the audio balance of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio balance of</param>
        /// <param name="inputAudioBalance">New audio balance value.</param>
        public void AddSetInputAudioBalanceRequest(string inputName, float inputAudioBalance)
        {
            this._requests.Add(new(new { inputName, inputAudioBalance }));
        }

        /// <summary>
        /// Adds a request to get the audio sync offset of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio sync offset of</param>
        /// <returns>Audio sync offset in milliseconds</returns>
        /// <remarks>
        /// Note: The audio sync offset can be negative too!
        /// </remarks>
        public void AddGetInputAudioSyncOffsetRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to set the audio sync offset of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio sync offset of</param>
        /// <param name="inputAudioSyncOffset">New audio sync offset in milliseconds.</param>
        public void AddSetInputAudioSyncOffsetRequest(string inputName, int inputAudioSyncOffset)
        {
            this._requests.Add(new(new { inputName, inputAudioSyncOffset }));
        }

        /// <summary>
        /// Adds a request to get the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to get the audio monitor type of</param>
        /// <returns>Audio monitor type</returns>
        public void AddGetInputAudioMonitorTypeRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to set the audio monitor type of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to set the audio monitor type of</param>
        /// <param name="monitorType">Audio monitor type</param>
        public void AddSetInputAudioMonitorTypeRequest(string inputName, MonitorType monitorType)
        {
            this._requests.Add(new(new { inputName, monitorType }));
        }

        /// <summary>
        /// Adds a request to get the enable state of all audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <returns>Object of audio tracks and associated enable states</returns>
        public void AddGetInputAudioTracksRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to set the enable state of audio tracks of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="inputAudioTracks">Track settings to apply</param>
        public void AddSetInputAudioTracksRequest(string inputName, AudioTracks inputAudioTracks)
        {
            this._requests.Add(new(new { inputName, inputAudioTracks }));
        }

        /// <summary>
        /// Adds a request to get the items of a list property from an input's properties.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="propertyName">Name of the list property to get the items of</param>
        /// <returns>Array of items in the list property</returns>
        /// <remarks>
        /// Note: Use this in cases where an input provides a dynamic, selectable list of items. For example, display capture, where it provides a list of available displays.
        /// </remarks>
        public void AddGetInputPropertiesListPropertyItemsRequest(string inputName, string propertyName)
        {
            this._requests.Add(new(new { inputName, propertyName }));
        }

        /// <summary>
        /// Adds a request to press a button in the properties of an input.
        /// </summary>
        /// <param name="inputName">Name of the input</param>
        /// <param name="propertyName">Name of the button property to press</param>
        /// <remarks>
        /// Note: Use this in cases where there is a button in the properties of an input that cannot be accessed in any other way. For example, browser sources, where there is a refresh button.
        /// </remarks>
        public void AddPressInputPropertiesButtonRequest(string inputName, string propertyName)
        {
            this._requests.Add(new(new { inputName, propertyName }));
        }
    }
}
