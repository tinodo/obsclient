namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Event Types.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EventType
    {
        //General Events

        /// <summary>
        /// OBS has begun the shutdown process.
        /// </summary>
        ExitStarted,

        /// <summary>
        /// An event has been emitted from a vendor.
        /// </summary>
        VendorEvent,

        /// <summary>
        /// Custom event emitted by BroadcastCustomEvent.
        /// </summary>
        CustomEvent,

        //Config Events

        /// <summary>
        /// The current scene collection has begun changing.
        /// </summary>
        CurrentSceneCollectionChanging,

        /// <summary>
        /// The current scene collection has changed.
        /// </summary>
        CurrentSceneCollectionChanged,

        /// <summary>
        /// The scene collection list has changed.
        /// </summary>
        SceneCollectionListChanged,

        /// <summary>
        /// The current profile has begun changing.
        /// </summary>
        CurrentProfileChanging,

        /// <summary>
        /// The current profile has changed.
        /// </summary>
        CurrentProfileChanged,

        /// <summary>
        /// The profile list has changed.
        /// </summary>
        ProfileListChanged,

        //Scenes Events

        /// <summary>
        /// A new scene has been created.
        /// </summary>
        SceneCreated,

        /// <summary>
        /// A scene has been removed.
        /// </summary>
        SceneRemoved,

        /// <summary>
        /// The name of a scene has changed.
        /// </summary>
        SceneNameChanged,

        /// <summary>
        /// The current program scene has changed.
        /// </summary>
        CurrentProgramSceneChanged,

        /// <summary>
        /// The current preview scene has changed.
        /// </summary>
        CurrentPreviewSceneChanged,

        /// <summary>
        /// The list of scenes has changed.
        /// </summary>
        SceneListChanged,

        //Inputs Events

        /// <summary>
        /// An input has been created.
        /// </summary>
        InputCreated,

        /// <summary>
        /// An input has been removed.
        /// </summary>
        InputRemoved,

        /// <summary>
        /// The name of an input has changed.
        /// </summary>
        InputNameChanged,

        /// <summary>
        /// An input's active state has changed.
        /// </summary>
        InputActiveStateChanged,

        /// <summary>
        /// An input's show state has changed.
        /// </summary>
        InputShowStateChanged,

        /// <summary>
        /// An input's mute state has changed.
        /// </summary>
        InputMuteStateChanged,

        /// <summary>
        /// An input's volume level has changed.
        /// </summary>
        InputVolumeChanged,

        /// <summary>
        /// The audio balance value of an input has changed.
        /// </summary>
        InputAudioBalanceChanged,

        /// <summary>
        /// The sync offset of an input has changed.
        /// </summary>
        InputAudioSyncOffsetChanged,

        /// <summary>
        /// The audio tracks of an input have changed.
        /// </summary>
        InputAudioTracksChanged,

        /// <summary>
        /// The monitor type of an input has changed.
        /// </summary>
        InputAudioMonitorTypeChanged,

        /// <summary>
        /// A high-volume event providing volume levels of all active inputs every 50 milliseconds.
        /// </summary>
        InputVolumeMeters,

        //Transitions Events

        /// <summary>
        /// The current scene transition has changed.
        /// </summary>
        CurrentSceneTransitionChanged,

        /// <summary>
        /// The current scene transition duration has changed.
        /// </summary>
        CurrentSceneTransitionDurationChanged,

        /// <summary>
        /// A scene transition has started.
        /// </summary>
        SceneTransitionStarted,

        /// <summary>
        /// A scene transition has completed fully.
        /// </summary>
        SceneTransitionEnded,

        /// <summary>
        /// A scene transition's video has completed fully.
        /// </summary>
        SceneTransitionVideoEnded,

        //Filters Events

        /// <summary>
        /// A source's filter list has been reindexed.
        /// </summary>
        SourceFilterListReindexed,

        /// <summary>
        /// A filter has been added to a source.
        /// </summary>
        SourceFilterCreated,

        /// <summary>
        /// A filter has been removed from a source.
        /// </summary>
        SourceFilterRemoved,

        /// <summary>
        /// The name of a source filter has changed.
        /// </summary>
        SourceFilterNameChanged,

        /// <summary>
        /// A source filter's enable state has changed.
        /// </summary>
        SourceFilterEnableStateChanged,

        //Scene Items Events

        /// <summary>
        /// A scene item has been created.
        /// </summary>
        SceneItemCreated,

        /// <summary>
        /// A scene item has been removed.
        /// </summary>
        SceneItemRemoved,

        /// <summary>
        /// A scene's item list has been reindexed.
        /// </summary>
        SceneItemListReindexed,

        /// <summary>
        /// A scene item's enable state has changed.
        /// </summary>
        SceneItemEnableStateChanged,

        /// <summary>
        /// A scene item's lock state has changed.
        /// </summary>
        SceneItemLockStateChanged,

        /// <summary>
        /// A scene item has been selected in the Ui.
        /// </summary>
        SceneItemSelected,

        /// <summary>
        /// The transform/crop of a scene item has changed.
        /// </summary>
        SceneItemTransformChanged,

        //Outputs Events

        /// <summary>
        /// The state of the stream output has changed.
        /// </summary>
        StreamStateChanged,

        /// <summary>
        /// The state of the record output has changed.
        /// </summary>
        RecordStateChanged,

        /// <summary>
        /// The state of the replay buffer output has changed.
        /// </summary>
        ReplayBufferStateChanged,

        /// <summary>
        /// The state of the virtualcam output has changed.
        /// </summary>
        VirtualcamStateChanged,

        /// <summary>
        /// The replay buffer has been saved.
        /// </summary>
        ReplayBufferSaved,

        //Media Inputs Events

        /// <summary>
        /// A media input has started playing.
        /// </summary>
        MediaInputPlaybackStarted,

        /// <summary>
        /// A media input has finished playing.
        /// </summary>
        MediaInputPlaybackEnded,

        /// <summary>
        /// An action has been performed on an input.
        /// </summary>
        MediaInputActionTriggered,

        //Ui Events

        /// <summary>
        /// Studio mode has been enabled or disabled.
        /// </summary>
        StudioModeStateChanged,

        /// <summary>
        /// A screenshot has been saved.
        /// </summary>
        ScreenshotSaved,
    }
}
