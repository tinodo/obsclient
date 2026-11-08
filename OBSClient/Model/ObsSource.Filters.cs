namespace OBSStudioClient.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;

    public abstract partial class ObsSource
    {
        /// <summary>
        /// Adds a filter to the scene or the input (on top of its filters), and returns it when the model shows it.
        /// </summary>
        /// <param name="name">The name of the new filter: it can not be the name of another filter of this source.</param>
        /// <param name="kind">The kind of filter, as OBS Studio calls it: for example <c>color_filter_v2</c> (the plain client lists them: <c>obs.Client.GetSourceFilterKindList()</c>).</param>
        /// <param name="settings">The settings that the filter starts with, for example <c>new { gamma = 0.5 }</c>, or null for the defaults of the kind.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>The filter, which is in <see cref="Filters"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="kind"/> is empty, or <paramref name="settings"/> is not an object.</exception>
        /// <exception cref="ObsNameInUseException">The source has a filter with that name already. Nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The source was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show the filter. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change (it does not know the kind, for example).</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task<ObsFilter> CreateFilterAsync(string name, string kind, object? settings = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentException.ThrowIfNullOrEmpty(kind);
            var data = new Dictionary<string, object?>
            {
                ["sourceUuid"] = this.Uuid,
                ["filterName"] = name,
                ["filterKind"] = kind,
            };
            if (settings is not null)
            {
                data["filterSettings"] = FilterSettingsAsObject(settings);
            }

            var description = $"creating filter '{name}' on {this.Label}";
            return this.Model.Writer.ExecuteAsync<ObsFilter>(
                new WriteJob
                {
                    Description = description,
                    Subject = this.Label,
                    SubjectGone = () => this.State == ObsObjectState.Gone,
                    Check = this.CheckCanHoldFilters,
                    BuildRequests = () => [new WriteRequest(RequestType.CreateSourceFilter, data)],
                    IsConfirmed = _ => this.State == ObsObjectState.Gone || this.Filters.FindExact(name) is not null,
                    Finish = _ => this.Filters.FindExact(name) ?? throw (this.State == ObsObjectState.Gone
                        ? new ObsRemovedException(this.Label, this.State)
                        : new ObsNotConfirmedException(description, "the filter is not in the model: it was removed again, OBS Studio changed its scene collection, or the model was not told of it in time")),
                    Refresh = [new RefreshTarget(ReadKind.Filters, this.Uuid)],
                    Map = (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceAlreadyExists ? ObsFilter.NameInUse(this, description, name) : null,
                },
                cancellationToken);
        }

        private void CheckCanHoldFilters()
        {
            if (this.State == ObsObjectState.Gone)
            {
                throw new ObsRemovedException(this.Label, this.State);
            }
        }

        private static JsonElement FilterSettingsAsObject(object settings)
        {
            // The parameter is an object so that new { gamma = 0.5 } can be passed: a token that was meant for cancellationToken would be taken for settings, and this is what the call says then.
            if (settings is CancellationToken)
            {
                throw new ArgumentException("A CancellationToken was passed as the settings of the filter. Pass it by name (cancellationToken: token) when the settings are left out.", nameof(settings));
            }

            JsonElement element;
            try
            {
                element = JsonSerializer.SerializeToElement(settings, settings.GetType());
            }
            catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
            {
                throw new ArgumentException($"The settings of a filter can not be written as JSON: {ex.Message}", nameof(settings), ex);
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The settings of a filter are an object: the fields of its kind and their values, for example new { gamma = 0.5 }.", nameof(settings));
            }

            return element;
        }
    }
}
