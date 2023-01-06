namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Transition
    /// </summary>
    public class Transition
    {
        /// <summary>
        /// Gets a value indicating whether the transition is configurable.
        /// </summary>
        [JsonPropertyName("transitionConfigurable")]
        public bool TransitionConfigurable { get; }

        /// <summary>
        /// Gets a value indicating whether the transition is fixed.
        /// </summary>
        [JsonPropertyName("transitionFixed")]
        public bool TransitionFixed { get; }

        /// <summary>
        /// Gets the transition kind.
        /// </summary>
        [JsonPropertyName("transitionKind")]
        public string TransitionKind { get; }

        /// <summary>
        /// Gets the transition name.
        /// </summary>
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Transition"/> class.
        /// </summary>
        /// <param name="transitionConfigurable">A value indicating whether the transition is configurable.</param>
        /// <param name="transitionFixed">A value indicating whether the transition is fixed.</param>
        /// <param name="transitionKind">The transition kind.</param>
        /// <param name="transitionName">The transition name.</param>
        [JsonConstructor]
        public Transition(bool transitionConfigurable, bool transitionFixed, string transitionKind, string transitionName)
        {
            this.TransitionConfigurable = transitionConfigurable;
            this.TransitionFixed = transitionFixed;
            this.TransitionKind = transitionKind;
            this.TransitionName = transitionName;
        }
    }
}
