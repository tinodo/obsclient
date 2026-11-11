namespace OBSStudioClient.Model
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;

    /// <summary>
    /// Converts between what obs-websocket says (the enums and strings of the client) and what the model shows (<see cref="ObsMonitorType"/>, <see cref="ObsAudioTracks"/>,
    /// <see cref="ObsDeinterlaceMode"/>, <see cref="ObsDeinterlaceFieldOrder"/>, <see cref="ObsMediaState"/> and <see cref="ObsDataRealm"/>).
    /// </summary>
    internal static class ModelEnums
    {
        private const string FieldOrderTop = "OBS_DEINTERLACE_FIELD_ORDER_TOP";
        private const string FieldOrderBottom = "OBS_DEINTERLACE_FIELD_ORDER_BOTTOM";

        public static ObsMonitorType ToModel(MonitorType type)
        {
            return type switch
            {
                MonitorType.OBS_MONITORING_TYPE_MONITOR_ONLY => ObsMonitorType.MonitorOnly,
                MonitorType.OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT => ObsMonitorType.MonitorAndOutput,
                _ => ObsMonitorType.Off,
            };
        }

        public static MonitorType ToWire(ObsMonitorType type)
        {
            return type switch
            {
                ObsMonitorType.MonitorOnly => MonitorType.OBS_MONITORING_TYPE_MONITOR_ONLY,
                ObsMonitorType.MonitorAndOutput => MonitorType.OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT,
                _ => MonitorType.OBS_MONITORING_TYPE_NONE,
            };
        }

        public static ObsDeinterlaceMode ToModel(DeinterlaceMode mode)
        {
            return mode switch
            {
                DeinterlaceMode.OBS_DEINTERLACE_MODE_DISCARD => ObsDeinterlaceMode.Discard,
                DeinterlaceMode.OBS_DEINTERLACE_MODE_RETRO => ObsDeinterlaceMode.Retro,
                DeinterlaceMode.OBS_DEINTERLACE_MODE_BLEND => ObsDeinterlaceMode.Blend,
                DeinterlaceMode.OBS_DEINTERLACE_MODE_BLEND_2X => ObsDeinterlaceMode.Blend2x,
                DeinterlaceMode.OBS_DEINTERLACE_MODE_LINEAR => ObsDeinterlaceMode.Linear,
                DeinterlaceMode.OBS_DEINTERLACE_MODE_LINEAR_2X => ObsDeinterlaceMode.Linear2x,
                DeinterlaceMode.OBS_DEINTERLACE_MODE_YADIF => ObsDeinterlaceMode.Yadif,
                DeinterlaceMode.OBS_DEINTERLACE_MODE_YADIF_2X => ObsDeinterlaceMode.Yadif2x,
                _ => ObsDeinterlaceMode.Disabled,
            };
        }

        public static DeinterlaceMode ToWire(ObsDeinterlaceMode mode)
        {
            return mode switch
            {
                ObsDeinterlaceMode.Discard => DeinterlaceMode.OBS_DEINTERLACE_MODE_DISCARD,
                ObsDeinterlaceMode.Retro => DeinterlaceMode.OBS_DEINTERLACE_MODE_RETRO,
                ObsDeinterlaceMode.Blend => DeinterlaceMode.OBS_DEINTERLACE_MODE_BLEND,
                ObsDeinterlaceMode.Blend2x => DeinterlaceMode.OBS_DEINTERLACE_MODE_BLEND_2X,
                ObsDeinterlaceMode.Linear => DeinterlaceMode.OBS_DEINTERLACE_MODE_LINEAR,
                ObsDeinterlaceMode.Linear2x => DeinterlaceMode.OBS_DEINTERLACE_MODE_LINEAR_2X,
                ObsDeinterlaceMode.Yadif => DeinterlaceMode.OBS_DEINTERLACE_MODE_YADIF,
                ObsDeinterlaceMode.Yadif2x => DeinterlaceMode.OBS_DEINTERLACE_MODE_YADIF_2X,
                _ => DeinterlaceMode.OBS_DEINTERLACE_MODE_DISABLE,
            };
        }

        public static ObsDeinterlaceFieldOrder ToModelFieldOrder(string order)
        {
            return string.Equals(order, FieldOrderBottom, StringComparison.Ordinal) ? ObsDeinterlaceFieldOrder.BottomFieldFirst : ObsDeinterlaceFieldOrder.TopFieldFirst;
        }

        public static ObsMediaState ToModel(MediaState state)
        {
            return state switch
            {
                MediaState.OBS_MEDIA_STATE_PLAYING => ObsMediaState.Playing,
                MediaState.OBS_MEDIA_STATE_OPENING => ObsMediaState.Opening,
                MediaState.OBS_MEDIA_STATE_BUFFERING => ObsMediaState.Buffering,
                MediaState.OBS_MEDIA_STATE_PAUSED => ObsMediaState.Paused,
                MediaState.OBS_MEDIA_STATE_STOPPED => ObsMediaState.Stopped,
                MediaState.OBS_MEDIA_STATE_ENDED => ObsMediaState.Ended,
                MediaState.OBS_MEDIA_STATE_ERROR => ObsMediaState.Error,
                _ => ObsMediaState.None,
            };
        }

        public static string ToWire(ObsDeinterlaceFieldOrder order)
        {
            return order == ObsDeinterlaceFieldOrder.BottomFieldFirst ? FieldOrderBottom : FieldOrderTop;
        }

        public static Realm ToWire(ObsDataRealm realm)
        {
            return realm switch
            {
                ObsDataRealm.Global => Realm.OBS_WEBSOCKET_DATA_REALM_GLOBAL,
                ObsDataRealm.Profile => Realm.OBS_WEBSOCKET_DATA_REALM_PROFILE,
                _ => throw new ArgumentOutOfRangeException(nameof(realm), realm, "This is not a realm of persistent data: use ObsDataRealm.Global or ObsDataRealm.Profile."),
            };
        }

        // The tracks as one bit for each: track 1 is bit 0.
        public static int ToMask(AudioTracks tracks)
        {
            return (tracks.Track1 ? 1 : 0) | (tracks.Track2 ? 2 : 0) | (tracks.Track3 ? 4 : 0) | (tracks.Track4 ? 8 : 0) | (tracks.Track5 ? 16 : 0) | (tracks.Track6 ? 32 : 0);
        }

        // The data that says the state of every track: obs-websocket takes an object with a boolean for the tracks that it names (the names are "1" to "6").
        public static Dictionary<string, bool> ToWire(ObsAudioTracks tracks)
        {
            var mask = (int)tracks;
            var wire = new Dictionary<string, bool>(6);
            for (var track = 1; track <= 6; track++)
            {
                wire[track.ToString(System.Globalization.CultureInfo.InvariantCulture)] = (mask & (1 << (track - 1))) != 0;
            }

            return wire;
        }
    }
}
