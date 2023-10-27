﻿namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Requests;
    using OBSStudioClient.Responses;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Response Messages
    /// </summary>
    public class RequestResponseMessage : IMessage, IJsonOnDeserialized
    {
        /// <summary>
        /// Gets the type of the response based on the request type.
        /// </summary>
        private static readonly Dictionary<RequestType, Type> _responseTypeMap = new ()
        {
            // General Requests
            {RequestType.GetVersion, typeof(VersionResponse) },
            {RequestType.GetStats, typeof(StatsResponse) },
            {RequestType.CallVendorRequest, typeof(CallVendorResponse) },
            {RequestType.GetHotkeyList, typeof(HotkeysResponse) },

            // Config Requests
            {RequestType.GetPersistentData, typeof(SlotValueResponse) },
            {RequestType.GetSceneCollectionList, typeof(SceneCollectionListResponse) },
            {RequestType.GetProfileList, typeof(ProfileListResponse) },
            {RequestType.GetProfileParameter, typeof(ProfileParameterResponse) },
            {RequestType.GetVideoSettings, typeof(VideoSettingsResponse) },
            {RequestType.GetStreamServiceSettings, typeof(StreamServiceSettingsResponse) },
            {RequestType.GetRecordDirectory, typeof(RecordDirectoryResponse) },

            // Sources Requests
            {RequestType.GetSourceActive, typeof(SourceActiveResponse) },
            {RequestType.GetSourceScreenshot, typeof(ImageDataResponse) },
            {RequestType.SaveSourceScreenshot, typeof(ImageDataResponse) },

            // Scenes Requests
            {RequestType.GetSceneList, typeof(SceneListResponse) },
            {RequestType.GetGroupList, typeof(GroupsResponse) },
            {RequestType.GetCurrentProgramScene, typeof(CurrentProgramSceneNameResponse) },
            {RequestType.GetCurrentPreviewScene, typeof(CurrentPreviewSceneNameResponse) },
            {RequestType.GetSceneSceneTransitionOverride, typeof(SceneTransitionResponse) },

            // Inputs Requests
            {RequestType.GetInputList, typeof(InputsResponse) },
            {RequestType.GetInputKindList, typeof(InputKindsResponse) },
            {RequestType.GetSpecialInputs, typeof(SpecialInputsResponse) },
            {RequestType.CreateInput, typeof(SceneItemIdResponse) },
            {RequestType.GetInputDefaultSettings, typeof(DefaultInputSettingsResponse) },
            {RequestType.GetInputSettings, typeof(InputSettingsResponse) },
            {RequestType.GetInputMute, typeof(InputMutedResponse) },
            {RequestType.ToggleInputMute, typeof(InputMutedResponse) },
            {RequestType.GetInputVolume, typeof(InputVolumeResponse) },
            {RequestType.GetInputAudioBalance, typeof(InputAudioBalanceResponse) },
            {RequestType.GetInputAudioSyncOffset, typeof(InputAudioSyncOffsetResponse) },
            {RequestType.GetInputAudioMonitorType, typeof(InputAudioMonitorTypeResponse) },
            {RequestType.GetInputAudioTracks, typeof(InputAudioTracksResponse) },
            {RequestType.GetInputPropertiesListPropertyItems, typeof(PropertyItemsResponse) },

            // Transitions Requests
            {RequestType.GetTransitionKindList, typeof(TransitionKindsResponse) },
            {RequestType.GetSceneTransitionList, typeof(SceneTransitionListResponse) },
            {RequestType.GetCurrentSceneTransition, typeof(TransitionResponse) },
            {RequestType.GetCurrentSceneTransitionCursor, typeof(TransitionCursorResponse) },

            // Filters Requests
            {RequestType.GetSourceFilterList, typeof(FiltersResponse) },
            {RequestType.GetSourceFilterDefaultSettings, typeof(DefaultFilterSettingsResponse) },
            {RequestType.GetSourceFilter, typeof(SourceFilterResponse) },

            // Scene Items Requests
            {RequestType.GetSceneItemList, typeof(SceneItemsResponse) },
            {RequestType.GetGroupSceneItemList, typeof(SceneItemsResponse) },
            {RequestType.GetSceneItemId, typeof(SceneItemIdResponse) },
            {RequestType.CreateSceneItem, typeof(SceneItemIdResponse) },
            {RequestType.DuplicateSceneItem, typeof(SceneItemIdResponse) },
            {RequestType.GetSceneItemTransform, typeof(SceneItemIdResponse) },
            {RequestType.GetSceneItemEnabled, typeof(SceneItemEnabledResponse) },
            {RequestType.GetSceneItemLocked, typeof(SceneItemlockedResponse) },
            {RequestType.GetSceneItemIndex, typeof(SceneItemIndexResponse) },
            {RequestType.GetSceneItemBlendMode, typeof(SceneItemBlendModeResponse) },

            // Outputs Requests
            {RequestType.GetVirtualCamStatus, typeof(OutputActiveResponse) },
            {RequestType.ToggleVirtualCam, typeof(OutputActiveResponse) },
            {RequestType.GetReplayBufferStatus, typeof(OutputActiveResponse) },
            {RequestType.ToggleReplayBuffer, typeof(OutputActiveResponse) },
            {RequestType.GetLastReplayBufferReplay, typeof(SavedReplayPathResponse) },
            {RequestType.GetOutputList, typeof(OutputsResponse) },
            {RequestType.GetOutputStatus, typeof(OutputStatusResponse) },
            {RequestType.ToggleOutput, typeof(OutputActiveResponse) },
            {RequestType.GetOutputSettings, typeof(OutputSettingsResponse) },

            // Stream Requests
            {RequestType.GetStreamStatus, typeof(OutputStatusResponse) },
            {RequestType.ToggleStream, typeof(OutputActiveResponse) },

            // Record Requests
            {RequestType.GetRecordStatus, typeof(RecordStatusResponse) },
            {RequestType.ToggleRecord, typeof(OutputActiveResponse) },
            {RequestType.StopRecord, typeof(OutputPathResponse) },
            {RequestType.ToggleRecordPause, typeof(OutputPausedResponse) },

            // Media Inputs Requests
            {RequestType.GetMediaInputStatus, typeof(MediaInputStatusResponse) },

            // Ui Requests
            {RequestType.GetStudioModeEnabled, typeof(StudioModeEnabledResponse) },
            {RequestType.GetMonitorList, typeof(MonitorListResponse) },
        };

        /// <summary>
        /// The type of request this is a response to.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("requestType")]
        public RequestType RequestType { get; }

        /// <summary>
        /// The identifier of the original request.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        /// <summary>
        /// The status of the request.
        /// </summary>
        [JsonPropertyName("requestStatus")]
        public RequestStatus RequestStatus { get; }

        /// <summary>
        /// The raw JSON response data
        /// </summary>
        [JsonPropertyName("responseData")]
        public JsonElement? RawResponseData { get; }

        /// <summary>
        /// The deserialized response data.
        /// </summary>
        [JsonIgnore]
        public IResponse? ResponseData { get; private set; }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestResponseMessage"/>
        /// </summary>
        /// <param name="requestType">The type of request this is a response to.</param>
        /// <param name="requestId">The identifier of the original request.</param>
        /// <param name="requestStatus">The status of the request.</param>
        /// <param name="rawResponseData"> The raw JSON response data</param>
        [JsonConstructor]
        public RequestResponseMessage(RequestType requestType, string requestId, RequestStatus requestStatus, JsonElement? rawResponseData)
        {
            this.RequestType = requestType;
            this.RequestId = requestId;
            this.RequestStatus = requestStatus;
            this.RawResponseData = rawResponseData;
        }

        /// <summary>
        /// Deserializes the raw JSON response to Response Data
        /// </summary>
        /// <exception cref="NotImplementedException">Not implemented.</exception>
        /// <exception cref="NotSupportedException">The response data is unknown.</exception>
        /// <exception cref="ObsClientException">Deserialization failed</exception>
        public void OnDeserialized()
        {
            if (!this.RawResponseData.HasValue)
            {
                if (this.RequestStatus.Result && _responseTypeMap.ContainsKey(this.RequestType))
                {
                    throw new ObsClientException("OBS Studio returned an empty response.");
                }

                return;
            }

            if (!_responseTypeMap.TryGetValue(this.RequestType, out Type? responseType))
            {
                throw new ObsClientException("OBS Studio response could not be mapped.");
            }

            if (JsonSerializer.Deserialize(this.RawResponseData.Value.GetRawText(), responseType) is not IResponse response)
            {
                throw new ObsClientException("OBS Studio response could not be read.");
            }

            this.ResponseData = response;

            /*
             * V1
             * 

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
                    RequestType.GetCurrentSceneTransition => this.RawResponseData.Value.Deserialize<TransitionResponse>(),
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
                    RequestType.ToggleRecord => this.RawResponseData.Value.Deserialize<OutputActiveResponse>(),
                    RequestType.StartRecord => null,
                    RequestType.StopRecord => this.RawResponseData.Value.Deserialize<OutputPathResponse>(),
                    RequestType.ToggleRecordPause => this.RawResponseData.Value.Deserialize<OutputPausedResponse>(),
                    RequestType.PauseRecord => null,
                    RequestType.ResumeRecord => null,
                    RequestType.SetRecordDirectory => null,

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

            */
        }
    }
}