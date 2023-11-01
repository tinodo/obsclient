namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Responses;
    using System;
    using System.Text.Json;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get data about the current plugin and RPC version.
        /// </summary>
        /// <returns>A <see cref="VersionResponse"/> object with OBS Studio Version information.</returns>
        public void AddGetVersionRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get statistics about OBS, obs-websocket, and the current session.
        /// </summary>
        /// <returns>A <see cref="StatsResponse"/> object with OBS Studio Statistics.</returns>
        public void AddGetStatsRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to broadcast a CustomEvent to all WebSocket clients. Receivers are clients which are identified and subscribed.
        /// </summary>
        /// <param name="eventData">Data payload to emit to all receivers</param>
        public void AddBroadcastCustomEventRequest(JsonElement eventData)
        {
            this.Requests.Add(new(new { eventData }));
        }

        /// <summary>
        /// Adds a request to call a request registered to a vendor.
        /// </summary>
        /// <param name="vendorName">Name of the vendor to use</param>
        /// <param name="requestType">The request type to call</param>
        /// <param name="requestData">Object containing appropriate request data</param>
        public void AddCallVendorRequestRequest(string vendorName, string requestType, JsonElement? requestData)
        {
            this.Requests.Add(new(new { vendorName, requestType, requestData }));
        }

        /// <summary>
        /// Adds a request to get an array of all hotkey names in OBS
        /// </summary>
        /// <returns>Array of hotkey names</returns>
        public void AddGetHotkeyListRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to trigger a hotkey using its name. See <see cref="GetHotkeyList"/>
        /// </summary>
        /// <param name="hotkeyName">Name of the hotkey to trigger</param>
        public void AddTriggerHotkeyByNameRequest(string hotkeyName)
        {
            this.Requests.Add(new(new { hotkeyName }));
        }

        /// <summary>
        /// Adds a request to trigger a hotkey using a sequence of keys.
        /// </summary>
        /// <param name="keyId">The OBS key ID to use. See https://github.com/obsproject/obs-studio/blob/master/libobs/obs-hotkeys.h</param>
        /// <param name="keyModifier">Key modifiers to apply</param>
        public void AddTriggerHotkeyByKeySequenceRequest(ObsKey? keyId, KeyModifier? keyModifier)
        {
            KeyModifiers keyModifiers = new(keyModifier);
            this.Requests.Add(new(new { keyId, keyModifiers }));
        }

        /// <summary>
        /// Adds a request to sleeps for a time duration or number of frames. Only available in request batches with types SERIAL_REALTIME or SERIAL_FRAME.
        /// </summary>
        /// <param name="sleepMillis">Number of milliseconds to sleep for (if SERIAL_REALTIME mode)</param>
        /// <param name="sleepFrames">Number of frames to sleep for (if SERIAL_FRAME mode)</param>
        /// <exception cref="NotSupportedException"></exception>
        public void AddSleepRequest(int? sleepMillis, int? sleepFrames)
        {
            this.Requests.Add(new(new { sleepMillis, sleepFrames }));
        }
    }
}
