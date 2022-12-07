namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets data about the current plugin and RPC version.
        /// </summary>
        /// <returns>A <see cref="VersionResponseData"/> object with OBS Studio Version information.</returns>
        public async Task<VersionResponseData> GetVersion()
        {
            return await this.SendRequestAsync<VersionResponseData>();
        }

        /// <summary>
        /// Gets statistics about OBS, obs-websocket, and the current session.
        /// </summary>
        /// <returns>A <see cref="StatsResponseData"/> object with OBS Studio Statistics.</returns>
        public async Task<StatsResponseData> GetStats() 
        {
            return await this.SendRequestAsync<StatsResponseData>();
        }

        /// <summary>
        /// Broadcasts a CustomEvent to all WebSocket clients. Receivers are clients which are identified and subscribed.
        /// </summary>
        /// <param name="eventData">Data payload to emit to all receivers</param>
        public async Task BroadcastCustomEvent(object eventData)
        {
            await this.SendRequestAsync(new { eventData });
        }

        /// <summary>
        /// Call a request registered to a vendor.
        /// </summary>
        /// <param name="vendorName">Name of the vendor to use</param>
        /// <param name="requestType">The request type to call</param>
        /// <param name="requestData">Object containing appropriate request data</param>
        public async Task<CallVendorRequestResponseData> CallVendorRequest(string vendorName, string requestType, object? requestData)
        {
            return await this.SendRequestAsync<CallVendorRequestResponseData>(new {vendorName, requestType, requestData});
        }

        /// <summary>
        /// Gets an array of all hotkey names in OBS
        /// </summary>
        /// <returns>Array of hotkey names</returns>
        public async Task<string[]> GetHotkeyList()
        {
            return (await this.SendRequestAsync<HotkeysResponseData>()).Hotkeys;
        }

        /// <summary>
        /// Triggers a hotkey using its name. See <see cref="GetHotkeyList"/>
        /// </summary>
        /// <param name="hotkeyName">Name of the hotkey to trigger</param>
        public async Task TriggerHotkeyByName(string hotkeyName)
        {
            await this.SendRequestAsync(new { hotkeyName });
        }

        /// <summary>
        /// Triggers a hotkey using a sequence of keys.
        /// </summary>
        /// <param name="keyId">The OBS key ID to use. See https://github.com/obsproject/obs-studio/blob/master/libobs/obs-hotkeys.h</param>
        /// <param name="keyModifier">Key modifiers to apply</param>
        public async Task TriggerHotkeyByKeySequence(ObsKey? keyId, KeyModifier? keyModifier)
        {
            KeyModifiers keyModifiers = new(keyModifier);
            await this.SendRequestAsync(new { keyId, keyModifiers });
        }

        /// <summary>
        /// Sleeps for a time duration or number of frames. Only available in request batches with types SERIAL_REALTIME or SERIAL_FRAME.
        /// </summary>
        /// <param name="sleepMillis">Number of milliseconds to sleep for (if SERIAL_REALTIME mode)</param>
        /// <param name="sleepFrames">Number of frames to sleep for (if SERIAL_FRAME mode)</param>
        /// <exception cref="NotSupportedException"></exception>
        /// <remarks>
        /// This method was only added for your reference. You can use Sleep in <see cref="SendRequestBatch(RequestBatchExecutionType, MessageClasses.RequestMessage[], bool)"/> and the method here is just added for parameter reference.
        /// </remarks>
        public void Sleep(int? sleepMillis, int? sleepFrames)
        {
            throw new NotSupportedException("Only available in request batches with types SERIAL_REALTIME or SERIAL_FRAME.");
        }
    }
}
