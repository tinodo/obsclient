namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Enums;
    using System;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Runtime.Serialization;

    public class RequestResponseMessage : IMessage, IJsonOnDeserialized
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]    
        [JsonPropertyName("requestType")]
        public RequestType RequestType { get; }

        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        [JsonPropertyName("requestStatus")]
        public RequestStatus RequestStatus { get; }

        [JsonPropertyName("responseData")]
        public JsonElement? RawResponseData { get; }

        [JsonIgnore]
        public IResponse? ResponseData { get; private set; }

        [JsonConstructor]
        public RequestResponseMessage(RequestType requestType, string requestId, RequestStatus requestStatus, JsonElement? rawResponseData)
        {
            this.RequestType = requestType;
            this.RequestId = requestId;
            this.RequestStatus = requestStatus;
            this.RawResponseData = rawResponseData;
        }

        public void OnDeserialized()
        {
            if (this.RawResponseData != null)
            {
                this.ResponseData = this.RequestType switch
                {
                    // General Requests
                    RequestType.GetVersion => this.RawResponseData.Value.Deserialize<VersionResponse>(),
                    RequestType.GetStats => this.RawResponseData.Value.Deserialize<StatsResponse>(),
                    RequestType.BroadcastCustomEvent => throw new NotImplementedException(),
                    RequestType.CallVendorRequest => throw new NotImplementedException(),
                    RequestType.GetHotkeyList => this.RawResponseData.Value.Deserialize<HotkeysResponse>(),
                    RequestType.TriggerHotkeyByName => null,
                    RequestType.TriggerHotkeyByKeySequence => null,
                    RequestType.Sleep => throw new NotImplementedException(),

                    // Config Requests
                    RequestType.GetPersistentData => this.RawResponseData.Value.Deserialize<SlotValueResponse>(),
                    RequestType.SetPersistentData => null,
                    RequestType.GetSceneCollectionList => this.RawResponseData.Value.Deserialize<SceneCollectionListResponse>(),
                    RequestType.SetCurrentSceneCollection => null,
                    RequestType.CreateSceneCollection => null,
                    RequestType.GetProfileList => this.RawResponseData.Value.Deserialize<ProfileListResponse>(),
                    RequestType.SetCurrentProfile => null,
                    RequestType.CreateProfile => null,
                    RequestType.RemoveProfile => null,
                    RequestType.GetProfileParameter => this.RawResponseData.Value.Deserialize<ProfileParameterResponse>(),
                    RequestType.SetProfileParameter => null,
                    RequestType.GetVideoSettings => this.RawResponseData.Value.Deserialize<VideoSettingsResponse>(),
                    RequestType.SetVideoSettings => null,
                    RequestType.GetStreamServiceSettings => this.RawResponseData.Value.Deserialize<StreamServiceSettingsResponse>(),
                    RequestType.SetStreamServiceSettings => null,
                    RequestType.GetRecordDirectory => this.RawResponseData.Value.Deserialize<RecordDirectoryResponse>(),

                    // Sources Requests
                    RequestType.GetSourceActive => this.RawResponseData.Value.Deserialize<SourceActiveResponse>(),
                    RequestType.GetSourceScreenshot => this.RawResponseData.Value.Deserialize<ImageDataResponse>(),
                    RequestType.SaveSourceScreenshot => this.RawResponseData.Value.Deserialize<ImageDataResponse>(),

                    // Scenes Requests
                    RequestType.GetSceneList => this.RawResponseData.Value.Deserialize<SceneListResponse>(),
                    RequestType.GetGroupList => this.RawResponseData.Value.Deserialize<GroupsResponse>(),
                    RequestType.GetCurrentProgramScene => this.RawResponseData.Value.Deserialize<CurrentProgramSceneNameResponse>(),
                    RequestType.SetCurrentProgramScene => null,
                    RequestType.GetCurrentPreviewScene => this.RawResponseData.Value.Deserialize<CurrentPreviewSceneNameResponse>(),
                    RequestType.SetCurrentPreviewScene => null,
                    RequestType.CreateScene => null,
                    RequestType.RemoveScene => null,
                    RequestType.SetSceneName => null,
                    RequestType.GetSceneSceneTransitionOverride => this.RawResponseData.Value.Deserialize<SceneTransitionResponse>(),
                    RequestType.SetSceneSceneTransitionOverride => null,

                    // Inputs Requests
                    RequestType.GetInputList => this.RawResponseData.Value.Deserialize<InputsResponse>(),
                    RequestType.GetInputKindList => this.RawResponseData.Value.Deserialize<InputKindsResponse>(),
                    RequestType.GetSpecialInputs => this.RawResponseData.Value.Deserialize<SpecialInputsResponse>(),
                    RequestType.CreateInput => this.RawResponseData.Value.Deserialize<SceneItemIdResponse>(),
                    RequestType.RemoveInput => null,
                    RequestType.SetInputName => null,
                    RequestType.GetInputDefaultSettings => this.RawResponseData.Value.Deserialize<DefaultInputSettingsResponse>(),
                    RequestType.GetInputSettings => this.RawResponseData.Value.Deserialize<InputSettingsResponse>(),
                    RequestType.SetInputSettings => null,
                    RequestType.GetInputMute => this.RawResponseData.Value.Deserialize<InputMutedResponse>(),
                    RequestType.SetInputMute => null,
                    RequestType.ToggleInputMute => this.RawResponseData.Value.Deserialize<InputMutedResponse>(),
                    RequestType.GetInputVolume => this.RawResponseData.Value.Deserialize<InputVolumeResponse>(),
                    RequestType.SetInputVolume => null,
                    RequestType.GetInputAudioBalance => this.RawResponseData.Value.Deserialize<InputAudioBalanceResponse>(),
                    RequestType.SetInputAudioBalance => null,
                    RequestType.GetInputAudioSyncOffset => this.RawResponseData.Value.Deserialize<InputAudioSyncOffsetResponse>(),
                    RequestType.SetInputAudioSyncOffset => null,
                    RequestType.GetInputAudioMonitorType => this.RawResponseData.Value.Deserialize<InputAudioMonitorTypeResponse>(),
                    RequestType.SetInputAudioMonitorType => null,
                    RequestType.GetInputAudioTracks => this.RawResponseData.Value.Deserialize<InputAudioTracksResponse>(),
                    RequestType.SetInputAudioTracks => null,
                    RequestType.GetInputPropertiesListPropertyItems => this.RawResponseData.Value.Deserialize<PropertyItemsResponse>(),
                    RequestType.PressInputPropertiesButton => null,

                    // Transitions Requests
                    RequestType.GetTransitionKindList => this.RawResponseData.Value.Deserialize<TransitionKindsResponse>(),
                    RequestType.GetSceneTransitionList => this.RawResponseData.Value.Deserialize<SceneTransitionListResponse>(),
                    RequestType.GetCurrentSceneTransition => this.RawResponseData.Value.Deserialize<ExtendedTransitionResponse>(),
                    RequestType.SetCurrentSceneTransition => null,
                    RequestType.SetCurrentSceneTransitionDuration => null,
                    RequestType.SetCurrentSceneTransitionSettings => null,
                    RequestType.GetCurrentSceneTransitionCursor => this.RawResponseData.Value.Deserialize<TransitionCursorResponse>(),
                    RequestType.TriggerStudioModeTransition => null,
                    RequestType.SetTBarPosition => null,

                    // Filters Requests
                    RequestType.GetSourceFilterList => this.RawResponseData.Value.Deserialize<FiltersResponse>(),
                    RequestType.GetSourceFilterDefaultSettings => this.RawResponseData.Value.Deserialize<DefaultFilterSettingsResponse>(),
                    RequestType.CreateSourceFilter => null,
                    RequestType.RemoveSourceFilter => null,
                    RequestType.SetSourceFilterName => null,
                    RequestType.GetSourceFilter => this.RawResponseData.Value.Deserialize<SourceFilterResponse>(),
                    RequestType.SetSourceFilterIndex => null,
                    RequestType.SetSourceFilterSettings => null,
                    RequestType.SetSourceFilterEnabled => null,

                    // Scene Items Requests
                    RequestType.GetSceneItemList => this.RawResponseData.Value.Deserialize<SceneItemsResponse>(),
                    RequestType.GetGroupSceneItemList => this.RawResponseData.Value.Deserialize<SceneItemsResponse>(),
                    RequestType.GetSceneItemId => this.RawResponseData.Value.Deserialize<SceneItemIdResponse>(),
                    RequestType.CreateSceneItem => this.RawResponseData.Value.Deserialize<SceneItemIdResponse>(),
                    RequestType.RemoveSceneItem => null,
                    RequestType.DuplicateSceneItem => this.RawResponseData.Value.Deserialize<SceneItemIdResponse>(),
                    RequestType.GetSceneItemTransform => this.RawResponseData.Value.Deserialize<SceneItemTransformResponse>(),
                    RequestType.SetSceneItemTransform => null,
                    RequestType.GetSceneItemEnabled => this.RawResponseData.Value.Deserialize<SceneItemEnabledResponse>(),
                    RequestType.SetSceneItemEnabled => null,
                    RequestType.GetSceneItemLocked => this.RawResponseData.Value.Deserialize<SceneItemlockedResponse>(),
                    RequestType.SetSceneItemLocked => null,
                    RequestType.GetSceneItemIndex => this.RawResponseData.Value.Deserialize<SceneItemIndexResponse>(),
                    RequestType.SetSceneItemIndex => null,
                    RequestType.GetSceneItemBlendMode => this.RawResponseData.Value.Deserialize<SceneItemBlendModeResponse>(),
                    RequestType.SetSceneItemBlendMode => null,

                    // Outputs Requests
                    RequestType.GetVirtualCamStatus => this.RawResponseData.Value.Deserialize<OutputActiveResponse>(),
                    RequestType.ToggleVirtualCam => this.RawResponseData.Value.Deserialize<OutputActiveResponse>(),
                    RequestType.StartVirtualCam => null,
                    RequestType.StopVirtualCam => null,
                    RequestType.GetReplayBufferStatus => this.RawResponseData.Value.Deserialize<OutputActiveResponse>(),
                    RequestType.ToggleReplayBuffer => this.RawResponseData.Value.Deserialize<OutputActiveResponse>(),
                    RequestType.StartReplayBuffer => null,
                    RequestType.StopReplayBuffer => null,
                    RequestType.SaveReplayBuffer => null,
                    RequestType.GetLastReplayBufferReplay => this.RawResponseData.Value.Deserialize<SavedReplayPathResponse>(),
                    RequestType.GetOutputList => this.RawResponseData.Value.Deserialize<OutputsResponse>(),
                    RequestType.GetOutputStatus => this.RawResponseData.Value.Deserialize<OutputStatusResponse>(),
                    RequestType.ToggleOutput => this.RawResponseData.Value.Deserialize<OutputActiveResponse>(),
                    RequestType.StartOutput => null,
                    RequestType.StopOutput => null,
                    RequestType.GetOutputSettings => this.RawResponseData.Value.Deserialize<OutputSettingsResponse>(),
                    RequestType.SetOutputSettings => null,

                    // Stream Requests
                    RequestType.GetStreamStatus => this.RawResponseData.Value.Deserialize<OutputStatusResponse>(),
                    RequestType.ToggleStream => this.RawResponseData.Value.Deserialize<OutputActiveResponse>(),
                    RequestType.StartStream => null,
                    RequestType.StopStream => null,
                    RequestType.SendStreamCaption => null,

                    // Record Requests
                    RequestType.GetRecordStatus => this.RawResponseData.Value.Deserialize<RecordStatusResponse>(),
                    RequestType.ToggleRecord => null,
                    RequestType.StartRecord => null,
                    RequestType.StopRecord => this.RawResponseData.Value.Deserialize<OutputPathResponse>(),
                    RequestType.ToggleRecordPause => null,
                    RequestType.PauseRecord => null,
                    RequestType.ResumeRecord => null,

                    // Media Inputs Requests
                    RequestType.GetMediaInputStatus => this.RawResponseData.Value.Deserialize<MediaInputStatusResponse>(),
                    RequestType.SetMediaInputCursor => null,
                    RequestType.OffsetMediaInputCursor => null,
                    RequestType.TriggerMediaInputAction => null,

                    // Ui Requests
                    RequestType.GetStudioModeEnabled => this.RawResponseData.Value.Deserialize<StudioModeEnabledResponse>(),
                    RequestType.SetStudioModeEnabled => null,
                    RequestType.OpenInputPropertiesDialog => null,
                    RequestType.OpenInputFiltersDialog => null,
                    RequestType.OpenInputInteractDialog => null,
                    RequestType.GetMonitorList => this.RawResponseData.Value.Deserialize<MonitorListResponse>(),
                    RequestType.OpenVideoMixProjector => null,
                    RequestType.OpenSourceProjector => null,

                    _ => throw new NotSupportedException()
                };

                if (this.ResponseData == null)
                {
                    throw new ObsClientException("Deserialization failure of RequestResponseData.");
                }
            }
        }
    }
}
