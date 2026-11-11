namespace OBSStudioClient.Model
{
    using OBSStudioClient.Classes;

    /// <summary>
    /// The level of the audio of one channel of an input, as OBS Studio measured it a moment ago (see <see cref="ObsAudioLevels"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The numbers are multipliers of the full scale: 1 is as loud as digital audio can be (0 dB), 0.5 is 6 dB lower, and the number goes above 1 when the volume of the input
    /// is turned up. A level that is shown as a bar is usually <see cref="PeakDb"/> or <see cref="MagnitudeDb"/>, between -100 (silence) and 0.
    /// </para>
    /// <para>
    /// OBS Studio measures the last chunk of audio that the input delivered (some tens of milliseconds) and says the result every 50 milliseconds, so the numbers jump from one
    /// message to the next: a meter that wants to look smooth keeps the highest peak and lets it fall.
    /// </para>
    /// </remarks>
    public readonly struct ObsChannelLevel : IEquatable<ObsChannelLevel>
    {
        internal ObsChannelLevel(float magnitude, float peak, float inputPeak)
        {
            this.Magnitude = magnitude;
            this.Peak = peak;
            this.InputPeak = inputPeak;
        }

        /// <summary>
        /// Gets the magnitude: the root mean square of the samples of the last chunk of audio, with the volume of the input applied. It is 0 while the input is muted. It is the number that
        /// tells how loud the audio sounds.
        /// </summary>
        public float Magnitude { get; }

        /// <summary>
        /// Gets the peak: the highest sample of the last chunk of audio, with the volume of the input applied. It is 0 while the input is muted.
        /// </summary>
        /// <remarks>
        /// OBS Studio can say a peak that is far too high in the first message that has audio of an input that just came on the air (1.9 was measured often, and 2.7e20 once), so a meter should limit it.
        /// </remarks>
        public float Peak { get; }

        /// <summary>
        /// Gets the peak of the audio as the input made it: before the volume of the input and its mute are applied. It is what a meter that has to show whether the source is too loud looks at,
        /// whatever the volume is.
        /// </summary>
        /// <remarks>It has the same limit as <see cref="Peak"/>: the first message can have a number that is far too high.</remarks>
        public float InputPeak { get; }

        /// <summary>
        /// Gets <see cref="Magnitude"/> in decibels: 0 is full scale, and silence is -100.
        /// </summary>
        public float MagnitudeDb => AudioMath.MulToDb(this.Magnitude);

        /// <summary>
        /// Gets <see cref="Peak"/> in decibels: 0 is full scale, and silence is -100.
        /// </summary>
        public float PeakDb => AudioMath.MulToDb(this.Peak);

        /// <summary>
        /// Gets <see cref="InputPeak"/> in decibels: 0 is full scale, and silence is -100.
        /// </summary>
        public float InputPeakDb => AudioMath.MulToDb(this.InputPeak);

        /// <summary>
        /// Determines whether two levels have the same numbers.
        /// </summary>
        public static bool operator ==(ObsChannelLevel left, ObsChannelLevel right) => left.Equals(right);

        /// <summary>
        /// Determines whether two levels have different numbers.
        /// </summary>
        public static bool operator !=(ObsChannelLevel left, ObsChannelLevel right) => !left.Equals(right);

        /// <inheritdoc/>
        public bool Equals(ObsChannelLevel other) => this.Magnitude.Equals(other.Magnitude) && this.Peak.Equals(other.Peak) && this.InputPeak.Equals(other.InputPeak);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ObsChannelLevel other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Magnitude, this.Peak, this.InputPeak);

        /// <summary>
        /// Gets the levels as text, in decibels.
        /// </summary>
        /// <returns>The magnitude, the peak and the input peak.</returns>
        public override string ToString() => FormattableString.Invariant($"magnitude {this.MagnitudeDb:N1} dB, peak {this.PeakDb:N1} dB, input peak {this.InputPeakDb:N1} dB");
    }
}
