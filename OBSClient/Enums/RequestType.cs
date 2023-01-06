namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request Type
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestType
    {
        //General Requests

        /// <summary>
        /// GetVersion Request
        /// </summary>
        GetVersion,

        /// <summary>
        /// GetStats Request
        /// </summary>
        GetStats,

        /// <summary>
        /// BroadcastCustomEvent Request
        /// </summary>
        BroadcastCustomEvent,

        /// <summary>
        /// CallVendorRequest Request
        /// </summary>
        CallVendorRequest,

        /// <summary>
        /// GetHotkeyList Request
        /// </summary>
        GetHotkeyList,

        /// <summary>
        /// TriggerHotkeyByName Request
        /// </summary>
        TriggerHotkeyByName,

        /// <summary>
        /// TriggerHotkeyByKeySequence Request
        /// </summary>
        TriggerHotkeyByKeySequence,

        /// <summary>
        /// Sleep Request
        /// </summary>
        Sleep,

        //Config Requests

        /// <summary>
        /// GetPersistentData Request
        /// </summary>
        GetPersistentData,

        /// <summary>
        /// SetPersistentData Request
        /// </summary>
        SetPersistentData,

        /// <summary>
        /// GetSceneCollectionList Request
        /// </summary>
        GetSceneCollectionList,

        /// <summary>
        /// SetCurrentSceneCollection Request
        /// </summary>
        SetCurrentSceneCollection,

        /// <summary>
        /// CreateSceneCollection Request
        /// </summary>
        CreateSceneCollection,

        /// <summary>
        /// GetProfileList Request
        /// </summary>
        GetProfileList,

        /// <summary>
        /// SetCurrentProfile Request
        /// </summary>
        SetCurrentProfile,

        /// <summary>
        /// CreateProfile Request
        /// </summary>
        CreateProfile,

        /// <summary>
        /// RemoveProfile Request
        /// </summary>
        RemoveProfile,

        /// <summary>
        /// GetProfileParameter Request
        /// </summary>
        GetProfileParameter,

        /// <summary>
        /// SetProfileParameter Request
        /// </summary>
        SetProfileParameter,

        /// <summary>
        /// GetVideoSettings Request
        /// </summary>
        GetVideoSettings,

        /// <summary>
        /// SetVideoSettings Request
        /// </summary>
        SetVideoSettings,

        /// <summary>
        /// GetStreamServiceSettings Request
        /// </summary>
        GetStreamServiceSettings,

        /// <summary>
        /// SetStreamServiceSettings Request
        /// </summary>
        SetStreamServiceSettings,

        /// <summary>
        /// GetRecordDirectory Request
        /// </summary>
        GetRecordDirectory,

        //Sources Requests

        /// <summary>
        /// GetSourceActive Request
        /// </summary>
        GetSourceActive,

        /// <summary>
        /// GetSourceScreenshot Request
        /// </summary>
        GetSourceScreenshot,

        /// <summary>
        /// SaveSourceScreenshot Request
        /// </summary>
        SaveSourceScreenshot,

        //Scenes Requests

        /// <summary>
        /// GetSceneList Request
        /// </summary>
        GetSceneList,

        /// <summary>
        /// GetGroupList Request
        /// </summary>
        GetGroupList,

        /// <summary>
        /// GetCurrentProgramScene Request
        /// </summary>
        GetCurrentProgramScene,

        /// <summary>
        /// SetCurrentProgramScene Request
        /// </summary>
        SetCurrentProgramScene,

        /// <summary>
        /// GetCurrentPreviewScene Request
        /// </summary>
        GetCurrentPreviewScene,

        /// <summary>
        /// SetCurrentPreviewScene Request
        /// </summary>
        SetCurrentPreviewScene,

        /// <summary>
        /// CreateScene Request
        /// </summary>
        CreateScene,

        /// <summary>
        /// RemoveScene Request
        /// </summary>
        RemoveScene,

        /// <summary>
        /// SetSceneName Request
        /// </summary>
        SetSceneName,

        /// <summary>
        /// GetSceneSceneTransitionOverride Request
        /// </summary>
        GetSceneSceneTransitionOverride,

        /// <summary>
        /// SetSceneSceneTransitionOverride Request
        /// </summary>
        SetSceneSceneTransitionOverride,

        //Inputs Requests

        /// <summary>
        /// GetInputList Request
        /// </summary>
        GetInputList,

        /// <summary>
        /// GetInputKindList Request
        /// </summary>
        GetInputKindList,

        /// <summary>
        /// GetSpecialInputs Request
        /// </summary>
        GetSpecialInputs,

        /// <summary>
        /// CreateInput Request
        /// </summary>
        CreateInput,

        /// <summary>
        /// RemoveInput Request
        /// </summary>
        RemoveInput,

        /// <summary>
        /// SetInputName Request
        /// </summary>
        SetInputName,

        /// <summary>
        /// GetInputDefaultSettings Request
        /// </summary>
        GetInputDefaultSettings,

        /// <summary>
        /// GetInputSettings Request
        /// </summary>
        GetInputSettings,

        /// <summary>
        /// SetInputSettings Request
        /// </summary>
        SetInputSettings,

        /// <summary>
        /// GetInputMute Request
        /// </summary>
        GetInputMute,

        /// <summary>
        /// SetInputMute Request
        /// </summary>
        SetInputMute,

        /// <summary>
        /// ToggleInputMute Request
        /// </summary>
        ToggleInputMute,

        /// <summary>
        /// GetInputVolume Request
        /// </summary>
        GetInputVolume,

        /// <summary>
        /// SetInputVolume Request
        /// </summary>
        SetInputVolume,

        /// <summary>
        /// GetInputAudioBalance Request
        /// </summary>
        GetInputAudioBalance,

        /// <summary>
        /// SetInputAudioBalance Request
        /// </summary>
        SetInputAudioBalance,

        /// <summary>
        /// GetInputAudioSyncOffset Request
        /// </summary>
        GetInputAudioSyncOffset,

        /// <summary>
        /// SetInputAudioSyncOffset Request
        /// </summary>
        SetInputAudioSyncOffset,

        /// <summary>
        /// GetInputAudioMonitorType Request
        /// </summary>
        GetInputAudioMonitorType,

        /// <summary>
        /// SetInputAudioMonitorType Request
        /// </summary>
        SetInputAudioMonitorType,

        /// <summary>
        /// GetInputAudioTracks Request
        /// </summary>
        GetInputAudioTracks,

        /// <summary>
        /// SetInputAudioTracks Request
        /// </summary>
        SetInputAudioTracks,

        /// <summary>
        /// GetInputPropertiesListPropertyItems Request
        /// </summary>
        GetInputPropertiesListPropertyItems,

        /// <summary>
        /// PressInputPropertiesButton Request
        /// </summary>
        PressInputPropertiesButton,

        //Transitions Requests

        /// <summary>
        /// GetTransitionKindList Request
        /// </summary>
        GetTransitionKindList,

        /// <summary>
        /// GetSceneTransitionList Request
        /// </summary>
        GetSceneTransitionList,

        /// <summary>
        /// GetCurrentSceneTransition Request
        /// </summary>
        GetCurrentSceneTransition,

        /// <summary>
        /// SetCurrentSceneTransition Request
        /// </summary>
        SetCurrentSceneTransition,

        /// <summary>
        /// SetCurrentSceneTransitionDuration Request
        /// </summary>
        SetCurrentSceneTransitionDuration,

        /// <summary>
        /// SetCurrentSceneTransitionSettings Request
        /// </summary>
        SetCurrentSceneTransitionSettings,

        /// <summary>
        /// GetCurrentSceneTransitionCursor Request
        /// </summary>
        GetCurrentSceneTransitionCursor,

        /// <summary>
        /// TriggerStudioModeTransition Request
        /// </summary>
        TriggerStudioModeTransition,

        /// <summary>
        /// SetTBarPosition Request
        /// </summary>
        SetTBarPosition,

        //Filters Requests

        /// <summary>
        /// GetSourceFilterList Request
        /// </summary>
        GetSourceFilterList,

        /// <summary>
        /// GetSourceFilterDefaultSettings Request
        /// </summary>
        GetSourceFilterDefaultSettings,

        /// <summary>
        /// CreateSourceFilter Request
        /// </summary>
        CreateSourceFilter,

        /// <summary>
        /// RemoveSourceFilter Request
        /// </summary>
        RemoveSourceFilter,

        /// <summary>
        /// SetSourceFilterName Request
        /// </summary>
        SetSourceFilterName,

        /// <summary>
        /// GetSourceFilter Request
        /// </summary>
        GetSourceFilter,

        /// <summary>
        /// SetSourceFilterIndex Request
        /// </summary>
        SetSourceFilterIndex,

        /// <summary>
        /// SetSourceFilterSettings Request
        /// </summary>
        SetSourceFilterSettings,

        /// <summary>
        /// SetSourceFilterEnabled Request
        /// </summary>
        SetSourceFilterEnabled,

        //Scene Items Requests

        /// <summary>
        /// GetSceneItemList Request
        /// </summary>
        GetSceneItemList,

        /// <summary>
        /// GetGroupSceneItemList Request
        /// </summary>
        GetGroupSceneItemList,

        /// <summary>
        /// GetSceneItemId Request
        /// </summary>
        GetSceneItemId,

        /// <summary>
        /// CreateSceneItem Request
        /// </summary>
        CreateSceneItem,

        /// <summary>
        /// RemoveSceneItem Request
        /// </summary>
        RemoveSceneItem,

        /// <summary>
        /// DuplicateSceneItem Request
        /// </summary>
        DuplicateSceneItem,

        /// <summary>
        /// GetSceneItemTransform Request
        /// </summary>
        GetSceneItemTransform,

        /// <summary>
        /// SetSceneItemTransform Request
        /// </summary>
        SetSceneItemTransform,

        /// <summary>
        /// GetSceneItemEnabled Request
        /// </summary>
        GetSceneItemEnabled,

        /// <summary>
        /// SetSceneItemEnabled Request
        /// </summary>
        SetSceneItemEnabled,

        /// <summary>
        /// GetSceneItemLocked Request
        /// </summary>
        GetSceneItemLocked,

        /// <summary>
        /// SetSceneItemLocked Request
        /// </summary>
        SetSceneItemLocked,

        /// <summary>
        /// GetSceneItemIndex Request
        /// </summary>
        GetSceneItemIndex,

        /// <summary>
        /// SetSceneItemIndex Request
        /// </summary>
        SetSceneItemIndex,

        /// <summary>
        /// GetSceneItemBlendMode Request
        /// </summary>
        GetSceneItemBlendMode,

        /// <summary>
        /// SetSceneItemBlendMode Request
        /// </summary>
        SetSceneItemBlendMode,

        //Outputs Requests

        /// <summary>
        /// GetVirtualCamStatus Request
        /// </summary>
        GetVirtualCamStatus,

        /// <summary>
        /// ToggleVirtualCam Request
        /// </summary>
        ToggleVirtualCam,

        /// <summary>
        /// StartVirtualCam Request
        /// </summary>
        StartVirtualCam,

        /// <summary>
        /// StopVirtualCam Request
        /// </summary>
        StopVirtualCam,

        /// <summary>
        /// GetReplayBufferStatus Request
        /// </summary>
        GetReplayBufferStatus,

        /// <summary>
        /// ToggleReplayBuffer Request
        /// </summary>
        ToggleReplayBuffer,

        /// <summary>
        /// StartReplayBuffer Request
        /// </summary>
        StartReplayBuffer,

        /// <summary>
        /// StopReplayBuffer Request
        /// </summary>
        StopReplayBuffer,

        /// <summary>
        /// SaveReplayBuffer Request
        /// </summary>
        SaveReplayBuffer,

        /// <summary>
        /// GetLastReplayBufferReplay Request
        /// </summary>
        GetLastReplayBufferReplay,

        /// <summary>
        /// GetOutputList Request
        /// </summary>
        GetOutputList,

        /// <summary>
        /// GetOutputStatus Request
        /// </summary>
        GetOutputStatus,

        /// <summary>
        /// ToggleOutput Request
        /// </summary>
        ToggleOutput,

        /// <summary>
        /// StartOutput Request
        /// </summary>
        StartOutput,

        /// <summary>
        /// StopOutput Request
        /// </summary>
        StopOutput,

        /// <summary>
        /// GetOutputSettings Request
        /// </summary>
        GetOutputSettings,

        /// <summary>
        /// SetOutputSettings Request
        /// </summary>
        SetOutputSettings,

        //Stream Requests

        /// <summary>
        /// GetStreamStatus Request
        /// </summary>
        GetStreamStatus,

        /// <summary>
        /// ToggleStream Request
        /// </summary>
        ToggleStream,

        /// <summary>
        /// StartStream Request
        /// </summary>
        StartStream,

        /// <summary>
        /// StopStream Request
        /// </summary>
        StopStream,

        /// <summary>
        /// SendStreamCaption Request
        /// </summary>
        SendStreamCaption,

        //Record Requests

        /// <summary>
        /// GetRecordStatus Request
        /// </summary>
        GetRecordStatus,

        /// <summary>
        /// ToggleRecord Request
        /// </summary>
        ToggleRecord,

        /// <summary>
        /// StartRecord Request
        /// </summary>
        StartRecord,

        /// <summary>
        /// StopRecord Request
        /// </summary>
        StopRecord,

        /// <summary>
        /// ToggleRecordPause Request
        /// </summary>
        ToggleRecordPause,

        /// <summary>
        /// PauseRecord Request
        /// </summary>
        PauseRecord,

        /// <summary>
        /// ResumeRecord Request
        /// </summary>
        ResumeRecord,

        //Media Inputs Requests

        /// <summary>
        /// GetMediaInputStatus Request
        /// </summary>
        GetMediaInputStatus,

        /// <summary>
        /// SetMediaInputCursor Request
        /// </summary>
        SetMediaInputCursor,

        /// <summary>
        /// OffsetMediaInputCursor Request
        /// </summary>
        OffsetMediaInputCursor,

        /// <summary>
        /// TriggerMediaInputAction Request
        /// </summary>
        TriggerMediaInputAction,

        //Ui Requests

        /// <summary>
        /// GetStudioModeEnabled Request
        /// </summary>
        GetStudioModeEnabled,

        /// <summary>
        /// SetStudioModeEnabled Request
        /// </summary>
        SetStudioModeEnabled,

        /// <summary>
        /// OpenInputPropertiesDialog Request
        /// </summary>
        OpenInputPropertiesDialog,

        /// <summary>
        /// OpenInputFiltersDialog Request
        /// </summary>
        OpenInputFiltersDialog,

        /// <summary>
        /// OpenInputInteractDialog Request
        /// </summary>
        OpenInputInteractDialog,

        /// <summary>
        /// GetMonitorList Request
        /// </summary>
        GetMonitorList,

        /// <summary>
        /// OpenVideoMixProjector Request
        /// </summary>
        OpenVideoMixProjector,

        /// <summary>
        /// OpenSourceProjector Request
        /// </summary>
        OpenSourceProjector,
    }
}
