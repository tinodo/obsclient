namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Event Messages 
    /// </summary>
    public class EventMessage : IMessage, IJsonOnDeserialized
    {
        /// <summary>
        /// The type of Event
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("eventType")]
        public EventType EventType { get; }

        /// <summary>
        /// The Events you have subscribed to
        /// </summary>
        [JsonPropertyName("eventIntent")]
        public EventSubscriptions EventIntent { get; }

        /// <summary>
        /// The raw JSON data sent to or received from OBS Studio
        /// </summary>
        [JsonPropertyName("eventData")]
        public JsonElement? RawEventData { get; }

        /// <summary>
        /// The deserialized event
        /// </summary>
        [JsonIgnore]
        public EventArgs? EventData { get; private set; }

        /// <summary>
        /// Creates a new instance of an <see cref="EventMessage"/> object.
        /// </summary>
        /// <param name="eventType">The type of event.</param>
        /// <param name="eventIntent">The subscribed events.</param>
        /// <param name="rawEventData">The json data for the event data.</param>
        [JsonConstructor]
        public EventMessage(EventType eventType, EventSubscriptions eventIntent, JsonElement? rawEventData)
        {
            this.EventType = eventType;
            this.EventIntent = eventIntent;
            this.RawEventData = rawEventData;
        }

        /// <summary>
        /// Deserializes the raw JSON data to EventArgs.
        /// </summary>
        /// <exception cref="NotImplementedException">When the event isn't expected to hold any event data.</exception>
        /// <exception cref="ObsClientException">When deserialization failed.</exception>
        public void OnDeserialized()
        {
            if (this.RawEventData != null)
            {
                this.EventData = this.EventType switch
                {
                    // General Events
                    EventType.ExitStarted => null,
                    EventType.VendorEvent => this.RawEventData.Value.Deserialize<VendorEventEventArgs>(),
                    EventType.CustomEvent => this.RawEventData.Value.Deserialize<CustomEventEventArgs>(),

                    // Config Events
                    EventType.CurrentSceneCollectionChanging => this.RawEventData.Value.Deserialize<SceneCollectionNameEventArgs>(),
                    EventType.CurrentSceneCollectionChanged => this.RawEventData.Value.Deserialize<SceneCollectionNameEventArgs>(),
                    EventType.SceneCollectionListChanged => this.RawEventData.Value.Deserialize<SceneCollectionListEventArgs>(),
                    EventType.CurrentProfileChanging => this.RawEventData.Value.Deserialize<ProfileNameEventArgs>(),
                    EventType.CurrentProfileChanged => this.RawEventData.Value.Deserialize<ProfileNameEventArgs>(),
                    EventType.ProfileListChanged => this.RawEventData.Value.Deserialize<ProfileListEventArgs>(),

                    // Scenes Events
                    EventType.SceneCreated => this.RawEventData.Value.Deserialize<SceneModifiedEventArgs>(),
                    EventType.SceneRemoved => this.RawEventData.Value.Deserialize<SceneModifiedEventArgs>(),
                    EventType.SceneNameChanged => this.RawEventData.Value.Deserialize<SceneNameChangedEventArgs>(),
                    EventType.CurrentProgramSceneChanged => this.RawEventData.Value.Deserialize<SceneNameEventArgs>(),
                    EventType.CurrentPreviewSceneChanged => this.RawEventData.Value.Deserialize<SceneNameEventArgs>(),
                    EventType.SceneListChanged => this.RawEventData.Value.Deserialize<SceneListEventArgs>(),

                    // Inputs Events
                    EventType.InputCreated => this.RawEventData.Value.Deserialize<InputCreatedEventArgs>(),
                    EventType.InputRemoved => this.RawEventData.Value.Deserialize<InputNameEventArgs>(),
                    EventType.InputNameChanged => this.RawEventData.Value.Deserialize<InputNameChangedEventArgs>(),
                    EventType.InputActiveStateChanged => this.RawEventData.Value.Deserialize<InputActiveStateChangedEventArgs>(),
                    EventType.InputShowStateChanged => this.RawEventData.Value.Deserialize<InputShowStateChangedEventArgs>(),
                    EventType.InputMuteStateChanged => this.RawEventData.Value.Deserialize<InputMuteStateChangedEventArgs>(),
                    EventType.InputVolumeChanged => this.RawEventData.Value.Deserialize<InputVolumeChangedEventArgs>(),
                    EventType.InputAudioBalanceChanged => this.RawEventData.Value.Deserialize<InputAudioBalanceChangedEventArgs>(),
                    EventType.InputAudioSyncOffsetChanged => this.RawEventData.Value.Deserialize<InputAudioSyncOffsetChangedEventArgs>(),
                    EventType.InputAudioTracksChanged => this.RawEventData.Value.Deserialize<InputAudioTracksChangedEventArgs>(),
                    EventType.InputAudioMonitorTypeChanged => this.RawEventData.Value.Deserialize<InputAudioMonitorTypeChangedEventArgs>(),
                    EventType.InputVolumeMeters => this.RawEventData.Value.Deserialize<InputVolumeMetersEventArgs>(),

                    // Transitions Events
                    EventType.CurrentSceneTransitionChanged => this.RawEventData.Value.Deserialize<TransitionNameEventArgs>(),
                    EventType.CurrentSceneTransitionDurationChanged => this.RawEventData.Value.Deserialize<TransitionDurationEventArgs>(),
                    EventType.SceneTransitionStarted => this.RawEventData.Value.Deserialize<TransitionNameEventArgs>(),
                    EventType.SceneTransitionEnded => this.RawEventData.Value.Deserialize<TransitionNameEventArgs>(),
                    EventType.SceneTransitionVideoEnded => this.RawEventData.Value.Deserialize<TransitionNameEventArgs>(),

                    // Filters Events
                    EventType.SourceFilterListReindexed => this.RawEventData.Value.Deserialize<SourceFiltersEventArgs>(),
                    EventType.SourceFilterCreated => this.RawEventData.Value.Deserialize<SourceFilterCreatedEventArgs>(),
                    EventType.SourceFilterRemoved => this.RawEventData.Value.Deserialize<SourceFilterRemovedEventArgs>(),
                    EventType.SourceFilterNameChanged => this.RawEventData.Value.Deserialize<SourceFilterNameChangedEventArgs>(),
                    EventType.SourceFilterEnableStateChanged => this.RawEventData.Value.Deserialize<SourceFilterEnableStateChangedEventArgs>(),

                    // Scene Items Events
                    EventType.SceneItemCreated => this.RawEventData.Value.Deserialize<SceneItemCreatedEventArgs>(),
                    EventType.SceneItemRemoved => this.RawEventData.Value.Deserialize<SceneItemRemovedEventArgs>(),
                    EventType.SceneItemListReindexed => this.RawEventData.Value.Deserialize<SceneItemListReindexedEventArgs>(),
                    EventType.SceneItemEnableStateChanged => this.RawEventData.Value.Deserialize<SceneItemEnableStateChangedEventArgs>(),
                    EventType.SceneItemLockStateChanged => this.RawEventData.Value.Deserialize<SceneItemLockStateChangedEventArgs>(),
                    EventType.SceneItemSelected => this.RawEventData.Value.Deserialize<SceneItemSelectedEventArgs>(),
                    EventType.SceneItemTransformChanged => this.RawEventData.Value.Deserialize<SceneItemTransformChangedEventArgs>(),

                    // Outputs Events
                    EventType.StreamStateChanged => this.RawEventData.Value.Deserialize<OutputStateChangedEventArgs>(),
                    EventType.RecordStateChanged => this.RawEventData.Value.Deserialize<RecordStateChangedEventArgs>(),
                    EventType.ReplayBufferStateChanged => this.RawEventData.Value.Deserialize<OutputStateChangedEventArgs>(),
                    EventType.VirtualcamStateChanged => this.RawEventData.Value.Deserialize<OutputStateChangedEventArgs>(),
                    EventType.ReplayBufferSaved => this.RawEventData.Value.Deserialize<ReplayBufferSavedEventArgs>(),

                    // Media Inputs Events
                    EventType.MediaInputPlaybackStarted => this.RawEventData.Value.Deserialize<InputNameEventArgs>(),
                    EventType.MediaInputPlaybackEnded => this.RawEventData.Value.Deserialize<InputNameEventArgs>(),
                    EventType.MediaInputActionTriggered => this.RawEventData.Value.Deserialize<MediaInputActionTriggeredEventArgs>(),
                    EventType.StudioModeStateChanged => this.RawEventData.Value.Deserialize<StudioModeStateChangedEventArgs>(),
                    EventType.ScreenshotSaved => this.RawEventData.Value.Deserialize<ScreenshotSavedEventArgs>(),

                    // Otherwise...
                    _ => throw new NotImplementedException(),
                };

                if (this.EventData == null)
                {
                    throw new ObsClientException("Deserialization failure of EventResponseData.");
                }
            }
        }
    }
}
