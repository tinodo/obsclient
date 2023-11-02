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
    public class RequestResponseMessage : IMessage, IJsonOnDeserialized
    {
        /// <summary>
        /// Gets the type of the response based on the request type.
        /// </summary>
        private static readonly Dictionary<RequestType, Type> _responseTypeMap = new()
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
        }
    }
}
