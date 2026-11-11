namespace OBSStudioClient.Model
{
    using System.ComponentModel;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// The scene collections of OBS Studio: which one is loaded, the names of all, and the calls that load one and make one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same object for as long as the <see cref="ObsModel"/> lives (<see cref="ObsModel.SceneCollections"/>), and says with <see cref="INotifyPropertyChanged"/> when <see cref="Current"/> and
    /// <see cref="Names"/> change, also when the user (or another program) changes them in OBS Studio. A scene collection holds the scenes, the inputs and the scene transitions: what
    /// <see cref="ObsModel.Scenes"/> and <see cref="ObsModel.Inputs"/> show is the collection that is loaded. When OBS Studio loads another one the model reads everything again
    /// (<see cref="ObsModel.State"/> is <see cref="ObsModelState.Synchronizing"/> meanwhile), and the scenes, inputs and scene items that the application holds are those of the collection that is gone:
    /// they say so (<see cref="ObsSource.State"/>). OBS Studio has no request that removes or renames a scene collection.
    /// </para>
    /// <para>
    /// What OBS Studio does (measured with OBS Studio 33.0.0): it answers a request to load a collection when the collection is loaded, and sends the events that say so at about the same moment, which can be
    /// a little after the answer. A collection that is made is loaded at once, and has one scene, named "Scene". Nothing happens when the collection that is asked for is the loaded one already.
    /// </para>
    /// </remarks>
    public sealed class ObsSceneCollections : INotifyPropertyChanged
    {
        private readonly ObsModel _model;
        private NameList? _list;

        internal ObsSceneCollections(ObsModel model)
        {
            this._model = model;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the name of the scene collection that is loaded. It is null until OBS Studio has said.
        /// </summary>
        public string? Current => Volatile.Read(ref this._list)?.Current;

        /// <summary>
        /// Gets the names of the scene collections, in the order in which OBS Studio lists them. The list is empty until OBS Studio has said, and it is another list when the collections change.
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
        /// Makes OBS Studio load a scene collection, and returns when the model shows it completely.
        /// </summary>
        /// <param name="name">The name of the scene collection (<see cref="Names"/> lists them). The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Current"/> is the collection and the model has read it (<see cref="ObsModel.State"/> is <see cref="ObsModelState.Synchronized"/>).</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNotFoundException">OBS Studio has no scene collection of that name (the message suggests names that are close). Nothing was changed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it loaded the collection.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio answers when it has loaded the collection, which takes longer for a large one, so the call waits for the answer as long as <see cref="ObsModelOptions.SynchronizationTimeout"/> when
        /// that is more than <see cref="ObsModelOptions.WriteTimeout"/>; and then for the model to read the collection, which takes as long as the first synchronization. What the application holds of the
        /// collection that was loaded before (scenes, inputs, scene items) is gone then: ask the model for the new ones. OBS Studio does nothing (and sends no event) when the collection is loaded already,
        /// so the call returns at once then.
        /// </para>
        /// <para>
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes. Changes that were asked for before this one, and that are not confirmed when OBS Studio changes its collection, end as
        /// they do when somebody else changes it.
        /// </para>
        /// </remarks>
        public Task SetCurrentAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"loading the scene collection '{name}'";
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"scene collection '{name}'",
                    BuildRequests = () => [new WriteRequest(RequestType.SetCurrentSceneCollection, new { sceneCollectionName = name })],
                    ExpectsSwitch = true,
                    IsConfirmed = _ => this.IsLoadedAndRead(name),
                    Finish = _ => this.IsLoadedAndRead(name)
                        ? null
                        : throw new ObsNotConfirmedException(description, $"the model shows the scene collection '{this.Current}' (or has not read '{name}' completely): OBS Studio did not load it, or loaded another one since"),
                    Refresh = [new RefreshTarget(ReadKind.SceneCollections)],
                    Map = (_, response) => this.Explain(response, name, description),
                },
                cancellationToken);
        }

        /// <summary>
        /// Makes a scene collection, which OBS Studio loads at once, and returns when the model shows it completely.
        /// </summary>
        /// <param name="name">The name of the new scene collection: it can not be the name of another scene collection. The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Names"/> lists the collection, <see cref="Current"/> is another collection than before the call (OBS Studio loaded the new one) and the model has read it.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNameInUseException">There is a scene collection with that name. Nothing was changed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the collection.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio said that it made the collection, and the model does not list it: OBS Studio did not make it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The collection has one scene, named "Scene", and nothing else (measured with OBS Studio 33.0.0). What the application holds of the collection that was loaded before is gone, as it is for
        /// <see cref="SetCurrentAsync"/>, which also says how long the call waits.
        /// </remarks>
        public Task CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"creating the scene collection '{name}'";
            string? before = null;
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    BuildRequests = () =>
                    {
                        before = this.Current;
                        return [new WriteRequest(RequestType.CreateSceneCollection, new { sceneCollectionName = name })];
                    },
                    ExpectsSwitch = true,
                    IsConfirmed = _ => this.Lists(name) && !string.Equals(this.Current, before, StringComparison.Ordinal) && this._model.State == ObsModelState.Synchronized,
                    Finish = _ => this.Lists(name)
                        ? null
                        : throw new ObsNotConfirmedException(description, $"the scene collection '{name}' is not in the model: OBS Studio did not make it"),
                    Refresh = [new RefreshTarget(ReadKind.SceneCollections)],
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

        // The collection is the loaded one, and the model has read it: a change of the collection is not done before.
        private bool IsLoadedAndRead(string name)
        {
            return string.Equals(this.Current, name, StringComparison.Ordinal) && this._model.State == ObsModelState.Synchronized;
        }

        private bool Lists(string name)
        {
            return Volatile.Read(ref this._list) is { } list && list.Contains(name);
        }

        // What OBS Studio's refusal of a change means for a scene collection: it has no collection of that name (600), or it has one already (601).
        private Exception? Explain(Messages.RequestResponseMessage response, string name, string description)
        {
            return response.RequestStatus.Code switch
            {
                RequestStatusCode.ResourceNotFound => new ObsNotFoundException("scene collection", name, NameLookup.Suggest(this.Names, name)),
                RequestStatusCode.ResourceAlreadyExists => new ObsNameInUseException(name, $"OBS Studio refused {description}: a scene collection named '{name}' exists already. Nothing was changed.", null),
                _ => null,
            };
        }
    }
}
