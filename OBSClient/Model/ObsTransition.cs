namespace OBSStudioClient.Model
{
    /// <summary>
    /// A scene transition of OBS Studio: how a scene gives way to another one. It tells what the transition is called, what kind it is, and whether it has a duration of its own and settings.
    /// </summary>
    /// <remarks>
    /// An <see cref="ObsTransition"/> is a snapshot: it does not change, and <see cref="ObsTransitions.Current"/> is another object when the current transition is another one (or the same one
    /// with other values).
    /// </remarks>
    public sealed class ObsTransition
    {
        internal ObsTransition(string name, Guid uuid, string kind, bool isFixed, bool isConfigurable)
        {
            this.Name = name;
            this.Uuid = uuid;
            this.Kind = kind;
            this.IsFixed = isFixed;
            this.IsConfigurable = isConfigurable;
        }

        /// <summary>
        /// Gets the name of the transition ("Cut", "Fade"). Names are generally unique, and OBS Studio does not guarantee it.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the UUID of the transition.
        /// </summary>
        public Guid Uuid { get; }

        /// <summary>
        /// Gets the kind of the transition (<c>cut_transition</c>, <c>fade_transition</c>, <c>swipe_transition</c>, ...): <see cref="ObsTransitions.GetKindsAsync"/> lists the kinds of this OBS Studio.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Gets whether the transition has no duration that can be set: a cut takes no time. OBS Studio accepts a duration for it all the same, and the transitions that have one use it.
        /// </summary>
        public bool IsFixed { get; }

        /// <summary>
        /// Gets whether the transition has settings (<see cref="ObsTransitions.GetSettingsAsync"/>): a swipe has a direction, a fade to a color has a color. OBS Studio refuses the settings of
        /// one that has none.
        /// </summary>
        public bool IsConfigurable { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Transition '{this.Name}' ({this.Kind})";
        }
    }
}
