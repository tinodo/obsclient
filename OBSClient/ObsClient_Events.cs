namespace OBSStudioClient
{
    using OBSStudioClient.Events;

    public partial class ObsClient
    {
        //
        //
        // General Events
        //
        //

        /// <summary>
        /// Occurs when OBS has begun the shutdown process.
        /// </summary>
        public event AsyncEventHandler? ExitStarted;

        /// <summary>
        /// Occurs when an event has been emitted from a vendor.
        /// </summary>
        /// <remarks>
        /// A vendor is a unique name registered by a third-party plugin or script, which allows for custom requests and events to be added to obs-websocket. If a plugin or script implements vendor requests or events, documentation is expected to be provided with them.
        /// </remarks>
        public event AsyncEventHandler<VendorEventEventArgs>? VendorEvent;

        /// <summary>
        /// Occurs when a custom event emitted by <see cref="BroadcastCustomEvent(object)"/>.
        /// </summary>
        public event AsyncEventHandler<CustomEventEventArgs>? CustomEvent;

        //
        //
        // Config Events
        //
        //

        /// <summary>
        /// Occurs when the current scene collection has begun changing.
        /// </summary>
        /// <remarks>
        /// Note: We recommend using this event to trigger a pause of all polling requests, as performing any requests during a scene collection change is considered undefined behavior and can cause crashes!
        /// </remarks>
        public event AsyncEventHandler<SceneCollectionNameEventArgs>? CurrentSceneCollectionChanging;

        /// <summary>
        /// Occurs when the current scene collection has changed.
        /// </summary>
        /// <remarks>
        /// Note: If polling has been paused during CurrentSceneCollectionChanging, this is the que to restart polling.
        /// </remarks>
        public event AsyncEventHandler<SceneCollectionNameEventArgs>? CurrentSceneCollectionChanged;

        /// <summary>
        /// Occurs when the scene collection list has changed.
        /// </summary>
        public event AsyncEventHandler<SceneCollectionListEventArgs>? SceneCollectionListChanged;

        /// <summary>
        /// Occurs when the current profile has begun changing.
        /// </summary>
        public event AsyncEventHandler<ProfileNameEventArgs>? CurrentProfileChanging;

        /// <summary>
        /// Occurs when the current profile has changed.
        /// </summary>
        public event AsyncEventHandler<ProfileNameEventArgs>? CurrentProfileChanged;

        /// <summary>
        /// Occurs when the profile list has changed.
        /// </summary>
        public event AsyncEventHandler<ProfileListEventArgs>? ProfileListChanged;

        //
        //
        // Scenes Events
        //
        //

        /// <summary>
        /// Occurs when a new scene has been created.
        /// </summary>
        public event AsyncEventHandler<SceneModifiedEventArgs>? SceneCreated;

        /// <summary>
        /// Occurs when a scene has been removed.
        /// </summary>
        public event AsyncEventHandler<SceneModifiedEventArgs>? SceneRemoved;

        /// <summary>
        /// Occurs when the name of a scene has changed.
        /// </summary>
        public event AsyncEventHandler<SceneNameChangedEventArgs>? SceneNameChanged;

        /// <summary>
        /// Occurs when the current program scene has changed.
        /// </summary>
        public event AsyncEventHandler<SceneNameEventArgs>? CurrentProgramSceneChanged;

        /// <summary>
        /// Occurs when the current preview scene has changed.
        /// </summary>
        public event AsyncEventHandler<SceneNameEventArgs>? CurrentPreviewSceneChanged;

        /// <summary>
        /// Occurs when the list of scenes has changed.
        /// </summary>
        public event AsyncEventHandler<SceneListEventArgs>? SceneListChanged;

        //
        //
        // Inputs Events
        //
        //

        /// <summary>
        /// Occurs when an input has been created.
        /// </summary>
        public event AsyncEventHandler<InputCreatedEventArgs>? InputCreated;

        /// <summary>
        /// Occurs when an input has been removed.
        /// </summary>
        public event AsyncEventHandler<InputNameEventArgs>? InputRemoved;

        /// <summary>
        /// Occurs when the name of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputNameChangedEventArgs>? InputNameChanged;

        /// <summary>
        /// Occurs when an input's active state has changed.
        /// </summary>
        /// <remarks>
        /// When an input is active, it means it's being shown by the program feed.
        /// </remarks>
        public event AsyncEventHandler<InputActiveStateChangedEventArgs>? InputActiveStateChanged;

        /// <summary>
        /// Occurs when an input's show state has changed.
        /// </summary>
        /// <remarks>
        /// When an input is showing, it means it's being shown by the preview or a dialog.
        /// </remarks>
        public event AsyncEventHandler<InputShowStateChangedEventArgs>? InputShowStateChanged;

        /// <summary>
        /// Occurs when an input's mute state has changed.
        /// </summary>
        public event AsyncEventHandler<InputMuteStateChangedEventArgs>? InputMuteStateChanged;

        /// <summary>
        /// Occurs when an input's volume level has changed.
        /// </summary>
        public event AsyncEventHandler<InputVolumeChangedEventArgs>? InputVolumeChanged;

        /// <summary>
        /// Occurs when the audio balance value of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioBalanceChangedEventArgs>? InputAudioBalanceChanged;

        /// <summary>
        /// Occurs when the sync offset of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioSyncOffsetChangedEventArgs>? InputAudioSyncOffsetChanged;

        /// <summary>
        /// Occurs when the audio tracks of an input have changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioTracksChangedEventArgs>? InputAudioTracksChanged;

        /// <summary>
        /// Occurs when the monitor type of an input has changed.
        /// </summary>
        public event AsyncEventHandler<InputAudioMonitorTypeChangedEventArgs>? InputAudioMonitorTypeChanged;

        /// <summary>
        /// Occurs every 50 milliseconds providing volume levels of all active inputs.
        /// </summary>
        public event AsyncEventHandler<InputVolumeMetersEventArgs>? InputVolumeMeters;

        //
        //
        // Transitions Events
        //
        //

        /// <summary>
        /// Occurs when the current scene transition has changed.
        /// </summary>
        public event AsyncEventHandler<TransitionNameEventArgs>? CurrentSceneTransitionChanged;

        /// <summary>
        /// Occurs when the current scene transition duration has changed.
        /// </summary>
        public event AsyncEventHandler<TransitionDurationEventArgs>? CurrentSceneTransitionDurationChanged;

        /// <summary>
        /// Occurs when a scene transition has started.
        /// </summary>
        public event AsyncEventHandler<TransitionNameEventArgs>? SceneTransitionStarted;

        /// <summary>
        /// Occurs when a scene transition has completed fully.
        /// </summary>
        /// <remarks>
        /// Note: Does not appear to trigger when the transition is interrupted by the user.
        /// </remarks>
        public event AsyncEventHandler<TransitionNameEventArgs>? SceneTransitionEnded;

        /// <summary>
        /// Occurs when a scene transition's video has completed fully.
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
        /// Occurs when a source's filter list has been reindexed.
        /// </summary>
        public event AsyncEventHandler<SourceFiltersEventArgs>? SourceFilterListReindexed;

        /// <summary>
        /// Occurs when a filter has been added to a source.
        /// </summary>
        public event AsyncEventHandler<SourceFilterCreatedEventArgs>? SourceFilterCreated;

        /// <summary>
        /// Occurs when a filter has been removed from a source.
        /// </summary>
        public event AsyncEventHandler<SourceFilterRemovedEventArgs>? SourceFilterRemoved;

        /// <summary>
        /// Occurs when the name of a source filter has changed.
        /// </summary>
        public event AsyncEventHandler<SourceFilterNameChangedEventArgs>? SourceFilterNameChanged;

        /// <summary>
        /// Occurs when a source filter's enable state has changed.
        /// </summary>
        public event AsyncEventHandler<SourceFilterEnableStateChangedEventArgs>? SourceFilterEnableStateChanged;

        //
        //
        // Scene Items Events
        //
        //

        /// <summary>
        /// Occurs when a scene item has been created.
        /// </summary>
        public event AsyncEventHandler<SceneItemCreatedEventArgs>? SceneItemCreated;

        /// <summary>
        /// Occurs when a scene item has been removed.
        /// </summary>
        public event AsyncEventHandler<SceneItemRemovedEventArgs>? SceneItemRemoved;

        /// <summary>
        /// Occurs when a scene's item list has been reindexed.
        /// </summary>
        public event AsyncEventHandler<SceneItemListReindexedEventArgs>? SceneItemListReindexed;

        /// <summary>
        /// Occurs when a scene item's enable state has changed.
        /// </summary>
        public event AsyncEventHandler<SceneItemEnableStateChangedEventArgs>? SceneItemEnableStateChanged;

        /// <summary>
        /// Occurs when a scene item's lock state has changed.
        /// </summary>
        public event AsyncEventHandler<SceneItemLockStateChangedEventArgs>? SceneItemLockStateChanged;

        /// <summary>
        /// Occurs when a scene item has been selected in the Ui.
        /// </summary>
        public event AsyncEventHandler<SceneItemSelectedEventArgs>? SceneItemSelected;

        /// <summary>
        /// Occurs when the transform/crop of a scene item has changed.
        /// </summary>
        public event AsyncEventHandler<SceneItemTransformChangedEventArgs>? SceneItemTransformChanged;

        //
        //
        // Outputs Events
        //
        //

        /// <summary>
        /// Occurs when the state of the stream output has changed.
        /// </summary>
        public event AsyncEventHandler<OutputStateChangedEventArgs>? StreamStateChanged;

        /// <summary>
        /// Occurs when the state of the record output has changed.
        /// </summary>
        public event AsyncEventHandler<RecordStateChangedEventArgs>? RecordStateChanged;

        /// <summary>
        /// Occurs when the state of the replay buffer output has changed.
        /// </summary>
        public event AsyncEventHandler<OutputStateChangedEventArgs>? ReplayBufferStateChanged;

        /// <summary>
        /// Occurs when the state of the virtualcam output has changed.
        /// </summary>
        public event AsyncEventHandler<OutputStateChangedEventArgs>? VirtualcamStateChanged;

        /// <summary>
        /// Occurs when the replay buffer has been saved.
        /// </summary>
        public event AsyncEventHandler<ReplayBufferSavedEventArgs>? ReplayBufferSaved;

        //
        //
        // Media Inputs Events
        //
        //

        /// <summary>
        /// Occurs when a media input has started playing.
        /// </summary>
        public event AsyncEventHandler<InputNameEventArgs>? MediaInputPlaybackStarted;

        /// <summary>
        /// Occurs when a media input has finished playing.
        /// </summary>
        public event AsyncEventHandler<InputNameEventArgs>? MediaInputPlaybackEnded;

        /// <summary>
        /// Occurs when an action has been performed on an input.
        /// </summary>
        public event AsyncEventHandler<MediaInputActionTriggeredEventArgs>? MediaInputActionTriggered;

        //
        //
        // Ui Events
        //
        //

        /// <summary>
        /// Occurs when Studio mode has been enabled or disabled.
        /// </summary>
        public event AsyncEventHandler<StudioModeStateChangedEventArgs>? StudioModeStateChanged;

        /// <summary>
        /// Occurs when a screenshot has been saved.
        /// </summary>
        /// <remarks>
        /// Note: Triggered for the screenshot feature available in Settings -> Hotkeys -> Screenshot Output ONLY. Applications using Get/SaveSourceScreenshot should implement a CustomEvent if this kind of inter-client communication is desired.
        /// </remarks>
        public event AsyncEventHandler<ScreenshotSavedEventArgs>? ScreenshotSaved;
    }
}
