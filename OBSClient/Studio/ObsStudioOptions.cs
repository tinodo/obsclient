namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;

    /// <summary>
    /// The settings of an <see cref="ObsStudio"/>.
    /// </summary>
    public sealed class ObsStudioOptions
    {
        /// <summary>
        /// Gets or sets whether a name also matches when it differs only by case, if that leaves one object. An exact match always wins.
        /// The default is false: names are case sensitive, as they are in OBS Studio.
        /// </summary>
        public bool IgnoreCase { get; set; }

        /// <summary>
        /// Gets or sets whether the connection is restored when it is lost, as <see cref="ObsClient.AutoReconnect"/> does. The default is true: the model keeps
        /// the last values that it knew while the connection is down (see <see cref="ObsStudio.IsStale"/>), and reads OBS Studio again when it is back.
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// Gets or sets how long <see cref="ObsStudio.ConnectAsync"/> waits for the model to be complete, after the connection is made. The default is 30 seconds.
        /// A very large collection of scenes needs a few seconds; one second is enough for a hundred scenes with a thousand scene items.
        /// </summary>
        public TimeSpan SynchronizationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets how long a read of the model may take. The default is 10 seconds. (The requests that you make through <see cref="ObsStudio.Client"/>
        /// have the time limit of that client.)
        /// </summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the events that OBS Studio sends in addition to the ones the model needs, for your own handlers on <see cref="ObsStudio.Client"/>.
        /// The default is none. The events that the model needs are always subscribed to.
        /// </summary>
        public EventSubscriptions AdditionalEvents { get; set; } = EventSubscriptions.None;

        /// <summary>
        /// Gets or sets the context that the notifications (<see cref="ObsStudio.Changed"/> and the PropertyChanged events) are raised on, for
        /// example the context of a user interface thread. When it is null, they are raised on a thread of the thread pool, one at a time, in order.
        /// </summary>
        /// <remarks>
        /// The model itself is never changed on this context: it has a processing loop of its own. A handler that blocks holds up only the notifications behind it.
        /// </remarks>
        public SynchronizationContext? NotificationContext { get; set; }

        /// <summary>
        /// Gets or sets the source of time, for the waits of the model. The default is <see cref="TimeProvider.System"/>. Tests can pass their own.
        /// </summary>
        public TimeProvider? TimeProvider { get; set; }

        // A copy, so that an ObsStudio is not changed by what the application does to the options afterwards.
        internal ObsStudioOptions Clone()
        {
            return (ObsStudioOptions)this.MemberwiseClone();
        }
    }
}
