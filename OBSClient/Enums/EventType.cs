namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EventType
    {
        //General Events
        ExitStarted,
        VendorEvent,
        CustomEvent,

        //Config Events
        CurrentSceneCollectionChanging,
        CurrentSceneCollectionChanged,
        SceneCollectionListChanged,
        CurrentProfileChanging,
        CurrentProfileChanged,
        ProfileListChanged,

        //Scenes Events
        SceneCreated,
        SceneRemoved,
        SceneNameChanged,
        CurrentProgramSceneChanged,
        CurrentPreviewSceneChanged,
        SceneListChanged,

        //Inputs Events
        InputCreated,
        InputRemoved,
        InputNameChanged,
        InputActiveStateChanged,
        InputShowStateChanged,
        InputMuteStateChanged,
        InputVolumeChanged,
        InputAudioBalanceChanged,
        InputAudioSyncOffsetChanged,
        InputAudioTracksChanged,
        InputAudioMonitorTypeChanged,
        InputVolumeMeters,

        //Transitions Events
        CurrentSceneTransitionChanged,
        CurrentSceneTransitionDurationChanged,
        SceneTransitionStarted,
        SceneTransitionEnded,
        SceneTransitionVideoEnded,

        //Filters Events
        SourceFilterListReindexed,
        SourceFilterCreated,
        SourceFilterRemoved,
        SourceFilterNameChanged,
        SourceFilterEnableStateChanged,

        //Scene Items Events
        SceneItemCreated,
        SceneItemRemoved,
        SceneItemListReindexed,
        SceneItemEnableStateChanged,
        SceneItemLockStateChanged,
        SceneItemSelected,
        SceneItemTransformChanged,

        //Outputs Events
        StreamStateChanged,
        RecordStateChanged,
        ReplayBufferStateChanged,
        VirtualcamStateChanged,
        ReplayBufferSaved,

        //Media Inputs Events
        MediaInputPlaybackStarted,
        MediaInputPlaybackEnded,
        MediaInputActionTriggered,

        //Ui Events
        StudioModeStateChanged,
        ScreenshotSaved,
    }
}
