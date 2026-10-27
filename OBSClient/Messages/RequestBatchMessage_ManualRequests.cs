namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;

    // The requests that tools/protocol/generate.py does not write ([manual] in tools/protocol/overlay.toml): the client offers
    // them in another shape than the protocol does.
    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to trigger a hotkey using a sequence of keys.
        /// </summary>
        /// <param name="keyId">The OBS key ID to use. See https://github.com/obsproject/obs-studio/blob/master/libobs/obs-hotkeys.h</param>
        /// <param name="keyModifier">Key modifiers to apply</param>
        public void AddTriggerHotkeyByKeySequenceRequest(ObsKey? keyId, KeyModifier? keyModifier)
        {
            KeyModifiers keyModifiers = new(keyModifier);
            this._requests.Add(new RequestMessage(RequestType.TriggerHotkeyByKeySequence, new { keyId, keyModifiers }));
        }

        /// <summary>
        /// Adds a request to sleep for a time duration or number of frames. Only available in request batches with types SERIAL_REALTIME or SERIAL_FRAME.
        /// </summary>
        /// <param name="sleepMillis">Number of milliseconds to sleep for (if SERIAL_REALTIME mode, between 0 and 50000)</param>
        /// <param name="sleepFrames">Number of frames to sleep for (if SERIAL_FRAME mode, between 0 and 10000)</param>
        /// <exception cref="NotSupportedException">Both or none of <paramref name="sleepMillis"/> and <paramref name="sleepFrames"/> are set.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The value is out of range.</exception>
        public void AddSleepRequest(int? sleepMillis, int? sleepFrames)
        {
            if (sleepFrames == null && sleepMillis == null)
            {
                throw new NotSupportedException("Either sleepMillis or sleepFrames must be set.");
            }

            if (sleepFrames != null && sleepMillis != null)
            {
                throw new NotSupportedException("Only one of sleepMillis or sleepFrames can be set.");
            }

            if (sleepMillis.HasValue && sleepMillis.Value is < 0 or > 50000)
            {
                throw new ArgumentOutOfRangeException(nameof(sleepMillis), "sleepMillis must be between 0 and 50000.");
            }

            if (sleepFrames.HasValue && sleepFrames.Value is < 0 or > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(sleepFrames), "sleepFrames must be between 0 and 10000.");
            }

            this._requests.Add(new RequestMessage(RequestType.Sleep, new { sleepMillis, sleepFrames }));
        }
    }
}
