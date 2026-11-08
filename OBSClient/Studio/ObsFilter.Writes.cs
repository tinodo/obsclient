namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Studio.Engine;

    public sealed partial class ObsFilter
    {
        /// <summary>
        /// Enables or disables the filter, and returns when the model shows it.
        /// </summary>
        /// <param name="value">True to enable the filter, false to disable it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsEnabled"/> is <paramref name="value"/>, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsRemovedException">The filter, or its source, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio, or the connection that the change was asked for on has ended and another was made. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The changes that you ask for are made one at a time, in the order in which you asked for them, and each returns when the model shows it: see
        /// <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait. The change is sent with the name that the filter has when its turn has come, so a rename that you asked for
        /// before it does not send it to another filter.
        /// </remarks>
        public Task SetEnabledAsync(bool value, CancellationToken cancellationToken = default)
        {
            return this.Source.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{(value ? "enabling" : "disabling")} {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.SetSourceFilterEnabled, new { sourceUuid = this.Source.Uuid, filterName = this.NameForRequests, filterEnabled = value })],
                    IsConfirmed = _ => this.IsRemoved || this.IsEnabled == value,
                    Refresh = [new RefreshTarget(ReadKind.Filters, this.Source.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Enables the filter when it is disabled, and disables it when it is enabled, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes with the value that <see cref="IsEnabled"/> was set to.</returns>
        /// <exception cref="ObsRemovedException">The filter, or its source, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call, or did not say in time whether the filter is enabled. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// OBS Studio has no request that toggles a filter. When its turn has come, after every change that you asked for before it has been answered, the change asks OBS Studio whether
        /// the filter is enabled, and sets the opposite: two calls make two toggles, in the order in which you asked for them. See <see cref="SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task<bool> ToggleEnabledAsync(CancellationToken cancellationToken = default)
        {
            // What OBS Studio holds when the turn of this change has come.
            var enabled = false;
            return this.Source.Studio.Writer.ExecuteAsync<bool>(
                new WriteJob
                {
                    Description = $"toggling {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    Prepare = async ask =>
                    {
                        var answer = await ask(new WriteRequest(RequestType.GetSourceFilter, new { sourceUuid = this.Source.Uuid, filterName = this.NameForRequests })).ConfigureAwait(false);
                        enabled = ((SourceFilterResponse)answer.ResponseData!).FilterEnabled;
                    },
                    BuildRequests = () => [new WriteRequest(RequestType.SetSourceFilterEnabled, new { sourceUuid = this.Source.Uuid, filterName = this.NameForRequests, filterEnabled = !enabled })],
                    ReadResponses = _ => !enabled,
                    IsConfirmed = result => this.IsRemoved || this.IsEnabled == (bool)result!,
                    Refresh = [new RefreshTarget(ReadKind.Filters, this.Source.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Gives the filter another name, and returns when the model shows it.
        /// </summary>
        /// <param name="name">The new name: it can not be the name of another filter of the same source. The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Name"/> is <paramref name="name"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNameInUseException">The source has a filter with that name already, also when it is this one. Nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The filter, or its source, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task RenameAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"renaming {this.Subject()} to '{name}'";
            return this.Source.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.SetSourceFilterName, new { sourceUuid = this.Source.Uuid, filterName = this.NameForRequests, newFilterName = name })],

                    // The filter has the new name from the answer on, and what is asked for after this change (its turn comes before the model shows the name) has to say so.
                    ReadResponses = _ =>
                    {
                        this.SetPendingName(name);
                        return null;
                    },
                    Finish = _ =>
                    {
                        this.ClearPendingName(name);
                        return null;
                    },
                    IsConfirmed = _ => this.IsRemoved || string.Equals(this.Name, name, StringComparison.Ordinal),
                    Refresh = [new RefreshTarget(ReadKind.Filters, this.Source.Uuid)],
                    Map = (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceAlreadyExists ? NameInUse(this.Source, description, name) : null,
                },
                cancellationToken);
        }

        /// <summary>
        /// Moves the filter to a position among the filters of its source, and returns when the model shows it.
        /// </summary>
        /// <param name="index">The new position: 0 is the first, which is applied first, and the number of filters minus one is the last.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Index"/> is <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative (thrown at once), or is not a position among the filters of the source (the call ends with it, after the filter is known to be there).</exception>
        /// <exception cref="ObsRemovedException">The filter, or its source, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The filters between the old and the new position move by one place, and OBS Studio says so in one event that lists them all: the model takes the positions from it. See
        /// <see cref="SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task SetIndexAsync(int index, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            return this.Source.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"moving {this.Subject()} to position {index}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = () =>
                    {
                        this.CheckWritable();

                        // Whether it is a position is asked after the filter is known to be there: what the model shows of a filter that was removed is a source with fewer filters.
                        var count = this.Source.Filters.Count;
                        if (index >= Math.Max(count, 1))
                        {
                            throw new ArgumentOutOfRangeException(nameof(index), index, $"The {this.Source.Label} has {count} filters: a position is from 0 to {Math.Max(count, 1) - 1}.");
                        }
                    },
                    BuildRequests = () => [new WriteRequest(RequestType.SetSourceFilterIndex, new { sourceUuid = this.Source.Uuid, filterName = this.NameForRequests, filterIndex = index })],
                    IsConfirmed = _ => this.IsRemoved || this.Index == index,
                    Refresh = [new RefreshTarget(ReadKind.Filters, this.Source.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Removes the filter from its source, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsRemoved"/> is true.</returns>
        /// <exception cref="ObsRemovedException">The filter, or its source, was removed already.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            return this.Source.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"removing {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.RemoveSourceFilter, new { sourceUuid = this.Source.Uuid, filterName = this.NameForRequests })],
                    IsConfirmed = _ => this.IsRemoved,
                    Refresh = [new RefreshTarget(ReadKind.Filters, this.Source.Uuid)],
                },
                cancellationToken);
        }

        // The exception for a name that OBS Studio refused because the source has a filter with it.
        internal static ObsNameInUseException NameInUse(ObsSource source, string operation, string name)
        {
            return new ObsNameInUseException(name, $"OBS Studio refused {operation}: the {source.Label} has a filter named '{name}' already. Nothing was changed.", null);
        }

        private string Subject()
        {
            return $"filter '{this.Name}' of {this.Source.Label}";
        }

        // A filter can be changed while it is on its source, and the source exists: OBS Studio answers for a source that it removed and keeps alive.
        private void CheckWritable()
        {
            if (this.IsRemoved)
            {
                throw new ObsRemovedException(this.Subject(), ObsObjectState.Gone);
            }

            if (this.Source.State == ObsObjectState.Gone)
            {
                throw new ObsRemovedException(this.Source.Label, this.Source.State);
            }
        }
    }
}
