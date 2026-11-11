namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    // The settings of an input, and the properties that OBS Studio shows for it in its properties window.
    public sealed partial class ObsInput
    {
        /// <summary>
        /// Changes the settings of the input, and returns when the model shows it.
        /// </summary>
        /// <param name="settings">The settings to change: an object (for example <c>new { text = "Hello" }</c>), a dictionary, or a <see cref="System.Text.Json.JsonElement"/>. What the fields are called and what they mean depends on the kind of the input (see <see cref="Settings"/>).</param>
        /// <param name="overlay">True to change the settings that are given and leave the others as they are, which is what OBS Studio does with the settings that you change in its properties window; false to take all the settings of the input back to their defaults first, so that the input has the given settings and no others.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Settings"/> shows the settings that were given.</returns>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is not an object, or can not be written as JSON.</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio does not check the settings: it keeps every field that it is given, also one that the kind does not know (and that does nothing), and a value of the wrong kind (a text for a
        /// number) makes the input do what it does with a value that it can not use. Look at <see cref="Settings"/> of an input that is set up as you want, and send what you see there.
        /// </para>
        /// <para>
        /// A field that is an object is replaced as a whole, not merged (the other fields of the object that was there are gone). A null field is ignored: OBS Studio does not keep a null, so the field
        /// keeps what it was. Both were checked with OBS Studio 33.0.0. The input is updated by OBS Studio when it has
        /// the settings: a change that the kind of the input can not take (a file that is not there) is not an error of this call. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task SetSettingsAsync(object settings, bool overlay = true, CancellationToken cancellationToken = default)
        {
            var given = ModelJson.AsObject(settings, nameof(settings), "the settings of an input", "new { text = \"Hello\" }");

            // What OBS Studio has left of the settings when it took its own back to nothing first: a null is not kept.
            var kept = overlay ? given : SettingsValue.WithoutNulls(given);
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"changing the settings of input '{this.Name}'",
                    Subject = $"input '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckLive,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputSettings, new { inputUuid = this.Uuid, inputSettings = given, overlay })],

                    // Everything that was given is there. A reset leaves what was given and nothing else.
                    IsConfirmed = _ => this.IsRemoved || (overlay ? SettingsValue.Contains(this.Settings, given) : SettingsValue.DeepEquals(this.Settings, kept)),
                    Refresh = [new RefreshTarget(ReadKind.InputSettings, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the choices of a list property of the input: what the dropdown with that name in the properties window of the input holds.
        /// </summary>
        /// <param name="propertyName">The name of the property: the name of the setting that the dropdown sets (<c>device_id</c> of a video capture device, <c>monitor_id</c> of a display capture).</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The choices, in the order of the dropdown. It is empty when the list is.</returns>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the input has no property with that name (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.ResourceNotFound"/>), or it is not a list (<see cref="RequestStatusCode.InvalidResourceType"/>).</exception>
        /// <remarks>
        /// The list is what OBS Studio makes when it is asked: the displays and the devices that the computer has at that moment. Use it when a kind offers a choice that the settings can not
        /// tell: the value of a choice (<see cref="ObsPropertyItem.Value"/>) is what to set the setting to (see <see cref="SetSettingsAsync"/>). The request is made in its turn, after the changes that were asked for before it.
        /// </remarks>
        public Task<IReadOnlyList<ObsPropertyItem>> GetPropertyItemsAsync(string propertyName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(propertyName);
            return this.Model.Writer.ExecuteAsync<IReadOnlyList<ObsPropertyItem>>(
                new WriteJob
                {
                    Description = $"asking for the choices of the property '{propertyName}' of {this.Label}",
                    Subject = this.Label,
                    ReadOnly = true,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.GetInputPropertiesListPropertyItems, new { inputUuid = this.Uuid, propertyName })],
                    ReadResponses = static responses => ((PropertyItemsResponse)responses[0].ResponseData!).PropertyItems.Select(item => new ObsPropertyItem(item.ItemName, item.ItemValue, item.ItemEnabled)).ToArray(),
                    Map = MapPropertyRefusal,
                },
                cancellationToken);
        }

        /// <summary>
        /// Presses a button of the properties of the input, as a person does in the properties window, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="propertyName">The name of the button: <c>refreshnocache</c> is the button that reloads a browser source (the name of the button is not the text on it).</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered. What the button does may take longer.</returns>
        /// <exception cref="ArgumentException"><paramref name="propertyName"/> is empty.</exception>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it pressed the button.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the input has no property with that name (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.ResourceNotFound"/>), it is not a button (<see cref="RequestStatusCode.InvalidResourceType"/>), or the button is not enabled (<see cref="RequestStatusCode.InvalidResourceState"/>).</exception>
        /// <remarks>
        /// What a button does depends on the input: the model shows what it changes through the events, like any other change, and the call does not wait for that. See
        /// <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task PressPropertyButtonAsync(string propertyName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(propertyName);
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"pressing the button '{propertyName}' of {this.Label}",
                    Subject = this.Label,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.PressInputPropertiesButton, new { inputUuid = this.Uuid, propertyName })],
                    Map = MapPropertyRefusal,
                },
                cancellationToken);
        }

        // "Not found" about the property is a refusal of the request, and not a word about the input: the input that is gone is "No input was found".
        private static Exception? MapPropertyRefusal(int index, RequestResponseMessage response)
        {
            return response.RequestStatus.Code == RequestStatusCode.ResourceNotFound && response.RequestStatus.Comment?.Contains("property", StringComparison.OrdinalIgnoreCase) == true
                ? new ObsResponseException(response.RequestStatus)
                : null;
        }
    }
}
