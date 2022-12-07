namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestType
    {
        //General Requests

        GetVersion,
        GetStats,
        BroadcastCustomEvent,
        CallVendorRequest,
        GetHotkeyList,
        TriggerHotkeyByName,
        TriggerHotkeyByKeySequence,
        Sleep,
        
        //Config Requests
        
        GetPersistentData,
        SetPersistentData,
        GetSceneCollectionList,
        SetCurrentSceneCollection,
        CreateSceneCollection,
        GetProfileList,
        SetCurrentProfile,
        CreateProfile,
        RemoveProfile,
        GetProfileParameter,
        SetProfileParameter,
        GetVideoSettings,
        SetVideoSettings,
        GetStreamServiceSettings,
        SetStreamServiceSettings,
        GetRecordDirectory,
        
        //Sources Requests
        
        GetSourceActive,
        GetSourceScreenshot,
        SaveSourceScreenshot,
        
        //Scenes Requests
        
        GetSceneList,
        GetGroupList,
        GetCurrentProgramScene,
        SetCurrentProgramScene,
        GetCurrentPreviewScene,
        SetCurrentPreviewScene,
        CreateScene,
        RemoveScene,
        SetSceneName,
        GetSceneSceneTransitionOverride,
        SetSceneSceneTransitionOverride,
        
        //Inputs Requests
        
        GetInputList,
        GetInputKindList,
        GetSpecialInputs,
        CreateInput,
        RemoveInput,
        SetInputName,
        GetInputDefaultSettings,
        GetInputSettings,
        SetInputSettings,
        GetInputMute,
        SetInputMute,
        ToggleInputMute,
        GetInputVolume,
        SetInputVolume,
        GetInputAudioBalance,
        SetInputAudioBalance,
        GetInputAudioSyncOffset,
        SetInputAudioSyncOffset,
        GetInputAudioMonitorType,
        SetInputAudioMonitorType,
        GetInputAudioTracks,
        SetInputAudioTracks,
        GetInputPropertiesListPropertyItems,
        PressInputPropertiesButton,
        
        //Transitions Requests
        
        GetTransitionKindList,
        GetSceneTransitionList,
        GetCurrentSceneTransition,
        SetCurrentSceneTransition,
        SetCurrentSceneTransitionDuration,
        SetCurrentSceneTransitionSettings,
        GetCurrentSceneTransitionCursor,
        TriggerStudioModeTransition,
        SetTBarPosition,
        
        //Filters Requests
        
        GetSourceFilterList,
        GetSourceFilterDefaultSettings,
        CreateSourceFilter,
        RemoveSourceFilter,
        SetSourceFilterName,
        GetSourceFilter,
        SetSourceFilterIndex,
        SetSourceFilterSettings,
        SetSourceFilterEnabled,
        
        //Scene Items Requests
        
        GetSceneItemList,
        GetGroupSceneItemList,
        GetSceneItemId,
        CreateSceneItem,
        RemoveSceneItem,
        DuplicateSceneItem,
        GetSceneItemTransform,
        SetSceneItemTransform,
        GetSceneItemEnabled,
        SetSceneItemEnabled,
        GetSceneItemLocked,
        SetSceneItemLocked,
        GetSceneItemIndex,
        SetSceneItemIndex,
        GetSceneItemBlendMode,
        SetSceneItemBlendMode,
        
        //Outputs Requests
        
        GetVirtualCamStatus,
        ToggleVirtualCam,
        StartVirtualCam,
        StopVirtualCam,
        GetReplayBufferStatus,
        ToggleReplayBuffer,
        StartReplayBuffer,
        StopReplayBuffer,
        SaveReplayBuffer,
        GetLastReplayBufferReplay,
        GetOutputList,
        GetOutputStatus,
        ToggleOutput,
        StartOutput,
        StopOutput,
        GetOutputSettings,
        SetOutputSettings,
        
        //Stream Requests
        
        GetStreamStatus,
        ToggleStream,
        StartStream,
        StopStream,
        SendStreamCaption,
        
        //Record Requests
        
        GetRecordStatus,
        ToggleRecord,
        StartRecord,
        StopRecord,
        ToggleRecordPause,
        PauseRecord,
        ResumeRecord,
        
        //Media Inputs Requests
        
        GetMediaInputStatus,
        SetMediaInputCursor,
        OffsetMediaInputCursor,
        TriggerMediaInputAction,
        
        //Ui Requests
        
        GetStudioModeEnabled,
        SetStudioModeEnabled,
        OpenInputPropertiesDialog,
        OpenInputFiltersDialog,
        OpenInputInteractDialog,
        GetMonitorList,
        OpenVideoMixProjector,
        OpenSourceProjector,
    }
}
