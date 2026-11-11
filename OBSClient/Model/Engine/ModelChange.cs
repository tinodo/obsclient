namespace OBSStudioClient.Model.Engine
{
    /// <summary>
    /// What changed in the model.
    /// </summary>
    internal enum ModelChangeKind
    {
        /// <summary>The connection ended.</summary>
        Disconnected,

        /// <summary>Everything was read, and the model is up to date.</summary>
        Synchronized,

        /// <summary>A scene appeared.</summary>
        SceneAdded,

        /// <summary>A scene was removed, or is Detached now (see <see cref="SourceDetached"/>), or gone.</summary>
        SceneRemoved,

        /// <summary>A scene was renamed.</summary>
        SceneRenamed,

        /// <summary>The position of a scene in the list of scenes changed.</summary>
        SceneOrderChanged,

        /// <summary>An input appeared (it is complete).</summary>
        InputAdded,

        /// <summary>An input was removed, or is gone.</summary>
        InputRemoved,

        /// <summary>An input was renamed.</summary>
        InputRenamed,

        /// <summary>The mute state of an input changed.</summary>
        InputMuteChanged,

        /// <summary>The volume of an input changed.</summary>
        InputVolumeChanged,

        /// <summary>The settings of an input changed.</summary>
        InputSettingsChanged,

        /// <summary>The audio balance of an input changed.</summary>
        InputAudioBalanceChanged,

        /// <summary>The sync offset of an input changed.</summary>
        InputSyncOffsetChanged,

        /// <summary>The monitor type of an input changed.</summary>
        InputMonitorTypeChanged,

        /// <summary>The audio tracks of an input changed.</summary>
        InputTracksChanged,

        /// <summary>What a media input does (its playback state) changed.</summary>
        InputMediaStateChanged,

        /// <summary>A scene item appeared (it is complete).</summary>
        ItemAdded,

        /// <summary>A scene item was removed.</summary>
        ItemRemoved,

        /// <summary>A scene item was enabled or disabled.</summary>
        ItemEnabledChanged,

        /// <summary>A scene item was locked or unlocked.</summary>
        ItemLockedChanged,

        /// <summary>The position of a scene item in its scene changed.</summary>
        ItemIndexChanged,

        /// <summary>OBS removed a scene or input, and keeps it alive because scene items still use it.</summary>
        SourceDetached,

        /// <summary>The program scene changed.</summary>
        ProgramSceneChanged,

        /// <summary>Studio mode was switched on or off.</summary>
        StudioModeChanged,

        /// <summary>The preview scene changed (or there is none any more: studio mode was switched off).</summary>
        PreviewSceneChanged,

        /// <summary>The current scene transition changed.</summary>
        TransitionChanged,

        /// <summary>The duration of the scene transitions changed.</summary>
        TransitionDurationChanged,

        /// <summary>A scene transition started or ended.</summary>
        TransitionRunningChanged,

        /// <summary>An output (the stream, the recording, the virtual camera, the replay buffer) changed what it is doing; the text says which.</summary>
        OutputChanged,

        /// <summary>A filter appeared in a scene or an input (the subject); the text is its name.</summary>
        FilterAdded,

        /// <summary>A filter was removed from a scene or an input (the subject); the text is its name.</summary>
        FilterRemoved,

        /// <summary>A filter was renamed; the text is the new name.</summary>
        FilterRenamed,

        /// <summary>A filter was enabled or disabled; the text is its name.</summary>
        FilterEnabledChanged,

        /// <summary>The position of a filter changed; the text is its name.</summary>
        FilterIndexChanged,

        /// <summary>The model corrected itself: it held something that a read showed not to exist.</summary>
        Repaired,

        /// <summary>
        /// OBS Studio said something that is not a state of the model but happened: a screenshot was saved, a file was written, OBS Studio is exiting, somebody sent a custom event. The type of the
        /// event (an <c>EventType</c>) is the old value, the text is its name, and the data of the event is the new value. The application is told in the order in which the events arrived, also
        /// while the model is read.
        /// </summary>
        Notification,
    }

    /// <summary>
    /// One change of the model.
    /// </summary>
    /// <param name="Kind">What changed.</param>
    /// <param name="Subject">The UUID of the scene or input it is about, or <see cref="Guid.Empty"/>.</param>
    /// <param name="ItemId">The id of the scene item it is about, or 0.</param>
    /// <param name="Text">A name or a description, when that helps.</param>
    /// <param name="OldValue">The old value, for a change of a value.</param>
    /// <param name="NewValue">The new value, for a change of a value.</param>
    internal readonly record struct ModelChange(ModelChangeKind Kind, Guid Subject, int ItemId, string? Text, object? OldValue, object? NewValue);
}
