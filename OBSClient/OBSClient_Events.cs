namespace OBSStudioClient
{
    using OBSStudioClient.Events;
    using OBSStudioClient.Messages;

    public partial class OBSClient
    {
        //
        //
        // General Events
        //
        //

        /// <summary>
        /// OBS has begun the shutdown process.
        /// </summary>
        public event AsyncEventHandler? ExitStarted;

        /// <summary>
        /// An event has been emitted from a vendor.
        /// </summary>
        /// <remarks>
        /// A vendor is a unique name registered by a third-party plugin or script, which allows for custom requests and events to be added to obs-websocket. If a plugin or script implements vendor requests or events, documentation is expected to be provided with them.
        /// </remarks>
        public event AsyncEventHandler<VendorEventEventArgs>? VendorEvent;

        /// <summary>
        /// Custom event emitted by <see cref="BroadcastCustomEvent(object)"/>.
        /// </summary>
        public event AsyncEventHandler<CustomEventEventArgs>? CustomEvent;

        //
        //
        // Config Events
        //
        //

        /// <summary>
        /// he current scene collection has begun changing.
        /// </summary>
        /// <remarks>
        /// Note: We recommend using this event to trigger a pause of all polling requests, as performing any requests during a scene collection change is considered undefined behavior and can cause crashes!
        /// </remarks>
        public event AsyncEventHandler<SceneCollectionNameEventArgs>? CurrentSceneCollectionChanging;

        /// <summary>
        /// The current scene collection has changed.
        /// </summary>
        /// <remarks>
        /// Note: If polling has been paused during CurrentSceneCollectionChanging, this is the que to restart polling.
        /// </remarks>
        public event AsyncEventHandler<SceneCollectionNameEventArgs>? CurrentSceneCollectionChanged;

        /// <summary>
        /// The scene collection list has changed.
        /// </summary>
        public event AsyncEventHandler<SceneCollectionListEventArgs>? SceneCollectionListChanged;

        /// <summary>
        /// The current profile has begun changing.
        /// </summary>
        public event AsyncEventHandler<ProfileNameEventArgs>? CurrentProfileChanging;

        /// <summary>
        /// The current profile has changed.
        /// </summary>
        public event AsyncEventHandler<ProfileNameEventArgs>? CurrentProfileChanged;

        /// <summary>
        /// The profile list has changed.
        /// </summary>
        public event AsyncEventHandler<ProfileListEventArgs>? ProfileListChanged;

        //
        //
        // Scenes Events
        //
        //

        /// <summary>
        /// A new scene has been created.
        /// </summary>
        public event AsyncEventHandler<SceneModifiedEventArgs>? SceneCreated;

        /// <summary>
        /// A scene has been removed.
        /// </summary>
        public event AsyncEventHandler<SceneModifiedEventArgs>? SceneRemoved;

        /// <summary>
        /// The name of a scene has changed.
        /// </summary>
        public event AsyncEventHandler<SceneNameChangedEventArgs>? SceneNameChanged;

        /// <summary>
        /// The current program scene has changed.
        /// </summary>
        public event AsyncEventHandler<SceneNameEventArgs>? CurrentProgramSceneChanged;

        /// <summary>
        /// The current preview scene has changed.
        /// </summary>
        public event AsyncEventHandler<SceneNameEventArgs>? CurrentPreviewSceneChanged;

        /// <summary>
        /// The list of scenes has changed.
        /// </summary>
        /// <remarks>
        /// TODO: Make OBS fire this event when scenes are reordered.
        /// </remarks>
        public event AsyncEventHandler<SceneListEventArgs>? SceneListChanged;

        //
        //
        // Inputs Events
        //
        //

        /// <summary>
        /// An input has been created.
        /// </summary>
        public event AsyncEventHandler<InputCreatedEventArgs>? InputCreated;

        /// <summary>
        /// An input has been removed.
        /// </summary>
        public event AsyncEventHandler<InputNameEventArgs>? InputRemoved;

        /// <summary>
        /// The name of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputNameChangedEventArgs>? InputNameChanged;

        /// <summary>
        /// An input's active state has changed.
        /// </summary>
        /// <remarks>
        /// When an input is active, it means it's being shown by the program feed.
        /// </remarks>
        public event AsyncEventHandler<InputActiveStateChangedEventArgs>? InputActiveStateChanged;

        /// <summary>
        /// An input's show state has changed.
        /// </summary>
        /// <remarks>
        /// When an input is showing, it means it's being shown by the preview or a dialog.
        /// </remarks>
        public event AsyncEventHandler<InputShowStateChangedEventArgs>? InputShowStateChanged;

        /// <summary>
        /// An input's mute state has changed.
        /// </summary>
        public event AsyncEventHandler<InputMuteStateChangedEventArgs>? InputMuteStateChanged;

        /// <summary>
        /// An input's volume level has changed.
        /// </summary>
        public event AsyncEventHandler<InputVolumeChangedEventArgs>? InputVolumeChanged;

        /// <summary>
        /// The audio balance value of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioBalanceChangedEventArgs>? InputAudioBalanceChanged;

        /// <summary>
        /// The sync offset of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioSyncOffsetChangedEventArgs>? InputAudioSyncOffsetChanged;

        /// <summary>
        /// The audio tracks of an input have changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioTracksChangedEventArgs>? InputAudioTracksChanged;

        /// <summary>
        /// The monitor type of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioMonitorTypeChangedEventArgs>? InputAudioMonitorTypeChanged;

        /// <summary>
        /// A high-volume event providing volume levels of all active inputs every 50 milliseconds.
        /// </summary>
        public event AsyncEventHandler<InputVolumeMetersEventArgs>? InputVolumeMeters;

        //
        //
        // Transitions Events
        //
        //

        /// <summary>
        /// The current scene transition has changed.
        /// </summary>
        public event AsyncEventHandler<TransitionNameEventArgs>? CurrentSceneTransitionChanged;

        /// <summary>
        /// The current scene transition duration has changed.
        /// </summary>
        public event AsyncEventHandler<TransitionDurationEventArgs>? CurrentSceneTransitionDurationChanged;

        /// <summary>
        /// A scene transition has started.
        /// </summary>
        public event AsyncEventHandler<TransitionNameEventArgs>? SceneTransitionStarted;

        /// <summary>
        /// A scene transition has completed fully.
        /// </summary>
        /// <remarks>
        /// Note: Does not appear to trigger when the transition is interrupted by the user.
        /// </remarks>
        public event AsyncEventHandler<TransitionNameEventArgs>? SceneTransitionEnded;

        /// <summary>
        /// A scene transition's video has completed fully.
        /// </summary>
        /// <remarks>
        /// Useful for stinger transitions to tell when the video actually ends. SceneTransitionEnded only signifies the cut point, not the completion of transition playback.
        /// Note: Appears to be called by every transition, regardless of relevance.
        /// </remarks>
        public event AsyncEventHandler<TransitionNameEventArgs>? SceneTransitionVideoEnded;

        //
        //
        // Filters Events
        //
        //

        /// <summary>
        /// A source's filter list has been reindexed.
        /// </summary>
        public event AsyncEventHandler<SourceFiltersEventArgs>? SourceFilterListReindexed;

        /// <summary>
        /// A filter has been added to a source.
        /// </summary>
        public event AsyncEventHandler<SourceFilterCreatedEventArgs>? SourceFilterCreated;

        /// <summary>
        /// A filter has been removed from a source.
        /// </summary>
        public event AsyncEventHandler<SourceFilterRemovedEventArgs>? SourceFilterRemoved;

        /// <summary>
        /// The name of a source filter has changed.
        /// </summary>
        public event AsyncEventHandler<SourceFilterNameChangedEventArgs>? SourceFilterNameChanged;

        /// <summary>
        /// A source filter's enable state has changed.
        /// </summary>
        public event AsyncEventHandler<SourceFilterEnableStateChangedEventArgs>? SourceFilterEnableStateChanged;

        //
        //
        // Scene Items Events
        //
        //

        /// <summary>
        /// A scene item has been created.
        /// </summary>
        public event AsyncEventHandler<SceneItemCreatedEventArgs>? SceneItemCreated;

        /// <summary>
        /// A scene item has been removed.
        /// </summary>
        public event AsyncEventHandler<SceneItemRemovedEventArgs>? SceneItemRemoved;

        /// <summary>
        /// A scene's item list has been reindexed.
        /// </summary>
        public event AsyncEventHandler<SceneItemListReindexedEventArgs>? SceneItemListReindexed;

        /// <summary>
        /// A scene item's enable state has changed.
        /// </summary>
        public event AsyncEventHandler<SceneItemEnableStateChangedEventArgs>? SceneItemEnableStateChanged;

        /// <summary>
        /// A scene item's lock state has changed.
        /// </summary>
        public event AsyncEventHandler<SceneItemLockStateChangedEventArgs>? SceneItemLockStateChanged;

        /// <summary>
        /// A scene item has been selected in the Ui.
        /// </summary>
        public event AsyncEventHandler<SceneItemSelectedEventArgs>? SceneItemSelected;

        /// <summary>
        /// The transform/crop of a scene item has changed.
        /// </summary>
        public event AsyncEventHandler<SceneItemTransformChangedEventArgs>? SceneItemTransformChanged;

        //
        //
        // Outputs Events
        //
        //

        /// <summary>
        /// The state of the stream output has changed.
        /// </summary>
        public event AsyncEventHandler<OutputStateChangedEventArgs>? StreamStateChanged;

        /// <summary>
        /// The state of the record output has changed.
        /// </summary>
        public event AsyncEventHandler<RecordStateChangedEventArgs>? RecordStateChanged;

        /// <summary>
        /// The state of the replay buffer output has changed.
        /// </summary>
        public event AsyncEventHandler<OutputStateChangedEventArgs>? ReplayBufferStateChanged;

        /// <summary>
        /// The state of the virtualcam output has changed.
        /// </summary>
        public event AsyncEventHandler<OutputStateChangedEventArgs>? VirtualcamStateChanged;

        /// <summary>
        /// The replay buffer has been saved.
        /// </summary>
        public event AsyncEventHandler<ReplayBufferSavedEventArgs>? ReplayBufferSaved;

        //
        //
        // Media Inputs Events
        //
        //

        /// <summary>
        /// A media input has started playing.
        /// </summary>
        public event AsyncEventHandler<InputNameEventArgs>? MediaInputPlaybackStarted;

        /// <summary>
        /// A media input has finished playing.
        /// </summary>
        public event AsyncEventHandler<InputNameEventArgs>? MediaInputPlaybackEnded;

        /// <summary>
        /// An action has been performed on an input.
        /// </summary>
        public event AsyncEventHandler<MediaInputActionTriggeredEventArgs>? MediaInputActionTriggered;

        //
        //
        // Ui Events
        //
        //

        /// <summary>
        /// Studio mode has been enabled or disabled.
        /// </summary>
        public event AsyncEventHandler<StudioModeStateChangedEventArgs>? StudioModeStateChanged;

        /// <summary>
        /// A screenshot has been saved.
        /// </summary>
        /// <remarks>
        /// Note: Triggered for the screenshot feature available in Settings -> Hotkeys -> Screenshot Output ONLY. Applications using Get/SaveSourceScreenshot should implement a CustomEvent if this kind of inter-client communication is desired.
        /// </remarks>
        public event AsyncEventHandler<ScreenshotSavedEventArgs>? ScreenshotSaved;
    }
}
