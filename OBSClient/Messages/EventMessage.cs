namespace OBSStudioClient.Messages
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
        /// Gets the type of the event based on the request type.
        /// </summary>
        private static readonly Dictionary<EventType, Type> _responseTypeMap = new()
        { 
            // General Events
            { EventType.VendorEvent, typeof(VendorEventEventArgs) },
            { EventType.CustomEvent, typeof(CustomEventEventArgs) },

            // Config Events
            { EventType.CurrentSceneCollectionChanging, typeof(SceneCollectionNameEventArgs) },
            { EventType.CurrentSceneCollectionChanged, typeof(SceneCollectionNameEventArgs) },
            { EventType.SceneCollectionListChanged, typeof(SceneCollectionListEventArgs) },
            { EventType.CurrentProfileChanging, typeof(ProfileNameEventArgs) },
            { EventType.CurrentProfileChanged, typeof(ProfileNameEventArgs) },
            { EventType.ProfileListChanged, typeof(ProfileListEventArgs) },

            // Scenes Events
            { EventType.SceneCreated, typeof(SceneModifiedEventArgs) },
            { EventType.SceneRemoved, typeof(SceneModifiedEventArgs) },
            { EventType.SceneNameChanged, typeof(SceneNameChangedEventArgs) },
            { EventType.CurrentProgramSceneChanged, typeof(SceneNameEventArgs) },
            { EventType.CurrentPreviewSceneChanged, typeof(SceneNameEventArgs) },
            { EventType.SceneListChanged, typeof(SceneListEventArgs) },

            // Inputs Events
            { EventType.InputCreated, typeof(InputCreatedEventArgs) },
            { EventType.InputRemoved, typeof(InputNameEventArgs) },
            { EventType.InputNameChanged, typeof(InputNameChangedEventArgs) },
            { EventType.InputActiveStateChanged, typeof(InputActiveStateChangedEventArgs) },
            { EventType.InputShowStateChanged, typeof(InputShowStateChangedEventArgs) },
            { EventType.InputMuteStateChanged, typeof(InputMuteStateChangedEventArgs) },
            { EventType.InputVolumeChanged, typeof(InputVolumeChangedEventArgs) },
            { EventType.InputAudioBalanceChanged, typeof(InputAudioBalanceChangedEventArgs) },
            { EventType.InputAudioSyncOffsetChanged, typeof(InputAudioSyncOffsetChangedEventArgs) },
            { EventType.InputAudioTracksChanged, typeof(InputAudioTracksChangedEventArgs) },
            { EventType.InputAudioMonitorTypeChanged, typeof(InputAudioMonitorTypeChangedEventArgs) },
            { EventType.InputVolumeMeters, typeof(InputVolumeMetersEventArgs) },

            // Transitions Events
            { EventType.CurrentSceneTransitionChanged, typeof(TransitionNameEventArgs) },
            { EventType.CurrentSceneTransitionDurationChanged, typeof(TransitionDurationEventArgs) },
            { EventType.SceneTransitionStarted, typeof(TransitionNameEventArgs) },
            { EventType.SceneTransitionEnded, typeof(TransitionNameEventArgs) },
            { EventType.SceneTransitionVideoEnded, typeof(TransitionNameEventArgs) },

            // Filters Events
            { EventType.SourceFilterListReindexed, typeof(SourceFiltersEventArgs) },
            { EventType.SourceFilterCreated, typeof(SourceFilterCreatedEventArgs) },
            { EventType.SourceFilterRemoved, typeof(SourceFilterRemovedEventArgs) },
            { EventType.SourceFilterNameChanged, typeof(SourceFilterNameChangedEventArgs) },
            { EventType.SourceFilterEnableStateChanged, typeof(SourceFilterEnableStateChangedEventArgs) },

            // Scene Items Events
            { EventType.SceneItemCreated, typeof(SceneItemCreatedEventArgs) },
            { EventType.SceneItemRemoved, typeof(SceneItemRemovedEventArgs) },
            { EventType.SceneItemListReindexed, typeof(SceneItemListReindexedEventArgs) },
            { EventType.SceneItemEnableStateChanged, typeof(SceneItemEnableStateChangedEventArgs) },
            { EventType.SceneItemLockStateChanged, typeof(SceneItemLockStateChangedEventArgs) },
            { EventType.SceneItemSelected, typeof(SceneItemSelectedEventArgs) },
            { EventType.SceneItemTransformChanged, typeof(SceneItemTransformChangedEventArgs) },

            // Outputs Events
            { EventType.StreamStateChanged, typeof(OutputStateChangedEventArgs) },
            { EventType.RecordStateChanged, typeof(RecordStateChangedEventArgs) },
            { EventType.ReplayBufferStateChanged, typeof(OutputStateChangedEventArgs) },
            { EventType.VirtualcamStateChanged, typeof(OutputStateChangedEventArgs) },
            { EventType.ReplayBufferSaved, typeof(ReplayBufferSavedEventArgs) },

            // Media Inputs Events
            { EventType.MediaInputPlaybackStarted, typeof(InputNameEventArgs) },
            { EventType.MediaInputPlaybackEnded, typeof(InputNameEventArgs) },
            { EventType.MediaInputActionTriggered, typeof(MediaInputActionTriggeredEventArgs) },
            { EventType.StudioModeStateChanged, typeof(StudioModeStateChangedEventArgs) },
            { EventType.ScreenshotSaved, typeof(ScreenshotSavedEventArgs) },
        };

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
        public EventArgs EventData { get; private set; }

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
            this.EventData = EventArgs.Empty;
        }

        /// <summary>
        /// Deserializes the raw JSON data to EventArgs.
        /// </summary>
        /// <exception cref="NotImplementedException">When the event isn't expected to hold any event data.</exception>
        /// <exception cref="ObsClientException">When deserialization failed.</exception>
        public void OnDeserialized()
        {
            if (!this.RawEventData.HasValue)
            {
                if (_responseTypeMap.ContainsKey(this.EventType))
                {
                    throw new ObsClientException("OBS Studio returned an empty event.");
                }

                return;
            }

            if (!_responseTypeMap.TryGetValue(this.EventType, out Type? responseType))
            {
                throw new ObsClientException("OBS Studio event could not be mapped.");
            }

            if (JsonSerializer.Deserialize(this.RawEventData.Value.GetRawText(), responseType) is not EventArgs eventArgs)
            {
                throw new ObsClientException("OBS Studio event could not read.");
            }

            this.EventData = eventArgs;
        }
    }
}
