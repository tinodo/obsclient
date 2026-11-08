namespace OBSStudioClient.Exceptions
{
    using OBSStudioClient.Model;

    /// <summary>
    /// Represents an exception that is thrown when something is changed that OBS Studio removed: a scene, an input or a scene item that is not
    /// <see cref="ObsObjectState.Live"/> any more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reference to an object that was removed stays valid, and says so (<see cref="ObsSource.State"/>, <see cref="ObsSceneItem.IsRemoved"/>), but it can not be changed
    /// any more: the request is not sent. The one thing that is allowed on a removed scene or input that OBS Studio keeps alive (<see cref="ObsObjectState.Detached"/>)
    /// is to remove it for good, which frees its name. After OBS Studio has changed its scene collection every object that you held is <see cref="ObsObjectState.Gone"/>:
    /// look the object up again.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ObsRemovedException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsRemovedException"/> class.
        /// </summary>
        /// <param name="description">What it is, for example "scene 'Gaming'" or "scene item 3 of scene 'Gaming'".</param>
        /// <param name="state">What OBS Studio has done with it.</param>
        public ObsRemovedException(string description, ObsObjectState state)
            : base(BuildMessage(description, state))
        {
            this.Description = description;
            this.State = state;
        }

        /// <summary>
        /// Gets what the object is, for example "scene 'Gaming'".
        /// </summary>
        public string Description { get; } = string.Empty;

        /// <summary>
        /// Gets what OBS Studio has done with the object: <see cref="ObsObjectState.Detached"/> or <see cref="ObsObjectState.Gone"/>.
        /// </summary>
        public ObsObjectState State { get; }

        private static string BuildMessage(string description, ObsObjectState state)
        {
            return state == ObsObjectState.Detached
                ? $"The {description} was removed, and OBS Studio keeps it alive for the scene items that still use it. It can not be changed; only removing it for good is possible."
                : $"The {description} does not exist any more in OBS Studio (it was removed, or OBS Studio changed its scene collection). Look it up again.";
        }
    }
}
