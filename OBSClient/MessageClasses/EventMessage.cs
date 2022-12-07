namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Messages;
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class EventMessage : IMessageData, IJsonOnDeserialized
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("eventType")]
        public EventType EventType { get; }

        [JsonPropertyName("eventIntent")]
        public EventSubscriptions EventIntent { get; }

        [JsonPropertyName("eventData")]
        public JsonElement? RawEventData { get; }

        [JsonIgnore]
        public EventArgs? EventData { get; private set; }

        [JsonConstructor]
        public EventMessage(EventType eventType, EventSubscriptions eventIntent, JsonElement? rawEventData)
        {
            EventType = eventType;
            EventIntent = eventIntent;
            RawEventData = rawEventData;
        }

        public void OnDeserialized()
        {
            if (RawEventData != null)
            {
                EventData = EventType switch
                {
                    // General Events
                    EventType.ExitStarted => null,
                    EventType.VendorEvent => RawEventData.Value.Deserialize<VendorEventEventArgs>(),
                    EventType.CustomEvent => RawEventData.Value.Deserialize<CustomEventEventArgs>(),

                    // Config Events
                    EventType.CurrentSceneCollectionChanging => RawEventData.Value.Deserialize<SceneCollectionNameEventArgs>(),
                    EventType.CurrentSceneCollectionChanged => RawEventData.Value.Deserialize<SceneCollectionNameEventArgs>(),
                    EventType.SceneCollectionListChanged => RawEventData.Value.Deserialize<SceneCollectionListEventArgs>(),
                    EventType.CurrentProfileChanging => RawEventData.Value.Deserialize<ProfileNameEventArgs>(),
                    EventType.CurrentProfileChanged => RawEventData.Value.Deserialize<ProfileNameEventArgs>(),
                    EventType.ProfileListChanged => RawEventData.Value.Deserialize<ProfileListEventArgs>(),

                    // Scenes Events
                    EventType.SceneCreated => RawEventData.Value.Deserialize<SceneModifiedEventArgs>(),
                    EventType.SceneRemoved => RawEventData.Value.Deserialize<SceneModifiedEventArgs>(),
                    EventType.SceneNameChanged => RawEventData.Value.Deserialize<SceneNameChangedEventArgs>(),
                    EventType.CurrentProgramSceneChanged => RawEventData.Value.Deserialize<SceneNameEventArgs>(),
                    EventType.CurrentPreviewSceneChanged => RawEventData.Value.Deserialize<SceneNameEventArgs>(),
                    EventType.SceneListChanged => RawEventData.Value.Deserialize<SceneListEventArgs>(),

                    // Inputs Events
                    EventType.InputCreated => RawEventData.Value.Deserialize<InputCreatedEventArgs>(),
                    EventType.InputRemoved => RawEventData.Value.Deserialize<InputNameEventArgs>(),
                    EventType.InputNameChanged => RawEventData.Value.Deserialize<InputNameChangedEventArgs>(),
                    EventType.InputActiveStateChanged => RawEventData.Value.Deserialize<InputActiveStateChangedEventArgs>(),
                    EventType.InputShowStateChanged => RawEventData.Value.Deserialize<InputShowStateChangedEventArgs>(),
                    EventType.InputMuteStateChanged => RawEventData.Value.Deserialize<InputMuteStateChangedEventArgs>(),
                    EventType.InputVolumeChanged => RawEventData.Value.Deserialize<InputVolumeChangedEventArgs>(),
                    EventType.InputAudioBalanceChanged => RawEventData.Value.Deserialize<InputAudioBalanceChangedEventArgs>(),
                    EventType.InputAudioSyncOffsetChanged => RawEventData.Value.Deserialize<InputAudioSyncOffsetChangedEventArgs>(),
                    EventType.InputAudioTracksChanged => RawEventData.Value.Deserialize<InputAudioTracksChangedEventArgs>(),
                    EventType.InputAudioMonitorTypeChanged => RawEventData.Value.Deserialize<InputAudioMonitorTypeChangedEventArgs>(),
                    EventType.InputVolumeMeters => RawEventData.Value.Deserialize<InputVolumeMetersEventArgs>(),

                    // Transitions Events
                    EventType.CurrentSceneTransitionChanged => RawEventData.Value.Deserialize<TransitionNameEventArgs>(),
                    EventType.CurrentSceneTransitionDurationChanged => RawEventData.Value.Deserialize<TransitionDurationEventArgs>(),
                    EventType.SceneTransitionStarted => RawEventData.Value.Deserialize<TransitionNameEventArgs>(),
                    EventType.SceneTransitionEnded => RawEventData.Value.Deserialize<TransitionNameEventArgs>(),
                    EventType.SceneTransitionVideoEnded => RawEventData.Value.Deserialize<TransitionNameEventArgs>(),

                    // Filters Events
                    EventType.SourceFilterListReindexed => RawEventData.Value.Deserialize<SourceFiltersEventArgs>(),
                    EventType.SourceFilterCreated => RawEventData.Value.Deserialize<SourceFilterCreatedEventArgs>(),
                    EventType.SourceFilterRemoved => RawEventData.Value.Deserialize<SourceFilterRemovedEventArgs>(),
                    EventType.SourceFilterNameChanged => RawEventData.Value.Deserialize<SourceFilterNameChangedEventArgs>(),
                    EventType.SourceFilterEnableStateChanged => RawEventData.Value.Deserialize<SourceFilterEnableStateChangedEventArgs>(),

                    // Scene Items Events
                    EventType.SceneItemCreated => RawEventData.Value.Deserialize<SceneItemCreatedEventArgs>(),
                    EventType.SceneItemRemoved => RawEventData.Value.Deserialize<SceneItemRemovedEventArgs>(),
                    EventType.SceneItemListReindexed => RawEventData.Value.Deserialize<SceneItemListReindexedEventArgs>(),
                    EventType.SceneItemEnableStateChanged => RawEventData.Value.Deserialize<SceneItemEnableStateChangedEventArgs>(),
                    EventType.SceneItemLockStateChanged => RawEventData.Value.Deserialize<SceneItemLockStateChangedEventArgs>(),
                    EventType.SceneItemSelected => RawEventData.Value.Deserialize<SceneItemSelectedEventArgs>(),
                    EventType.SceneItemTransformChanged => RawEventData.Value.Deserialize<SceneItemTransformChangedEventArgs>(),

                    // Outputs Events
                    EventType.StreamStateChanged => RawEventData.Value.Deserialize<OutputStateChangedEventArgs>(),
                    EventType.RecordStateChanged => RawEventData.Value.Deserialize<RecordStateChangedEventArgs>(),
                    EventType.ReplayBufferStateChanged => RawEventData.Value.Deserialize<OutputStateChangedEventArgs>(),
                    EventType.VirtualcamStateChanged => RawEventData.Value.Deserialize<OutputStateChangedEventArgs>(),
                    EventType.ReplayBufferSaved => RawEventData.Value.Deserialize<ReplayBufferSavedEventArgs>(),

                    // Media Inputs Events
                    EventType.MediaInputPlaybackStarted => RawEventData.Value.Deserialize<InputNameEventArgs>(),
                    EventType.MediaInputPlaybackEnded => RawEventData.Value.Deserialize<InputNameEventArgs>(),
                    EventType.MediaInputActionTriggered => RawEventData.Value.Deserialize<MediaInputActionTriggeredEventArgs>(),
                    EventType.StudioModeStateChanged => RawEventData.Value.Deserialize<StudioModeStateChangedEventArgs>(),
                    EventType.ScreenshotSaved => RawEventData.Value.Deserialize<ScreenshotSavedEventArgs>(),

                    // Otherwise...
                    _ => throw new NotImplementedException(),
                };

                if (EventData == null)
                {
                    throw new OBSClientException("Deserialization failure of EventResponseData.");
                }
            }
        }
    }
}
