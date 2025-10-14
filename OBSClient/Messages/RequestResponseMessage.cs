namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Responses;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Response Messages
    /// </summary>
    /// <remarks>
    /// Creates a new instance of a <see cref="RequestResponseMessage"/>
    /// </remarks>
    /// <param name="requestType">The type of request this is a response to.</param>
    /// <param name="requestId">The identifier of the original request.</param>
    /// <param name="requestStatus">The status of the request.</param>
    /// <param name="rawResponseData"> The raw JSON response data</param>
    /// <summary>
    /// Class for Request Response Messages
    /// </summary>
    [method: JsonConstructor]
    public class RequestResponseMessage(RequestType requestType, string requestId, RequestStatus requestStatus, JsonElement? rawResponseData) : IMessage, IJsonOnDeserialized
    {
        /// <summary>
        /// Gets the type of the response based on the request type.
        /// </summary>
        private static readonly Dictionary<RequestType, Type> _responseTypeMap = new()
        {
            // General Requests
            {RequestType.GetVersion, typeof(VersionResponse) },
            {RequestType.GetStats, typeof(StatsResponse) },
            {RequestType.BroadcastCustomEvent, typeof(EmptyResponse) },
            {RequestType.CallVendorRequest, typeof(CallVendorResponse) },
            {RequestType.GetHotkeyList, typeof(HotkeysResponse) },
            {RequestType.TriggerHotkeyByName, typeof(EmptyResponse) },
            {RequestType.TriggerHotkeyByKeySequence, typeof(EmptyResponse) },
            {RequestType.Sleep, typeof(EmptyResponse) },

            // Config Requests
            {RequestType.GetPersistentData, typeof(SlotValueResponse) },
            {RequestType.SetPersistentData, typeof(EmptyResponse) },
            {RequestType.GetSceneCollectionList, typeof(SceneCollectionListResponse) },
            {RequestType.SetCurrentSceneCollection, typeof(EmptyResponse) },
            {RequestType.CreateSceneCollection, typeof(EmptyResponse) },
            {RequestType.GetProfileList, typeof(ProfileListResponse) },
            {RequestType.SetCurrentProfile, typeof(EmptyResponse) },
            {RequestType.CreateProfile, typeof(EmptyResponse) },
            {RequestType.RemoveProfile, typeof(EmptyResponse) },
            {RequestType.GetProfileParameter, typeof(ProfileParameterResponse) },
            {RequestType.SetProfileParameter, typeof(EmptyResponse) },
            {RequestType.GetVideoSettings, typeof(VideoSettingsResponse) },
            {RequestType.SetVideoSettings, typeof(EmptyResponse) },
            {RequestType.GetStreamServiceSettings, typeof(StreamServiceSettingsResponse) },
            {RequestType.SetStreamServiceSettings, typeof(EmptyResponse) },
            {RequestType.GetRecordDirectory, typeof(RecordDirectoryResponse) },
            {RequestType.SetRecordDirectory, typeof(EmptyResponse) },

            // Sources Requests
            {RequestType.GetSourceActive, typeof(SourceActiveResponse) },
            {RequestType.GetSourceScreenshot, typeof(ImageDataResponse) },
            {RequestType.SaveSourceScreenshot, typeof(EmptyResponse) },

            // Scenes Requests
            {RequestType.GetSceneList, typeof(SceneListResponse) },
            {RequestType.GetGroupList, typeof(GroupsResponse) },
            {RequestType.GetCurrentProgramScene, typeof(CurrentProgramSceneNameResponse) },
            {RequestType.SetCurrentProgramScene, typeof(EmptyResponse) },
            {RequestType.GetCurrentPreviewScene, typeof(CurrentPreviewSceneNameResponse) },
            {RequestType.SetCurrentPreviewScene, typeof(EmptyResponse) },
            {RequestType.CreateScene, typeof(CreateSceneResponse) },
            {RequestType.RemoveScene, typeof(EmptyResponse) },
            {RequestType.SetSceneName, typeof(EmptyResponse) },
            {RequestType.GetSceneSceneTransitionOverride, typeof(SceneTransitionResponse) },
            {RequestType.SetSceneSceneTransitionOverride, typeof(EmptyResponse) },

            // Inputs Requests
            {RequestType.GetInputList, typeof(InputsResponse) },
            {RequestType.GetInputKindList, typeof(InputKindsResponse) },
            {RequestType.GetSpecialInputs, typeof(SpecialInputsResponse) },
            {RequestType.CreateInput, typeof(CreateInputResponse) },
            {RequestType.RemoveInput, typeof(EmptyResponse) },
            {RequestType.SetInputName, typeof(EmptyResponse) },
            {RequestType.GetInputDefaultSettings, typeof(DefaultInputSettingsResponse) },
            {RequestType.GetInputSettings, typeof(InputSettingsResponse) },
            {RequestType.GetInputMute, typeof(InputMutedResponse) },
            {RequestType.SetInputMute, typeof(EmptyResponse) },
            {RequestType.ToggleInputMute, typeof(InputMutedResponse) },
            {RequestType.GetInputVolume, typeof(InputVolumeResponse) },
            {RequestType.SetInputVolume, typeof(EmptyResponse) },
            {RequestType.GetInputAudioBalance, typeof(InputAudioBalanceResponse) },
            {RequestType.SetInputAudioBalance, typeof(EmptyResponse) },
            {RequestType.GetInputAudioSyncOffset, typeof(InputAudioSyncOffsetResponse) },
            {RequestType.SetInputAudioSyncOffset, typeof(EmptyResponse) },
            {RequestType.GetInputAudioMonitorType, typeof(InputAudioMonitorTypeResponse) },
            {RequestType.SetInputAudioMonitorType, typeof(EmptyResponse) },
            {RequestType.GetInputAudioTracks, typeof(InputAudioTracksResponse) },
            {RequestType.SetInputAudioTracks, typeof(EmptyResponse) },
            {RequestType.GetInputDeinterlaceMode, typeof(InputDeinterlaceModeResponse) },
            {RequestType.SetInputDeinterlaceMode, typeof(EmptyResponse) },
            {RequestType.GetInputDeinterlaceFieldOrder, typeof(InputDeinterlaceModeResponse) },
            {RequestType.SetInputDeinterlaceFieldOrder, typeof(EmptyResponse) },
            {RequestType.GetInputPropertiesListPropertyItems, typeof(PropertyItemsResponse) },
            {RequestType.PressInputPropertiesButton, typeof(EmptyResponse) },
            
            // Transitions Requests
            {RequestType.GetTransitionKindList, typeof(TransitionKindsResponse) },
            {RequestType.GetSceneTransitionList, typeof(SceneTransitionListResponse) },
            {RequestType.GetCurrentSceneTransition, typeof(TransitionResponse) },
            {RequestType.SetCurrentSceneTransition, typeof(EmptyResponse) },
            {RequestType.SetCurrentSceneTransitionDuration, typeof(EmptyResponse) },
            {RequestType.SetCurrentSceneTransitionSettings, typeof(EmptyResponse) },
            {RequestType.GetCurrentSceneTransitionCursor, typeof(TransitionCursorResponse) },
            {RequestType.TriggerStudioModeTransition, typeof(EmptyResponse) },
            {RequestType.SetTBarPosition, typeof(EmptyResponse) },

            // Filters Requests
            {RequestType.GetSourceFilterKindList, typeof(FilterKindListResponse) },
            {RequestType.GetSourceFilterList, typeof(FiltersResponse) },
            {RequestType.GetSourceFilterDefaultSettings, typeof(DefaultFilterSettingsResponse) },
            {RequestType.CreateSourceFilter, typeof(EmptyResponse) },
            {RequestType.RemoveSourceFilter, typeof(EmptyResponse) },
            {RequestType.SetSourceFilterName, typeof(EmptyResponse) },
            {RequestType.GetSourceFilter, typeof(SourceFilterResponse) },
            {RequestType.SetSourceFilterIndex, typeof(EmptyResponse) },
            {RequestType.SetSourceFilterSettings, typeof(EmptyResponse) },
            {RequestType.SetSourceFilterEnabled, typeof(EmptyResponse) },

            // Scene Items Requests
            {RequestType.GetSceneItemList, typeof(SceneItemsResponse) },
            {RequestType.GetGroupSceneItemList, typeof(SceneItemsResponse) },
            {RequestType.GetSceneItemId, typeof(SceneItemIdResponse) },
            {RequestType.GetSceneItemSource, typeof(SceneItemSourceResponse) },
            {RequestType.CreateSceneItem, typeof(SceneItemIdResponse) },
            {RequestType.RemoveSceneItem, typeof(EmptyResponse) },
            {RequestType.DuplicateSceneItem, typeof(SceneItemIdResponse) },
            {RequestType.GetSceneItemTransform, typeof(SceneItemTransformResponse) },
            {RequestType.SetSceneItemTransform, typeof(EmptyResponse) },
            {RequestType.GetSceneItemEnabled, typeof(SceneItemEnabledResponse) },
            {RequestType.SetSceneItemEnabled, typeof(EmptyResponse) },
            {RequestType.GetSceneItemLocked, typeof(SceneItemlockedResponse) },
            {RequestType.SetSceneItemLocked, typeof(EmptyResponse) },
            {RequestType.GetSceneItemIndex, typeof(SceneItemIndexResponse) },
            {RequestType.SetSceneItemIndex, typeof(EmptyResponse) },
            {RequestType.GetSceneItemBlendMode, typeof(SceneItemBlendModeResponse) },
            {RequestType.SetSceneItemBlendMode, typeof(EmptyResponse) },

            // Outputs Requests
            {RequestType.GetVirtualCamStatus, typeof(OutputActiveResponse) },
            {RequestType.ToggleVirtualCam, typeof(OutputActiveResponse) },
            {RequestType.StartVirtualCam, typeof(EmptyResponse) },
            {RequestType.StopVirtualCam, typeof(EmptyResponse) },
            {RequestType.GetReplayBufferStatus, typeof(OutputActiveResponse) },
            {RequestType.ToggleReplayBuffer, typeof(OutputActiveResponse) },
            {RequestType.StartReplayBuffer, typeof(EmptyResponse) },
            {RequestType.StopReplayBuffer, typeof(EmptyResponse) },
            {RequestType.SaveReplayBuffer, typeof(EmptyResponse) },
            {RequestType.GetLastReplayBufferReplay, typeof(SavedReplayPathResponse) },
            {RequestType.GetOutputList, typeof(OutputsResponse) },
            {RequestType.GetOutputStatus, typeof(OutputStatusResponse) },
            {RequestType.ToggleOutput, typeof(OutputActiveResponse) },
            {RequestType.StartOutput, typeof(EmptyResponse) },
            {RequestType.StopOutput, typeof(EmptyResponse) },
            {RequestType.GetOutputSettings, typeof(OutputSettingsResponse) },
            {RequestType.SetOutputSettings, typeof(EmptyResponse) },

            // Stream Requests
            {RequestType.GetStreamStatus, typeof(OutputStatusResponse) },
            {RequestType.ToggleStream, typeof(OutputActiveResponse) },
            {RequestType.StartStream, typeof(EmptyResponse) },
            {RequestType.StopStream, typeof(EmptyResponse) },
            {RequestType.SendStreamCaption, typeof(EmptyResponse) },

            // Record Requests
            {RequestType.GetRecordStatus, typeof(RecordStatusResponse) },
            {RequestType.ToggleRecord, typeof(OutputActiveResponse) },
            {RequestType.StartRecord, typeof(EmptyResponse) },
            {RequestType.StopRecord, typeof(OutputPathResponse) },
            {RequestType.ToggleRecordPause, typeof(OutputPausedResponse) },
            {RequestType.PauseRecord, typeof(EmptyResponse) },
            {RequestType.ResumeRecord, typeof(EmptyResponse) },
            {RequestType.SplitRecordFile, typeof(EmptyResponse) },
            {RequestType.CreateRecordChapter, typeof(EmptyResponse) },

            // Media Inputs Requests
            {RequestType.GetMediaInputStatus, typeof(MediaInputStatusResponse) },
            {RequestType.SetMediaInputCursor, typeof(EmptyResponse) },
            {RequestType.OffsetMediaInputCursor, typeof(EmptyResponse) },
            {RequestType.TriggerMediaInputAction, typeof(EmptyResponse) },

            // Ui Requests
            {RequestType.GetStudioModeEnabled, typeof(StudioModeEnabledResponse) },
            {RequestType.SetStudioModeEnabled, typeof(EmptyResponse) },
            {RequestType.OpenInputPropertiesDialog, typeof(EmptyResponse) },
            {RequestType.OpenInputFiltersDialog, typeof(EmptyResponse) },
            {RequestType.OpenInputInteractDialog, typeof(EmptyResponse) },
            {RequestType.GetMonitorList, typeof(MonitorListResponse) },
            {RequestType.OpenVideoMixProjector, typeof(EmptyResponse) },
            {RequestType.OpenSourceProjector, typeof(EmptyResponse) },
        };

        /// <summary>
        /// The type of request this is a response to.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("requestType")]
        public RequestType RequestType { get; } = requestType;

        /// <summary>
        /// The identifier of the original request.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; } = requestId;

        /// <summary>
        /// The status of the request.
        /// </summary>
        [JsonPropertyName("requestStatus")]
        public RequestStatus RequestStatus { get; } = requestStatus;

        /// <summary>
        /// The raw JSON response data
        /// </summary>
        [JsonPropertyName("responseData")]
        public JsonElement? RawResponseData { get; } = rawResponseData;

        /// <summary>
        /// The deserialized response data.
        /// </summary>
        [JsonIgnore]
        public IResponse? ResponseData { get; private set; }

        /// <summary>
        /// Deserializes the raw JSON response to Response Data
        /// </summary>
        /// <exception cref="NotImplementedException">Not implemented.</exception>
        /// <exception cref="NotSupportedException">The response data is unknown.</exception>
        /// <exception cref="ObsClientException">Deserialization failed</exception>
        public void OnDeserialized()
        {
            if (!_responseTypeMap.TryGetValue(this.RequestType, out Type? responseType))
            {
                throw new ObsClientException("OBS Studio response could not be mapped.");
            }

            if (!this.RawResponseData.HasValue)
            {
                if (responseType == typeof(EmptyResponse))
                {
                    this.ResponseData = new EmptyResponse();
                    return;
                }

                if (this.RequestStatus.Result)
                {
                    throw new ObsClientException("OBS Studio returned an empty response.");
                }

                return;
            }

            if (JsonSerializer.Deserialize(this.RawResponseData.Value.GetRawText(), responseType) is not IResponse response)
            {
                throw new ObsClientException("OBS Studio response could not be read.");
            }

            this.ResponseData = response;
        }
    }
}
