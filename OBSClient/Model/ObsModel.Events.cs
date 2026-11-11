namespace OBSStudioClient.Model
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Events;

    public sealed partial class ObsModel
    {
        private volatile bool _isExiting;

        /// <summary>
        /// Gets whether OBS Studio said that it is shutting down (see <see cref="ExitStarted"/>). It tells a connection that ended because OBS Studio was closed from one that was lost: when the
        /// state is <see cref="ObsModelState.Reconnecting"/> and this is true, OBS Studio was closed.
        /// </summary>
        /// <remarks>
        /// It turns back to false when the model is connected to an OBS Studio again (the one that was started again). PropertyChanged and <see cref="Changed"/> tell when it changes.
        /// </remarks>
        public bool IsExiting => this._isExiting;

        // Brings the property in line with the model. Returns whether it changed.
        internal bool SetExiting(bool exiting, out bool previous)
        {
            previous = this._isExiting;
            if (previous == exiting)
            {
                return false;
            }

            this._isExiting = exiting;
            return true;
        }
        /// <summary>
        /// Occurs when somebody sends a custom event: a program that called <see cref="BroadcastCustomEventAsync"/> (this one included), or a script of OBS Studio.
        /// </summary>
        /// <remarks>
        /// <para>
        /// OBS Studio sends the event to every client that is subscribed to the <see cref="OBSStudioClient.Enums.EventSubscriptions.General"/> events, which the model always is. The data of
        /// the event is what the sender made it.
        /// </para>
        /// <para>
        /// Like <see cref="Changed"/> it is raised in the order in which the events arrived, one at a time, on <see cref="ObsModelOptions.NotificationContext"/> (or a thread of the
        /// thread pool), and also while the model is being read. The same event is available as <see cref="ObsClient.CustomEvent"/> of <see cref="Client"/>, which the client raises from a dispatcher of its own.
        /// </para>
        /// </remarks>
        public event EventHandler<CustomEventEventArgs>? CustomEvent;

        /// <summary>
        /// Occurs when a plugin or a script of OBS Studio sends an event of its own (a vendor event). It is only raised when you subscribed to the vendor events, with
        /// <see cref="ObsModelOptions.AdditionalEvents"/> (<see cref="OBSStudioClient.Enums.EventSubscriptions.Vendors"/>): the model does not need them, and a plugin can send a great many.
        /// </summary>
        /// <remarks>
        /// It is raised like <see cref="CustomEvent"/>: in order, on <see cref="ObsModelOptions.NotificationContext"/>. The plugin or script says what its events mean.
        /// </remarks>
        public event EventHandler<VendorEventEventArgs>? VendorEvent;

        /// <summary>
        /// Occurs when OBS Studio begins to shut down. The connection ends a moment later, and with <see cref="ObsModelOptions.AutoReconnect"/> the model is then
        /// <see cref="ObsModelState.Reconnecting"/> until OBS Studio is back: this event tells that OBS Studio was closed, and not that the connection was lost.
        /// </summary>
        /// <remarks>
        /// <see cref="IsExiting"/> is true when the event is raised, and stays true until a connection is made to an OBS Studio that was started again. It is raised like
        /// <see cref="CustomEvent"/>: in order, on <see cref="ObsModelOptions.NotificationContext"/>.
        /// </remarks>
        public event EventHandler? ExitStarted;

        /// <summary>
        /// Occurs when OBS Studio has saved a screenshot that was asked for in OBS Studio itself (the hotkey for a screenshot, or its menu), with the path of the file.
        /// </summary>
        /// <remarks>
        /// OBS Studio does not send it for a screenshot that a request makes (<see cref="ObsSource.SaveScreenshotAsync"/> returns when the file is written). It is raised like
        /// <see cref="CustomEvent"/>: in order, on <see cref="ObsModelOptions.NotificationContext"/>.
        /// </remarks>
        public event EventHandler<ScreenshotSavedEventArgs>? ScreenshotSaved;

        /// <summary>
        /// Occurs when a scene item is selected in OBS Studio (somebody clicked it in the list of scene items or in the preview, or OBS Studio selected one that was just made, which it does for every new scene item).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The model does not keep which scene item is selected: OBS Studio does not say when one is not selected any more, and there is no request that asks. It is what happened, told in the order in which it
        /// happened. A scene item that was just made is selected at once, and the model tells of it after it has told (<see cref="Changed"/>) that the scene item exists.
        /// </para>
        /// <para>
        /// It is raised like <see cref="CustomEvent"/>: in order, on <see cref="ObsModelOptions.NotificationContext"/>.
        /// </para>
        /// </remarks>
        public event EventHandler<ObsSceneItemEventArgs>? SceneItemSelected;

        /// <summary>
        /// Occurs when OBS Studio says that the settings of a filter changed, with the filter and the settings that it has now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>OBS Studio does not send this event, so do not wait for it:</b> libobs tells of the updates of public sources only, and a filter is a private source, so OBS Studio 33.0.0 sent none for any filter in a test, however the
        /// settings were changed. The protocol has the event (<c>SourceFilterSettingsChanged</c>), and the model passes it on when a server does send it. The model does not keep the settings of a filter either, as a copy would go out of date
        /// without a sign: ask for them with <see cref="ObsFilter.GetSettingsAsync"/>.
        /// </para>
        /// <para>
        /// The event says the source by its name only. It is not raised when a scene and an input have the same name and both have a filter with the name that it says: it would be raised for a filter whose settings did not change.
        /// It is raised like <see cref="CustomEvent"/>: in order, on <see cref="ObsModelOptions.NotificationContext"/>.
        /// </para>
        /// </remarks>
        public event EventHandler<ObsFilterSettingsEventArgs>? FilterSettingsChanged;

        /// <summary>
        /// Occurs when OBS Studio sent new levels of the audio of the inputs that are on the air (twenty times a second, as long as they change). It is only raised when the model was asked to follow them
        /// (<see cref="ObsModelOptions.HighVolume"/> with <see cref="ObsHighVolume.AudioLevels"/>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The levels are a stream, not a history: what matters is how loud it is now. When the handlers of the application are slower than OBS Studio, the levels that came meanwhile are not raised
        /// one by one: the event is raised once with the newest levels, and <see cref="ObsAudioLevelsEventArgs.Ended"/> has every input that stopped being listed in between. The levels never wait in
        /// the queue of the other notifications of the model, and never fill it. A message that says what the previous one said (a silence that stays silent) is not raised.
        /// </para>
        /// <para>
        /// It is raised on the lane of the other notifications of the model: one at a time, never while another notification is being raised, on <see cref="ObsModelOptions.NotificationContext"/> (or a thread of the thread pool). Its place in that lane is the place of the first
        /// message that came after it was last raised, so it can come after the notification of a change that happened after that message. The PropertyChanged event of each input (<see cref="ObsInput.Levels"/>) follows it. The levels are not told by <see cref="Changed"/>.
        /// </para>
        /// </remarks>
        public event EventHandler<ObsAudioLevelsEventArgs>? AudioLevels;

        internal void RaiseCustomEvent(CustomEventEventArgs args)
        {
            this.RaiseEvent(this.CustomEvent, args, nameof(this.CustomEvent));
        }

        internal void RaiseVendorEvent(VendorEventEventArgs args)
        {
            this.RaiseEvent(this.VendorEvent, args, nameof(this.VendorEvent));
        }

        internal void RaiseScreenshotSaved(ScreenshotSavedEventArgs args)
        {
            this.RaiseEvent(this.ScreenshotSaved, args, nameof(this.ScreenshotSaved));
        }

        internal void RaiseSceneItemSelected(ObsSceneItemEventArgs args)
        {
            this.RaiseEvent(this.SceneItemSelected, args, nameof(this.SceneItemSelected));
        }

        internal void RaiseFilterSettingsChanged(ObsFilterSettingsEventArgs args)
        {
            this.RaiseEvent(this.FilterSettingsChanged, args, nameof(this.FilterSettingsChanged));
        }

        internal void RaiseAudioLevels(ObsAudioLevelsEventArgs args)
        {
            this.RaiseEvent(this.AudioLevels, args, nameof(this.AudioLevels));
        }

        internal void RaiseExitStarted()
        {
            var handler = this.ExitStarted;
            if (handler is null)
            {
                return;
            }

            foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    subscriber(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Exception in a handler of {Event}", nameof(this.ExitStarted));
                }
            }
        }

        // Raises an event of the model, or of one of its objects (sender), so that a handler that throws does not keep the other handlers from running.
        internal void RaiseEvent<TArgs>(EventHandler<TArgs>? handler, TArgs args, string name, object? sender = null)
        {
            if (handler is null)
            {
                return;
            }

            foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<TArgs>>())
            {
                try
                {
                    subscriber(sender ?? this, args);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Exception in a handler of {Event}", name);
                }
            }
        }
    }
}
