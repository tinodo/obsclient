namespace OBSStudioClient.Exceptions
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;

    /// <summary>
    /// Represents an exception that is thrown when OBS Studio refuses to give a scene or an input a name because the name is taken (the status 601 of obs-websocket).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing was changed. The message says what holds the name, as far as the model knows: a scene, a group (a group holds its name like any source), an input, or an input that was
    /// removed and that OBS Studio keeps alive (<see cref="ObsObjectState.Detached"/>) because scene items still show it. The name of such an input stays taken until its last scene
    /// item is gone, so creating or renaming something to that name fails until the input is removed for good: <see cref="HeldBy"/> is the object to call <c>RemoveAsync</c> on (unless a scene
    /// item inside a group shows it: <c>RemoveAsync</c> refuses that, and it has to be taken out of the group in OBS Studio first). With OBS Studio 31.0 and older a scene that was removed and is
    /// alive holds its name in the same way, and so does a group (a group is destroyed with the scene item that shows it, and <c>RemoveAsync</c> refuses a group: call it on that scene item);
    /// OBS Studio 31.1 and newer free the name of a scene at once.
    /// </para>
    /// <para>
    /// The names of scenes and inputs are not alike. An input can not have the name of a scene or of another input (also not of an input that was removed and is alive). A scene can
    /// not have the name of another scene, and whether it can have the name of an input depends on the version of OBS Studio: obs-websocket 5.7 and newer allows it, and obs-websocket
    /// 5.6 and older refuse the name of any source (the message says so when that is what happened). The names are case sensitive.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ObsNameInUseException : ObsResponseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNameInUseException"/> class.
        /// </summary>
        /// <param name="name">The name that was refused.</param>
        /// <param name="message">What was refused, and what holds the name.</param>
        /// <param name="heldBy">The object that holds the name according to the model, or null when the model does not show it.</param>
        public ObsNameInUseException(string name, string message, ObsSource? heldBy)
            : base(RequestStatusCode.ResourceAlreadyExists, message)
        {
            this.Name = name;
            this.HeldBy = heldBy;
        }

        /// <summary>
        /// Gets the name that OBS Studio refused.
        /// </summary>
        public string Name { get; } = string.Empty;

        /// <summary>
        /// Gets the object that holds the name, as far as the model knows: a scene, a group, an input, or an input that was removed and that OBS Studio keeps alive. It is null when the model
        /// does not show what holds the name (it can be behind OBS Studio by a few milliseconds).
        /// </summary>
        /// <remarks>
        /// When it is a source that is <see cref="ObsObjectState.Detached"/>, <c>RemoveAsync</c> on it removes the scene items that keep it alive, so that OBS Studio destroys it and
        /// the name is free (not for a group: <c>RemoveAsync</c> refuses a group, which is destroyed with the scene item that shows it, and the message names that scene item).
        /// </remarks>
        public ObsSource? HeldBy { get; }
    }
}
