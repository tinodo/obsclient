namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Transition
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Transition"/> class.
    /// </remarks>
    /// <param name="transitionConfigurable">A value indicating whether the transition is configurable.</param>
    /// <param name="transitionFixed">A value indicating whether the transition is fixed.</param>
    /// <param name="transitionKind">The transition kind.</param>
    /// <param name="transitionName">The transition name.</param>
    /// <param name="transitionUuid">The transition uuid.</param>
    [method: JsonConstructor]
    public class Transition(bool transitionConfigurable, bool transitionFixed, string transitionKind, string transitionName, Guid transitionUuid)
    {
        /// <summary>
        /// Gets a value indicating whether the transition is configurable.
        /// </summary>
        [JsonPropertyName("transitionConfigurable")]
        public bool TransitionConfigurable { get; } = transitionConfigurable;

        /// <summary>
        /// Gets a value indicating whether the transition is fixed.
        /// </summary>
        [JsonPropertyName("transitionFixed")]
        public bool TransitionFixed { get; } = transitionFixed;

        /// <summary>
        /// Gets the transition kind.
        /// </summary>
        [JsonPropertyName("transitionKind")]
        public string TransitionKind { get; } = transitionKind;

        /// <summary>
        /// Gets the transition name.
        /// </summary>
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; } = transitionName;

        /// <summary>
        /// Gets the transition name.
        /// </summary>
        [JsonPropertyName("transitionUuid")]
        public Guid TransitionUuid { get; } = transitionUuid;
    }
}
