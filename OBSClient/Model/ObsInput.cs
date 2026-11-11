namespace OBSStudioClient.Model
{
    using System.Text.Json;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// An input of OBS Studio: a source of video, audio or both (a camera, a microphone, an image, a browser source, and so on).
    /// </summary>
    public sealed partial class ObsInput : ObsSource
    {
        private bool _muted;
        private float _volumeMul = 1f;
        private float _volumeDb;
        private float _audioBalance = 0.5f;
        private int _syncOffset;
        private int _monitorType;
        private int _audioTracks = (int)ObsAudioTracks.All;
        private SettingsValue _settings = SettingsValue.Empty;
        private ObsMedia? _media;
        private int _active = Unknown;
        private int _showing = Unknown;

        // What the activity is until the model has it: it is only kept when it follows it (ObsHighVolume.Activity).
        private const int Unknown = -1;

        internal ObsInput(ObsModel model, Guid uuid, string name, ObsObjectState state, string kind, bool hasAudio)
            : base(model, uuid, name, state)
        {
            this.Kind = kind;
            this.HasAudio = hasAudio;
        }

        /// <summary>
        /// Gets the kind of the input, for example <c>wasapi_input_capture</c> or <c>color_source_v3</c>.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Gets whether the input has audio. Only then are <see cref="IsMuted"/>, <see cref="VolumeMul"/>, <see cref="VolumeDb"/>, <see cref="AudioBalance"/>, <see cref="SyncOffset"/>,
        /// <see cref="MonitorType"/> and <see cref="AudioTracks"/> meaningful.
        /// </summary>
        public bool HasAudio { get; }

        /// <summary>
        /// Gets the settings of the input: a JSON object with the values that were set, as OBS Studio says them (<c>{"text":"Hello","font":{"face":"Arial","size":48}}</c> for a text input).
        /// </summary>
        /// <remarks>
        /// <para>
        /// OBS Studio says the settings that were set, and leaves out the ones that still have the default of the input kind: ask <see cref="ObsModel.GetDefaultInputSettingsAsync"/> for the
        /// defaults of a kind, and lay the settings over them for the whole picture. What a setting is called, and what it means, depends on the kind of the input, and is not written
        /// down anywhere but in the source of OBS Studio and its plugins: the properties window of the input is the place to look (or <see cref="GetPropertyItemsAsync"/> for the choices of a list).
        /// </para>
        /// <para>
        /// The value is current: OBS Studio announces every change of the settings (the property changes when a person changes the input, or a request, a script or the input
        /// itself does). It is a snapshot: a change makes a new element, and an element that you hold does not change. An input that has no settings set has an empty object. Change the
        /// settings with <see cref="SetSettingsAsync"/>.
        /// </para>
        /// </remarks>
        public JsonElement Settings => Volatile.Read(ref this._settings).Element;

        /// <summary>
        /// Gets whether the input is muted. The value is current: OBS Studio announces every change of it.
        /// </summary>
        public bool IsMuted => Volatile.Read(ref this._muted);

        /// <summary>
        /// Gets the volume of the input as a multiplier (1 is 0 dB), with the precision that OBS Studio has: a single-precision number, as the one of
        /// <see cref="ObsClient"/>, so that a volume of 0.1 reads 0.1. The value is current: OBS Studio announces every change of it.
        /// </summary>
        public float VolumeMul => Volatile.Read(ref this._volumeMul);

        /// <summary>
        /// Gets the volume of the input in decibel, as a single-precision number (see <see cref="VolumeMul"/>). The value is current: OBS Studio announces
        /// every change of it.
        /// </summary>
        public float VolumeDb => Volatile.Read(ref this._volumeDb);

        /// <summary>
        /// Gets the balance of the audio between the left and the right: from 0 (all on the left) to 1 (all on the right); 0.5 is the middle, which is what an input has until it is changed. The
        /// value is current: OBS Studio announces every change of it. Only meaningful when <see cref="HasAudio"/>.
        /// </summary>
        public float AudioBalance => Volatile.Read(ref this._audioBalance);

        /// <summary>
        /// Gets how much the audio of the input is delayed (when it is positive) or advanced (when it is negative), to bring it in sync with the video: from -950 ms to 20 s, in whole
        /// milliseconds. The value is current: OBS Studio announces every change of it. Only meaningful when <see cref="HasAudio"/>.
        /// </summary>
        public TimeSpan SyncOffset => TimeSpan.FromMilliseconds(Volatile.Read(ref this._syncOffset));

        /// <summary>
        /// Gets whether the audio of the input is also played on the monitoring device of the computer of OBS Studio. The value is current: OBS Studio announces every change of it. Only
        /// meaningful when <see cref="HasAudio"/>. OBS Studio 33 has no <see cref="ObsMonitorType.MonitorOnly"/>: see <see cref="ObsMonitorType"/>.
        /// </summary>
        public ObsMonitorType MonitorType => (ObsMonitorType)Volatile.Read(ref this._monitorType);

        /// <summary>
        /// Gets the audio tracks that the input is mixed into: a recording or a stream has the audio of the inputs that are on the tracks that it takes. The value is current: OBS Studio
        /// announces every change of it. Only meaningful when <see cref="HasAudio"/>.
        /// </summary>
        public ObsAudioTracks AudioTracks => (ObsAudioTracks)Volatile.Read(ref this._audioTracks);

        /// <summary>
        /// Gets the playback of the input when it plays media that can be controlled (a media source, a VLC video source): its state, and the calls that play, pause, stop and seek it. It is null
        /// for an input that is no media input (a camera, a color source, a browser source), and it is the same object for as long as the input exists.
        /// </summary>
        /// <remarks>
        /// OBS Studio says which kinds of input are media inputs (from obs-websocket 5.6.0); an OBS Studio that does not say it has two media kinds, <c>ffmpeg_source</c> and <c>vlc_source</c>, and
        /// the model takes those for media inputs and no others.
        /// </remarks>
        public ObsMedia? Media => Volatile.Read(ref this._media);

        /// <summary>
        /// Gets whether the input is on the air: shown in the program output, because the scene that is on the air shows it (or a scene in it, and the scene item is not hidden), or because a transition between two scenes
        /// shows it. It is null unless the model was asked to follow the activity (<see cref="ObsModelOptions.HighVolume"/> with <see cref="ObsHighVolume.Activity"/>): OBS Studio sends an event for the inputs of a scene each
        /// time that the program scene, a transition or a scene item that shows them changes, so the model does not ask for them unless they are wanted. To ask OBS Studio once, whatever the options are, use
        /// <see cref="ObsSource.GetActivityAsync"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is current. A scene that is put on the air with a transition puts its inputs on the air when the transition starts, and the scene that goes off the air takes its inputs off the air after the transition has
        /// ended. An input that two scenes on the air show stays on the air when one of them goes. OBS Studio does not announce the activity of an input that it destroys, and says nothing of a scene (a scene in the model has no
        /// activity: ask <see cref="ObsSource.GetActivityAsync"/>).
        /// </para>
        /// <para>
        /// In studio mode what is on the air is a copy of the program scene that OBS Studio made when the scene went on the air (or when studio mode was switched on): what is done to the scene itself afterwards (hiding a scene
        /// item, making an input) does not reach the copy, so the inputs stay as they were until the next scene goes on the air or studio mode is switched off (measured with OBS Studio 33.0.0). The inputs of the preview scene
        /// are shown (<see cref="IsShowing"/>) and not on the air, unless the copy shows them as well.
        /// </para>
        /// </remarks>
        public bool? IsActive => ToNullable(Volatile.Read(ref this._active));

        /// <summary>
        /// Gets whether the input is shown in the user interface of OBS Studio: on the air (<see cref="IsActive"/>), or in the preview of studio mode, or in a projector, or in the window of its properties. It is null unless the model
        /// was asked to follow the activity (<see cref="ObsModelOptions.HighVolume"/> with <see cref="ObsHighVolume.Activity"/>).
        /// </summary>
        public bool? IsShowing => ToNullable(Volatile.Read(ref this._showing));

        private static bool? ToNullable(int value)
        {
            return value == Unknown ? null : value != 0;
        }

        internal bool SetActive(bool? value)
        {
            var wanted = value is null ? Unknown : (value.Value ? 1 : 0);
            if (Volatile.Read(ref this._active) == wanted)
            {
                return false;
            }

            Volatile.Write(ref this._active, wanted);
            return true;
        }

        internal bool SetShowing(bool? value)
        {
            var wanted = value is null ? Unknown : (value.Value ? 1 : 0);
            if (Volatile.Read(ref this._showing) == wanted)
            {
                return false;
            }

            Volatile.Write(ref this._showing, wanted);
            return true;
        }

        // The playback is made once, when the model knows that the input is a media input and what it does.
        internal ObsMedia AttachMedia(ObsMediaState state)
        {
            var media = new ObsMedia(this, state);
            Volatile.Write(ref this._media, media);
            return media;
        }

        // The values are written by one thread only, the one that runs the model, and read by any.
        internal bool SetMuted(bool muted)
        {
            if (Volatile.Read(ref this._muted) == muted)
            {
                return false;
            }

            Volatile.Write(ref this._muted, muted);
            return true;
        }

        internal bool SetVolumeMul(float value)
        {
            if (Volatile.Read(ref this._volumeMul) == value)
            {
                return false;
            }

            Volatile.Write(ref this._volumeMul, value);
            return true;
        }

        internal bool SetVolumeDb(float value)
        {
            if (Volatile.Read(ref this._volumeDb) == value)
            {
                return false;
            }

            Volatile.Write(ref this._volumeDb, value);
            return true;
        }

        internal bool SetAudioBalance(float value)
        {
            if (Volatile.Read(ref this._audioBalance) == value)
            {
                return false;
            }

            Volatile.Write(ref this._audioBalance, value);
            return true;
        }

        internal bool SetSyncOffset(int milliseconds)
        {
            if (Volatile.Read(ref this._syncOffset) == milliseconds)
            {
                return false;
            }

            Volatile.Write(ref this._syncOffset, milliseconds);
            return true;
        }

        internal bool SetMonitorType(ObsMonitorType value)
        {
            if (Volatile.Read(ref this._monitorType) == (int)value)
            {
                return false;
            }

            Volatile.Write(ref this._monitorType, (int)value);
            return true;
        }

        internal bool SetAudioTracks(int mask)
        {
            if (Volatile.Read(ref this._audioTracks) == mask)
            {
                return false;
            }

            Volatile.Write(ref this._audioTracks, mask);
            return true;
        }

        internal bool SetSettings(SettingsValue value)
        {
            var current = Volatile.Read(ref this._settings);
            if (ReferenceEquals(current, value) || current.Equals(value))
            {
                return false;
            }

            Volatile.Write(ref this._settings, value);
            return true;
        }
    }
}
