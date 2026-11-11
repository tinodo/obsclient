namespace OBSStudioClient.Model
{
    using System.ComponentModel;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// The profiles of OBS Studio: which one is current, the names of all, and the calls that switch to one, make one and remove one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same object for as long as the <see cref="ObsModel"/> lives (<see cref="ObsModel.Profiles"/>), and says with <see cref="INotifyPropertyChanged"/> when <see cref="Current"/> and
    /// <see cref="Names"/> change, also when the user (or another program) changes them in OBS Studio. A profile holds what OBS Studio keeps apart from the scenes: its video settings, its stream
    /// service, the folder for recordings, and its other settings. Only the current profile can be read and changed, so switch to a profile before asking for or changing what it holds. What is in the
    /// scenes belongs to the scene collection (<see cref="ObsModel.SceneCollections"/>), so a switch of the profile does not change anything else that the model shows.
    /// </para>
    /// <para>
    /// What OBS Studio does (measured with OBS Studio 33.0.0, and read in its source): a switch to another profile is done when OBS Studio answers, and the events that say so come at the same
    /// moment. A profile that is made is a clean one (the settings that OBS Studio gives a new profile), and becomes the current one a moment after OBS Studio has answered (about a tenth of a
    /// second). Nothing happens when the profile that is asked for is the current one already. OBS Studio does not remove the current profile: it asks the user in its own window, and answers
    /// at once as if it had, so <see cref="RemoveAsync"/> refuses it before it sends anything. A switch to a profile with another sample rate or speaker setup makes OBS Studio ask the user, in its
    /// window, whether it may restart; the profile is the current one all the same.
    /// </para>
    /// </remarks>
    public sealed class ObsProfiles : INotifyPropertyChanged
    {
        private readonly ObsModel _model;
        private NameList? _list;

        internal ObsProfiles(ObsModel model)
        {
            this._model = model;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the name of the current profile. It is null until OBS Studio has said.
        /// </summary>
        public string? Current => Volatile.Read(ref this._list)?.Current;

        /// <summary>
        /// Gets the names of the profiles, in the order in which OBS Studio lists them. The list is empty until OBS Studio has said, and it is another list when the profiles change.
        /// </summary>
        public IReadOnlyList<string> Names
        {
            get
            {
                IReadOnlyList<string>? names = Volatile.Read(ref this._list)?.Names;
                return names ?? Array.Empty<string>();
            }
        }

        /// <summary>
        /// Makes a profile the current one, and returns when the model shows it.
        /// </summary>
        /// <param name="name">The name of the profile (<see cref="Names"/> lists them). The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Current"/> is the profile.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNotFoundException">OBS Studio has no profile of that name (the message suggests names that are close). Nothing was changed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// OBS Studio does nothing (and sends no event) when the profile is the current one already, so the call returns at once then. The video settings, the stream service and the folder for recordings are
        /// those of the profile that is current now: ask for them again. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task SetCurrentAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"making '{name}' the current profile",
                    Subject = $"profile '{name}'",
                    BuildRequests = () => [new WriteRequest(RequestType.SetCurrentProfile, new { profileName = name })],
                    IsConfirmed = _ => this.IsCurrent(name),
                    Refresh = [new RefreshTarget(ReadKind.Profiles)],
                    Map = (_, response) => this.Explain(response, name, $"making '{name}' the current profile"),
                },
                cancellationToken);
        }

        /// <summary>
        /// Makes a profile, which becomes the current one, and returns when the model shows it.
        /// </summary>
        /// <param name="name">The name of the new profile: it can not be the name of another profile. The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Names"/> lists the profile and <see cref="Current"/> is another profile than before the call: OBS Studio switched to the new one.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNameInUseException">There is a profile with that name. Nothing was changed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the profile.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio said that it made the profile, and the model does not list it: OBS Studio did not make it (it does not, and does not say so, when it can not make a folder name of the name), or it was removed again. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// OBS Studio answers before it has made the profile and switched to it; it lists the profile a moment later (about 40 milliseconds were measured) and says that it is the current one about
        /// 90 milliseconds after that, and the call returns then. <see cref="Current"/> is the new profile when it returns, unless the profile was switched away from again meanwhile (profiles that are made
        /// right after one another are made in that order, and the last one is current). The profile is a clean one: it has the settings that OBS Studio gives a new profile, not those of the profile that
        /// was current. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"creating profile '{name}'";
            string? before = null;
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    BuildRequests = () =>
                    {
                        before = this.Current;
                        return [new WriteRequest(RequestType.CreateProfile, new { profileName = name })];
                    },
                    IsConfirmed = _ => this.Lists(name) && !this.IsCurrent(before),
                    Finish = _ => this.Lists(name)
                        ? null
                        : throw new ObsNotConfirmedException(description, $"the profile '{name}' is not in the model: OBS Studio did not make it, or it was removed again"),
                    Refresh = [new RefreshTarget(ReadKind.Profiles)],
                    Map = (_, response) => this.Explain(response, name, description),
                },
                cancellationToken);
        }

        /// <summary>
        /// Removes a profile, and returns when the model no longer lists it.
        /// </summary>
        /// <param name="name">The name of the profile. It can not be the current profile: make another profile the current one first (<see cref="SetCurrentAsync"/>).</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Names"/> no longer lists the profile.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The profile is the current one (also when it is the only one). Nothing was sent.</exception>
        /// <exception cref="ObsNotFoundException">OBS Studio has no profile of that name (the message suggests names that are close). Nothing was changed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it removed the profile.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio said that it removed the profile, and the model still lists it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio deletes the profile, with its settings and files; that can not be undone. For the current profile it does not: it opens a question in its own window ("are you sure?") and answers
        /// at once that it removed it (measured with OBS Studio 33.0.0, and read in its source); the profile stays until somebody answers the question. The call refuses it, before anything is sent, so
        /// that no question is left on the screen of the user.
        /// </para>
        /// <para>
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task RemoveAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"removing profile '{name}'";
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"profile '{name}'",
                    Check = () =>
                    {
                        if (this.IsCurrent(name))
                        {
                            throw new InvalidOperationException($"'{name}' is the current profile, and OBS Studio does not remove the current profile: it asks the user in its own window, and answers that it removed it. Make another profile the current one first. Nothing was sent.");
                        }
                    },
                    BuildRequests = () => [new WriteRequest(RequestType.RemoveProfile, new { profileName = name })],
                    IsConfirmed = _ => this.IsGone(name),
                    Finish = _ => this.IsGone(name)
                        ? null
                        : throw new ObsNotConfirmedException(description, $"the model still lists the profile '{name}': OBS Studio did not remove it"),
                    Refresh = [new RefreshTarget(ReadKind.Profiles)],
                    Map = (_, response) => this.Explain(response, name, description),
                },
                cancellationToken);
        }

        internal bool Set(NameList? list, out NameList? before)
        {
            before = Volatile.Read(ref this._list);
            if (before is null ? list is null : list is not null && before.Equals(list))
            {
                return false;
            }

            Volatile.Write(ref this._list, list);
            return true;
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this._model.Raise(this.PropertyChanged, this, propertyName);
        }

        private bool IsCurrent(string? name)
        {
            return string.Equals(this.Current, name, StringComparison.Ordinal);
        }

        private bool Lists(string name)
        {
            return Volatile.Read(ref this._list) is { } list && list.Contains(name);
        }

        private bool IsGone(string name)
        {
            return Volatile.Read(ref this._list) is { } list && !list.Contains(name);
        }

        // What OBS Studio's refusal of a change means for a profile: it has no profile of that name (600), or it has one already (601).
        private Exception? Explain(Messages.RequestResponseMessage response, string name, string description)
        {
            return response.RequestStatus.Code switch
            {
                RequestStatusCode.ResourceNotFound => new ObsNotFoundException("profile", name, NameLookup.Suggest(this.Names, name)),
                RequestStatusCode.ResourceAlreadyExists => new ObsNameInUseException(name, $"OBS Studio refused {description}: a profile named '{name}' exists already. Nothing was changed.", null),
                _ => null,
            };
        }
    }
}
