namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Enums;
    using System;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Runtime.Serialization;

    public class RequestResponseMessage : IMessageData, IJsonOnDeserialized
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
        public IResponseData? ResponseData { get; private set; }

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
                    RequestType.GetVersion => this.RawResponseData.Value.Deserialize<VersionResponseData>(),
                    RequestType.GetStats => this.RawResponseData.Value.Deserialize<StatsResponseData>(),
                    RequestType.BroadcastCustomEvent => throw new NotImplementedException(),
                    RequestType.CallVendorRequest => throw new NotImplementedException(),
                    RequestType.GetHotkeyList => this.RawResponseData.Value.Deserialize<HotkeysResponseData>(),
                    RequestType.TriggerHotkeyByName => null,
                    RequestType.TriggerHotkeyByKeySequence => null,
                    RequestType.Sleep => throw new NotImplementedException(),

                    // Config Requests
                    RequestType.GetPersistentData => this.RawResponseData.Value.Deserialize<SlotValueResponseData>(),
                    RequestType.SetPersistentData => null,
                    RequestType.GetSceneCollectionList => this.RawResponseData.Value.Deserialize<SceneCollectionListResponseData>(),
                    RequestType.SetCurrentSceneCollection => null,
                    RequestType.CreateSceneCollection => null,
                    RequestType.GetProfileList => this.RawResponseData.Value.Deserialize<ProfileListResponseData>(),
                    RequestType.SetCurrentProfile => null,
                    RequestType.CreateProfile => null,
                    RequestType.RemoveProfile => null,
                    RequestType.GetProfileParameter => this.RawResponseData.Value.Deserialize<ProfileParameterResponseData>(),
                    RequestType.SetProfileParameter => null,
                    RequestType.GetVideoSettings => this.RawResponseData.Value.Deserialize<VideoSettingsResponseData>(),
                    RequestType.SetVideoSettings => null,
                    RequestType.GetStreamServiceSettings => this.RawResponseData.Value.Deserialize<StreamServiceSettingsResponseData>(),
                    RequestType.SetStreamServiceSettings => null,
                    RequestType.GetRecordDirectory => this.RawResponseData.Value.Deserialize<RecordDirectoryResponseData>(),

                    // Sources Requests
                    RequestType.GetSourceActive => this.RawResponseData.Value.Deserialize<SourceActiveResponseData>(),
                    RequestType.GetSourceScreenshot => this.RawResponseData.Value.Deserialize<ImageDataResponseData>(),
                    RequestType.SaveSourceScreenshot => this.RawResponseData.Value.Deserialize<ImageDataResponseData>(),

                    // Scenes Requests
                    RequestType.GetSceneList => this.RawResponseData.Value.Deserialize<SceneListResponseData>(),
                    RequestType.GetGroupList => this.RawResponseData.Value.Deserialize<GroupsResponseData>(),
                    RequestType.GetCurrentProgramScene => this.RawResponseData.Value.Deserialize<CurrentProgramSceneNameResponseData>(),
                    RequestType.SetCurrentProgramScene => null,
                    RequestType.GetCurrentPreviewScene => this.RawResponseData.Value.Deserialize<CurrentPreviewSceneNameResponseData>(),
                    RequestType.SetCurrentPreviewScene => null,
                    RequestType.CreateScene => null,
                    RequestType.RemoveScene => null,
                    RequestType.SetSceneName => null,
                    RequestType.GetSceneSceneTransitionOverride => this.RawResponseData.Value.Deserialize<SceneTransitionResponseData>(),
                    RequestType.SetSceneSceneTransitionOverride => null,

                    // Inputs Requests
                    RequestType.GetInputList => this.RawResponseData.Value.Deserialize<InputsResponseData>(),
                    RequestType.GetInputKindList => this.RawResponseData.Value.Deserialize<InputKindsResponseData>(),
                    RequestType.GetSpecialInputs => this.RawResponseData.Value.Deserialize<SpecialInputsResponseData>(),
                    RequestType.CreateInput => this.RawResponseData.Value.Deserialize<SceneItemIdResponseData>(),
                    RequestType.RemoveInput => null,
                    RequestType.SetInputName => null,
                    RequestType.GetInputDefaultSettings => this.RawResponseData.Value.Deserialize<DefaultInputSettingsResponseData>(),
                    RequestType.GetInputSettings => this.RawResponseData.Value.Deserialize<InputSettingsResponseData>(),
                    RequestType.SetInputSettings => null,
                    RequestType.GetInputMute => this.RawResponseData.Value.Deserialize<InputMutedResponseData>(),
                    RequestType.SetInputMute => null,
                    RequestType.ToggleInputMute => this.RawResponseData.Value.Deserialize<InputMutedResponseData>(),
                    RequestType.GetInputVolume => this.RawResponseData.Value.Deserialize<InputVolumeResponseData>(),
                    RequestType.SetInputVolume => null,
                    RequestType.GetInputAudioBalance => this.RawResponseData.Value.Deserialize<InputAudioBalanceResponseData>(),
                    RequestType.SetInputAudioBalance => null,
                    RequestType.GetInputAudioSyncOffset => this.RawResponseData.Value.Deserialize<InputAudioSyncOffsetResponseData>(),
                    RequestType.SetInputAudioSyncOffset => null,
                    RequestType.GetInputAudioMonitorType => this.RawResponseData.Value.Deserialize<InputAudioMonitorTypeResponseData>(),
                    RequestType.SetInputAudioMonitorType => null,
                    RequestType.GetInputAudioTracks => this.RawResponseData.Value.Deserialize<InputAudioTracksResponseData>(),
                    RequestType.SetInputAudioTracks => null,
                    RequestType.GetInputPropertiesListPropertyItems => this.RawResponseData.Value.Deserialize<PropertyItemsResponseData>(),
                    RequestType.PressInputPropertiesButton => null,

                    // Transitions Requests
                    RequestType.GetTransitionKindList => this.RawResponseData.Value.Deserialize<TransitionKindsResponseData>(),
                    RequestType.GetSceneTransitionList => this.RawResponseData.Value.Deserialize<SceneTransitionListResponseData>(),
                    RequestType.GetCurrentSceneTransition => this.RawResponseData.Value.Deserialize<ExtendedTransitionResponseData>(),
                    RequestType.SetCurrentSceneTransition => null,
                    RequestType.SetCurrentSceneTransitionDuration => null,
                    RequestType.SetCurrentSceneTransitionSettings => null,
                    RequestType.GetCurrentSceneTransitionCursor => this.RawResponseData.Value.Deserialize<TransitionCursorResponseData>(),
                    RequestType.TriggerStudioModeTransition => null,
                    RequestType.SetTBarPosition => null,

                    // Filters Requests
                    RequestType.GetSourceFilterList => this.RawResponseData.Value.Deserialize<FiltersResponseData>(),
                    RequestType.GetSourceFilterDefaultSettings => this.RawResponseData.Value.Deserialize<DefaultFilterSettingsResponseData>(),
                    RequestType.CreateSourceFilter => null,
                    RequestType.RemoveSourceFilter => null,
                    RequestType.SetSourceFilterName => null,
                    RequestType.GetSourceFilter => this.RawResponseData.Value.Deserialize<SourceFilterResponseData>(),
                    RequestType.SetSourceFilterIndex => null,
                    RequestType.SetSourceFilterSettings => null,
                    RequestType.SetSourceFilterEnabled => null,

                    // Scene Items Requests
                    RequestType.GetSceneItemList => this.RawResponseData.Value.Deserialize<SceneItemsResponseData>(),
                    RequestType.GetGroupSceneItemList => this.RawResponseData.Value.Deserialize<SceneItemsResponseData>(),
                    RequestType.GetSceneItemId => this.RawResponseData.Value.Deserialize<SceneItemIdResponseData>(),
                    RequestType.CreateSceneItem => this.RawResponseData.Value.Deserialize<SceneItemIdResponseData>(),
                    RequestType.RemoveSceneItem => null,
                    RequestType.DuplicateSceneItem => this.RawResponseData.Value.Deserialize<SceneItemIdResponseData>(),
                    RequestType.GetSceneItemTransform => this.RawResponseData.Value.Deserialize<SceneItemTransformResponseData>(),
                    RequestType.SetSceneItemTransform => null,
                    RequestType.GetSceneItemEnabled => this.RawResponseData.Value.Deserialize<SceneItemEnabledResponseData>(),
                    RequestType.SetSceneItemEnabled => null,
                    RequestType.GetSceneItemLocked => this.RawResponseData.Value.Deserialize<SceneItemlockedResponseData>(),
                    RequestType.SetSceneItemLocked => null,
                    RequestType.GetSceneItemIndex => this.RawResponseData.Value.Deserialize<SceneItemIndexResponseData>(),
                    RequestType.SetSceneItemIndex => null,
                    RequestType.GetSceneItemBlendMode => this.RawResponseData.Value.Deserialize<SceneItemBlendModeResponseData>(),
                    RequestType.SetSceneItemBlendMode => null,

                    // Outputs Requests
                    RequestType.GetVirtualCamStatus => this.RawResponseData.Value.Deserialize<OutputActiveResponseData>(),
                    RequestType.ToggleVirtualCam => this.RawResponseData.Value.Deserialize<OutputActiveResponseData>(),
                    RequestType.StartVirtualCam => null,
                    RequestType.StopVirtualCam => null,
                    RequestType.GetReplayBufferStatus => this.RawResponseData.Value.Deserialize<OutputActiveResponseData>(),
                    RequestType.ToggleReplayBuffer => this.RawResponseData.Value.Deserialize<OutputActiveResponseData>(),
                    RequestType.StartReplayBuffer => null,
                    RequestType.StopReplayBuffer => null,
                    RequestType.SaveReplayBuffer => null,
                    RequestType.GetLastReplayBufferReplay => this.RawResponseData.Value.Deserialize<SavedReplayPathResponseData>(),
                    RequestType.GetOutputList => this.RawResponseData.Value.Deserialize<OutputsResponseData>(),
                    RequestType.GetOutputStatus => this.RawResponseData.Value.Deserialize<OutputStatusResponseData>(),
                    RequestType.ToggleOutput => this.RawResponseData.Value.Deserialize<OutputActiveResponseData>(),
                    RequestType.StartOutput => null,
                    RequestType.StopOutput => null,
                    RequestType.GetOutputSettings => this.RawResponseData.Value.Deserialize<OutputSettingsResponseData>(),
                    RequestType.SetOutputSettings => null,

                    // Stream Requests
                    RequestType.GetStreamStatus => this.RawResponseData.Value.Deserialize<OutputStatusResponseData>(),
                    RequestType.ToggleStream => this.RawResponseData.Value.Deserialize<OutputActiveResponseData>(),
                    RequestType.StartStream => null,
                    RequestType.StopStream => null,
                    RequestType.SendStreamCaption => null,

                    // Record Requests
                    RequestType.GetRecordStatus => this.RawResponseData.Value.Deserialize<RecordStatusResponseData>(),
                    RequestType.ToggleRecord => null,
                    RequestType.StartRecord => null,
                    RequestType.StopRecord => this.RawResponseData.Value.Deserialize<OutputPathResponseData>(),
                    RequestType.ToggleRecordPause => null,
                    RequestType.PauseRecord => null,
                    RequestType.ResumeRecord => null,

                    // Media Inputs Requests
                    RequestType.GetMediaInputStatus => this.RawResponseData.Value.Deserialize<MediaInputStatusResponseData>(),
                    RequestType.SetMediaInputCursor => null,
                    RequestType.OffsetMediaInputCursor => null,
                    RequestType.TriggerMediaInputAction => null,

                    // Ui Requests
                    RequestType.GetStudioModeEnabled => this.RawResponseData.Value.Deserialize<StudioModeEnabledData>(),
                    RequestType.SetStudioModeEnabled => null,
                    RequestType.OpenInputPropertiesDialog => null,
                    RequestType.OpenInputFiltersDialog => null,
                    RequestType.OpenInputInteractDialog => null,
                    RequestType.GetMonitorList => this.RawResponseData.Value.Deserialize<MonitorListResponseData>(),
                    RequestType.OpenVideoMixProjector => null,
                    RequestType.OpenSourceProjector => null,

                    _ => throw new NotSupportedException()
                };

                if (this.ResponseData == null)
                {
                    throw new OBSClientException("Deserialization failure of RequestResponseData.");
                }
            }
        }
    }
}
