namespace OBSStudioClient.Model
{
    /// <summary>
    /// The transition that a scene brings with it: when OBS Studio switches to the scene, it uses this transition (and duration) instead of the current ones.
    /// </summary>
    public sealed class ObsTransitionOverride
    {
        internal ObsTransitionOverride(string? name, TimeSpan? duration)
        {
            this.Name = name;
            this.Duration = duration;
        }

        /// <summary>
        /// Gets the name of the transition that is used for the scene, or null when the scene uses the current transition.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets the duration that is used for the scene, or null when it uses the duration that is set for the transitions.
        /// </summary>
        public TimeSpan? Duration { get; }

        /// <summary>
        /// Gets whether the scene has an override at all: a transition or a duration.
        /// </summary>
        public bool IsSet => this.Name is not null || this.Duration is not null;

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.IsSet
                ? $"Transition override: {this.Name ?? "(the current transition)"}, {(this.Duration is { } duration ? $"{duration.TotalMilliseconds:0} ms" : "(the current duration)")}"
                : "No transition override";
        }
    }
}
