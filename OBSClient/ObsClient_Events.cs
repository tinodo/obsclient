namespace OBSStudioClient
{
    using OBSStudioClient.Events;

#pragma warning disable CS0067 // The event handlers are called through a MulticastDelegate, which is not detected by the compiler.
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
        public event EventHandler? ExitStarted;

        /// <summary>
        /// Occurs when an event has been emitted from a vendor.
        /// </summary>
        /// <remarks>
        /// A vendor is a unique name registered by a third-party plugin or script, which allows for custom requests and events to be added to obs-websocket. If a plugin or script implements vendor requests or events, documentation is expected to be provided with them.
        /// </remarks>
        public event EventHandler<VendorEventEventArgs>? VendorEvent;

        /// <summary>
        /// Occurs when a custom event emitted by <see cref="BroadcastCustomEvent"/>.
        /// </summary>
        public event EventHandler<CustomEventEventArgs>? CustomEvent;

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
        public event EventHandler<SceneCollectionNameEventArgs>? CurrentSceneCollectionChanging;

        /// <summary>
        /// Occurs when the current scene collection has changed.
        /// </summary>
        /// <remarks>
        /// Note: If polling has been paused during CurrentSceneCollectionChanging, this is the que to restart polling.
        /// </remarks>
        public event EventHandler<SceneCollectionNameEventArgs>? CurrentSceneCollectionChanged;

        /// <summary>
        /// Occurs when the scene collection list has changed.
        /// </summary>
        public event EventHandler<SceneCollectionListEventArgs>? SceneCollectionListChanged;

        /// <summary>
        /// Occurs when the current profile has begun changing.
        /// </summary>
        public event EventHandler<ProfileNameEventArgs>? CurrentProfileChanging;

        /// <summary>
        /// Occurs when the current profile has changed.
        /// </summary>
        public event EventHandler<ProfileNameEventArgs>? CurrentProfileChanged;

        /// <summary>
        /// Occurs when the profile list has changed.
        /// </summary>
        public event EventHandler<ProfileListEventArgs>? ProfileListChanged;

        //
        //
        // Scenes Events
        //
        //

        /// <summary>
        /// Occurs when a new scene has been created.
        /// </summary>
        public event EventHandler<SceneModifiedEventArgs>? SceneCreated;

        /// <summary>
        /// Occurs when a scene has been removed.
        /// </summary>
        public event EventHandler<SceneModifiedEventArgs>? SceneRemoved;

        /// <summary>
        /// Occurs when the name of a scene has changed.
        /// </summary>
        public event EventHandler<SceneNameChangedEventArgs>? SceneNameChanged;

        /// <summary>
        /// Occurs when the current program scene has changed.
        /// </summary>
        public event EventHandler<SceneNameEventArgs>? CurrentProgramSceneChanged;

        /// <summary>
        /// Occurs when the current preview scene has changed.
        /// </summary>
        public event EventHandler<SceneNameEventArgs>? CurrentPreviewSceneChanged;

        /// <summary>
        /// Occurs when the list of scenes has changed.
        /// </summary>
        public event EventHandler<SceneListEventArgs>? SceneListChanged;

        //
        //
        // Inputs Events
        //
        //

        /// <summary>
        /// Occurs when an input has been created.
        /// </summary>
        public event EventHandler<InputCreatedEventArgs>? InputCreated;

        /// <summary>
        /// Occurs when an input has been removed.
        /// </summary>
        public event EventHandler<InputNameEventArgs>? InputRemoved;

        /// <summary>
        /// Occurs when the name of an input has changed.
        /// </summary>
        public event EventHandler<InputNameChangedEventArgs>? InputNameChanged;

        /// <summary>
        /// Occurs when an input's active state has changed.
        /// </summary>
        /// <remarks>
        /// When an input is active, it means it's being shown by the program feed.
        /// </remarks>
        public event EventHandler<InputActiveStateChangedEventArgs>? InputActiveStateChanged;

        /// <summary>
        /// Occurs when an input's show state has changed.
        /// </summary>
        /// <remarks>
        /// When an input is showing, it means it's being shown by the preview or a dialog.
        /// </remarks>
        public event EventHandler<InputShowStateChangedEventArgs>? InputShowStateChanged;

        /// <summary>
        /// Occurs when an input's mute state has changed.
        /// </summary>
        public event EventHandler<InputMuteStateChangedEventArgs>? InputMuteStateChanged;

        /// <summary>
        /// Occurs when an input's volume level has changed.
        /// </summary>
        public event EventHandler<InputVolumeChangedEventArgs>? InputVolumeChanged;

        /// <summary>
        /// Occurs when the audio balance value of an input has changed.
        /// </summary>
        public event EventHandler<InputAudioBalanceChangedEventArgs>? InputAudioBalanceChanged;

        /// <summary>
        /// Occurs when the sync offset of an input has changed.
        /// </summary>
        public event EventHandler<InputAudioSyncOffsetChangedEventArgs>? InputAudioSyncOffsetChanged;

        /// <summary>
        /// Occurs when the audio tracks of an input have changed.
        /// </summary>
        public event EventHandler<InputAudioTracksChangedEventArgs>? InputAudioTracksChanged;

        /// <summary>
        /// Occurs when the monitor type of an input has changed.
        /// </summary>
        public event EventHandler<InputAudioMonitorTypeChangedEventArgs>? InputAudioMonitorTypeChanged;

        /// <summary>
        /// Occurs every 50 milliseconds providing volume levels of all active inputs.
        /// </summary>
        public event EventHandler<InputVolumeMetersEventArgs>? InputVolumeMeters;

        //
        //
        // Transitions Events
        //
        //

        /// <summary>
        /// Occurs when the current scene transition has changed.
        /// </summary>
        public event EventHandler<TransitionNameEventArgs>? CurrentSceneTransitionChanged;

        /// <summary>
        /// Occurs when the current scene transition duration has changed.
        /// </summary>
        public event EventHandler<TransitionDurationEventArgs>? CurrentSceneTransitionDurationChanged;

        /// <summary>
        /// Occurs when a scene transition has started.
        /// </summary>
        public event EventHandler<TransitionNameEventArgs>? SceneTransitionStarted;

        /// <summary>
        /// Occurs when a scene transition has completed fully.
        /// </summary>
        /// <remarks>
        /// Note: Does not appear to trigger when the transition is interrupted by the user.
        /// </remarks>
        public event EventHandler<TransitionNameEventArgs>? SceneTransitionEnded;

        /// <summary>
        /// Occurs when a scene transition's video has completed fully.
        /// </summary>
        /// <remarks>
        /// Useful for stinger transitions to tell when the video actually ends. SceneTransitionEnded only signifies the cut point, not the completion of transition playback.
        /// Note: Appears to be called by every transition, regardless of relevance.
        /// </remarks>
        public event EventHandler<TransitionNameEventArgs>? SceneTransitionVideoEnded;

        //
        //
        // Filters Events
        //
        //

        /// <summary>
        /// Occurs when a source's filter list has been reindexed.
        /// </summary>
        public event EventHandler<SourceFiltersEventArgs>? SourceFilterListReindexed;

        /// <summary>
        /// Occurs when a filter has been added to a source.
        /// </summary>
        public event EventHandler<SourceFilterCreatedEventArgs>? SourceFilterCreated;

        /// <summary>
        /// Occurs when a filter has been removed from a source.
        /// </summary>
        public event EventHandler<SourceFilterRemovedEventArgs>? SourceFilterRemoved;

        /// <summary>
        /// Occurs when the name of a source filter has changed.
        /// </summary>
        public event EventHandler<SourceFilterNameChangedEventArgs>? SourceFilterNameChanged;

        /// <summary>
        /// Occurs when a source filter's enable state has changed.
        /// </summary>
        public event EventHandler<SourceFilterEnableStateChangedEventArgs>? SourceFilterEnableStateChanged;

        //
        //
        // Scene Items Events
        //
        //

        /// <summary>
        /// Occurs when a scene item has been created.
        /// </summary>
        public event EventHandler<SceneItemCreatedEventArgs>? SceneItemCreated;

        /// <summary>
        /// Occurs when a scene item has been removed.
        /// </summary>
        public event EventHandler<SceneItemRemovedEventArgs>? SceneItemRemoved;

        /// <summary>
        /// Occurs when a scene's item list has been reindexed.
        /// </summary>
        public event EventHandler<SceneItemListReindexedEventArgs>? SceneItemListReindexed;

        /// <summary>
        /// Occurs when a scene item's enable state has changed.
        /// </summary>
        public event EventHandler<SceneItemEnableStateChangedEventArgs>? SceneItemEnableStateChanged;

        /// <summary>
        /// Occurs when a scene item's lock state has changed.
        /// </summary>
        public event EventHandler<SceneItemLockStateChangedEventArgs>? SceneItemLockStateChanged;

        /// <summary>
        /// Occurs when a scene item has been selected in the Ui.
        /// </summary>
        public event EventHandler<SceneItemSelectedEventArgs>? SceneItemSelected;

        /// <summary>
        /// Occurs when the transform/crop of a scene item has changed.
        /// </summary>
        public event EventHandler<SceneItemTransformChangedEventArgs>? SceneItemTransformChanged;

        //
        //
        // Outputs Events
        //
        //

        /// <summary>
        /// Occurs when the state of the stream output has changed.
        /// </summary>
        public event EventHandler<OutputStateChangedEventArgs>? StreamStateChanged;

        /// <summary>
        /// Occurs when the state of the record output has changed.
        /// </summary>
        public event EventHandler<RecordStateChangedEventArgs>? RecordStateChanged;

        /// <summary>
        /// Occurs when the state of the replay buffer output has changed.
        /// </summary>
        public event EventHandler<OutputStateChangedEventArgs>? ReplayBufferStateChanged;

        /// <summary>
        /// Occurs when the state of the virtualcam output has changed.
        /// </summary>
        public event EventHandler<OutputStateChangedEventArgs>? VirtualcamStateChanged;

        /// <summary>
        /// Occurs when the replay buffer has been saved.
        /// </summary>
        public event EventHandler<ReplayBufferSavedEventArgs>? ReplayBufferSaved;

        //
        //
        // Media Inputs Events
        //
        //

        /// <summary>
        /// Occurs when a media input has started playing.
        /// </summary>
        public event EventHandler<InputNameEventArgs>? MediaInputPlaybackStarted;

        /// <summary>
        /// Occurs when a media input has finished playing.
        /// </summary>
        public event EventHandler<InputNameEventArgs>? MediaInputPlaybackEnded;

        /// <summary>
        /// Occurs when an action has been performed on an input.
        /// </summary>
        public event EventHandler<MediaInputActionTriggeredEventArgs>? MediaInputActionTriggered;

        //
        //
        // Ui Events
        //
        //

        /// <summary>
        /// Occurs when Studio mode has been enabled or disabled.
        /// </summary>
        public event EventHandler<StudioModeStateChangedEventArgs>? StudioModeStateChanged;

        /// <summary>
        /// Occurs when a screenshot has been saved.
        /// </summary>
        /// <remarks>
        /// Note: Triggered for the screenshot feature available in Settings -> Hotkeys -> Screenshot Output ONLY. Applications using Get/SaveSourceScreenshot should implement a CustomEvent if this kind of inter-client communication is desired.
        /// </remarks>
        public event EventHandler<ScreenshotSavedEventArgs>? ScreenshotSaved;
    }
#pragma warning restore CS0067 // The event 'ObsClient.ExitStarted' is never used

}
