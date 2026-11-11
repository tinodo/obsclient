namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;

    /// <summary>
    /// The state of a media input in the words that obs-websocket uses, for the tests that compare what the model shows with what the simulation of OBS Studio holds.
    /// </summary>
    internal static class MediaWire
    {
        /// <summary>Says a state as obs-websocket does.</summary>
        /// <param name="state">The state, or null for an input that is no media input.</param>
        /// <returns>The state as obs-websocket writes it, or null.</returns>
        public static string? Of(ObsMediaState? state)
        {
            return state switch
            {
                null => null,
                ObsMediaState.Playing => "OBS_MEDIA_STATE_PLAYING",
                ObsMediaState.Opening => "OBS_MEDIA_STATE_OPENING",
                ObsMediaState.Buffering => "OBS_MEDIA_STATE_BUFFERING",
                ObsMediaState.Paused => "OBS_MEDIA_STATE_PAUSED",
                ObsMediaState.Stopped => "OBS_MEDIA_STATE_STOPPED",
                ObsMediaState.Ended => "OBS_MEDIA_STATE_ENDED",
                ObsMediaState.Error => "OBS_MEDIA_STATE_ERROR",
                _ => "OBS_MEDIA_STATE_NONE",
            };
        }
    }
}
