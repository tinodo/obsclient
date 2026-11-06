namespace OBSStudioClient.Studio
{
    /// <summary>
    /// An input of OBS Studio: a source of video, audio or both (a camera, a microphone, an image, a browser source, and so on).
    /// </summary>
    public sealed class ObsInput : ObsSource
    {
        private bool _muted;
        private float _volumeMul = 1f;
        private float _volumeDb;

        internal ObsInput(ObsStudio studio, Guid uuid, string name, ObsObjectState state, string kind, bool hasAudio)
            : base(studio, uuid, name, state)
        {
            this.Kind = kind;
            this.HasAudio = hasAudio;
        }

        /// <summary>
        /// Gets the kind of the input, for example <c>wasapi_input_capture</c> or <c>color_source_v3</c>.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Gets whether the input has audio. Only then are <see cref="IsMuted"/>, <see cref="VolumeMul"/> and <see cref="VolumeDb"/> meaningful.
        /// </summary>
        public bool HasAudio { get; }

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
    }
}
