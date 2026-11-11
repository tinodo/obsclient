namespace OBSStudioClient.Model
{
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;
    using ObsMonitor = OBSStudioClient.Classes.Monitor;

    // What OBS Studio can be asked or told that is not about a scene, an input or an output: its statistics, its hotkeys, its monitors, custom events and vendors, projectors.
    public sealed partial class ObsModel
    {
        /// <summary>
        /// Asks OBS Studio for its statistics: the load on the computer, the frame rate, the frames that were skipped, and the traffic of this connection.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The statistics at the moment that OBS Studio answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The statistics move all the time, so the model does not keep them: ask when you need them. The request is made in its turn, after the changes that were asked for before it
        /// (see <see cref="ObsSceneItem.SetEnabledAsync"/>).
        /// </remarks>
        public Task<StatsResponse> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<StatsResponse>(
                new WriteJob
                {
                    Description = "asking for the statistics of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetStats, null)],
                    ReadResponses = static responses => (StatsResponse)responses[0].ResponseData!,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the names of its hotkeys.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The names, as <see cref="TriggerHotkeyAsync(string, string?, CancellationToken)"/> takes them (for example <c>ReplayBuffer.Save</c>).</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The hotkeys that belong to a source (mute, push to talk, show and hide) are in the list with the name of the hotkey only, and the source that has it is not said: pass the source as the
        /// context of <see cref="TriggerHotkeyAsync(string, string?, CancellationToken)"/> (or use <see cref="ObsSource.TriggerHotkeyAsync"/> of the source). In most cases there is a better way to do what a hotkey does: a method of the model that does it.
        /// </remarks>
        public Task<IReadOnlyList<string>> GetHotkeysAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<IReadOnlyList<string>>(
                new WriteJob
                {
                    Description = "asking for the hotkeys of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetHotkeyList, null)],
                    ReadResponses = static responses => ((HotkeysResponse)responses[0].ResponseData!).Hotkeys,
                },
                cancellationToken);
        }

        /// <summary>
        /// Triggers a hotkey of OBS Studio by its name, as if the key was pressed and released, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="name">The name of the hotkey: one of <see cref="GetHotkeysAsync"/>.</param>
        /// <param name="context">The name of the source, output, encoder or service that has the hotkey, for a hotkey that belongs to one, or null for the hotkeys of OBS Studio itself.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it triggered the hotkey.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: it has no hotkey of that name (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.ResourceNotFound"/>).</exception>
        /// <remarks>
        /// What the hotkey does is up to OBS Studio, and the model learns of it as it learns of anything else that happens in OBS Studio: through its events. So the call does not wait for the
        /// model. obs-websocket advises to use another request wherever there is one: a method of the model that does the same is the better choice. See <see cref="ObsSceneItem.SetEnabledAsync"/>
        /// for the order of changes.
        /// </remarks>
        public Task TriggerHotkeyAsync(string name, string? context = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"triggering the hotkey '{name}'",
                    BuildRequests = () => [new WriteRequest(RequestType.TriggerHotkeyByName, TriggerHotkeyByNameRequestData.Create(name, context))],
                    Map = static (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound ? new ObsResponseException(response.RequestStatus) : null,
                },
                cancellationToken);
        }

        /// <summary>
        /// Presses a key in OBS Studio, with the modifier keys that you give, and releases it, so that the hotkeys of OBS Studio that this key sequence is set for react. It returns when OBS Studio has answered.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifier keys that are held while the key is pressed.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it pressed the key.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the key is none and no modifier is given (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.CannotAct"/>).</exception>
        /// <remarks>
        /// OBS Studio does not say whether a hotkey reacted. See <see cref="TriggerHotkeyAsync(string, string?, CancellationToken)"/> for what to know about hotkeys, and <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of
        /// changes.
        /// </remarks>
        public Task TriggerHotkeyAsync(ObsKey key, KeyModifier modifiers = KeyModifier.None, CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"pressing the key {key}{(modifiers == KeyModifier.None ? string.Empty : $" with {modifiers}")}",
                    BuildRequests = () => [new WriteRequest(RequestType.TriggerHotkeyByKeySequence, new { keyId = key, keyModifiers = new KeyModifiers(modifiers) })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Sends a custom event to every client of OBS Studio that listens to the general events, this model included, and returns when OBS Studio has accepted it.
        /// </summary>
        /// <param name="data">The data of the event: an object (the fields and their values), for example <c>new { source = "my app", message = "hello" }</c>, or a <see cref="JsonElement"/>. It can not be empty.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="data"/> is not an object, or can not be written as JSON.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it sent the event.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request (the data is empty, for one).</exception>
        /// <remarks>
        /// The event arrives as <see cref="CustomEvent"/>, here and in every other program that is connected. It is a way for programs that control one OBS Studio to talk to each other. See
        /// <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task BroadcastCustomEventAsync(object data, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            var eventData = ModelJson.AsObject(data, nameof(data), "the data of a custom event", "new { source = \"my app\", message = \"hello\" }");
            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "sending a custom event",
                    BuildRequests = () => [new WriteRequest(RequestType.BroadcastCustomEvent, BroadcastCustomEventRequestData.Create(eventData))],
                },
                cancellationToken);
        }

        /// <summary>
        /// Calls a request that a plugin or a script of OBS Studio registered (a vendor request), and returns what it answered.
        /// </summary>
        /// <param name="vendor">The name of the vendor: the plugin or script that registered the request.</param>
        /// <param name="request">The type of the request, as the vendor names it.</param>
        /// <param name="data">The data of the request, as the vendor wants it: an object, or a <see cref="JsonElement"/>, or null for none.</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The answer: the vendor and the request that were called, and the data that the vendor answered.</returns>
        /// <exception cref="ArgumentException"><paramref name="vendor"/> or <paramref name="request"/> is empty, or <paramref name="data"/> is not an object, or can not be written as JSON.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known what the vendor did.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: it has no vendor of that name, or the vendor has no such request (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.ResourceNotFound"/>).</exception>
        /// <remarks>
        /// What a vendor request does, and what the model shows of it, is up to the plugin or the script: the model learns of its effects through the events, like of anything else that happens in OBS Studio.
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task<CallVendorResponse> CallVendorRequestAsync(string vendor, string request, object? data = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(vendor);
            ArgumentException.ThrowIfNullOrEmpty(request);
            JsonElement? requestData = data is null ? null : ModelJson.AsObject(data, nameof(data), "the data of a vendor request", "new { name = \"value\" }");
            return this._writer.ExecuteAsync<CallVendorResponse>(
                new WriteJob
                {
                    Description = $"calling the request '{request}' of the vendor '{vendor}'",
                    BuildRequests = () => [new WriteRequest(RequestType.CallVendorRequest, CallVendorRequestRequestData.Create(vendor, request, requestData))],
                    ReadResponses = static responses => (CallVendorResponse)responses[0].ResponseData!,
                    Map = static (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound ? new ObsResponseException(response.RequestStatus) : null,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the monitors that it sees.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The monitors, with the index that <see cref="OpenVideoMixProjectorAsync"/> and <see cref="ObsSource.OpenProjectorAsync"/> take.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        public Task<IReadOnlyList<ObsMonitor>> GetMonitorsAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<IReadOnlyList<ObsMonitor>>(
                new WriteJob
                {
                    Description = "asking for the monitors of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetMonitorList, null)],
                    ReadResponses = static responses => ((MonitorListResponse)responses[0].ResponseData!).Monitors,
                },
                cancellationToken);
        }

        /// <summary>
        /// Opens a projector of the preview, the program or the multiview of OBS Studio, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="mix">What the projector shows.</param>
        /// <param name="monitorIndex">The index of the monitor that the projector fills (see <see cref="GetMonitorsAsync"/>), or null (or -1) for a window.</param>
        /// <param name="geometry">The position and size of the window, as Qt writes them (base64), for a projector in a window; it can not be given together with a monitor, or null.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it opened the projector.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request (a monitor and a geometry were both given, or the monitor does not exist).</exception>
        /// <remarks>It changes nothing in the model, so the call does not wait for it. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.</remarks>
        public Task OpenVideoMixProjectorAsync(MixType mix, int? monitorIndex = null, string? geometry = null, CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"opening a projector of the {MixName(mix)}",
                    BuildRequests = () => [new WriteRequest(RequestType.OpenVideoMixProjector, OpenVideoMixProjectorRequestData.Create(mix, monitorIndex, geometry))],
                },
                cancellationToken);
        }

        private static string MixName(MixType mix)
        {
            return mix switch
            {
                MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW => "preview",
                MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM => "program",
                MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW => "multiview",
                _ => mix.ToString(),
            };
        }
    }
}
