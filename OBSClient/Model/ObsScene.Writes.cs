namespace OBSStudioClient.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Model.Engine;

    public sealed partial class ObsScene
    {
        /// <summary>
        /// Makes the scene the program scene (the one that is on the air), and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsProgram"/> is true, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="InvalidOperationException">The scene is a group (<see cref="IsGroup"/>): a group is not a scene that can be on the air.</exception>
        /// <exception cref="ObsRemovedException">The scene was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio shows the scene through the transition that is set, and announces the new program scene when the transition has ended, which can take longer than
        /// <see cref="ObsModelOptions.ConfirmationTimeout"/>. It answers a question about the program scene with the new scene at once, so the model reads it, and the task
        /// completes while the transition is still running. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task SwitchToAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"switching to scene '{this.Name}'",
                    Subject = $"scene '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckCanBeProgram,
                    BuildRequests = () => [new WriteRequest(RequestType.SetCurrentProgramScene, new { sceneUuid = this.Uuid })],
                    IsConfirmed = _ => this.IsRemoved || this.IsProgram,
                    Refresh = [new RefreshTarget(ReadKind.Program)],
                },
                cancellationToken);
        }

        private void CheckCanBeProgram()
        {
            if (this.State != ObsObjectState.Live)
            {
                throw new ObsRemovedException(this.Label, this.State);
            }

            if (this.IsGroup)
            {
                throw new InvalidOperationException($"The group '{this.Name}' can not be the program scene.");
            }
        }

        /// <summary>
        /// Makes an input in the scene (and a scene item that shows it), and returns the input when the model shows both.
        /// </summary>
        /// <param name="name">The name of the new input: it can not be the name of another input or of a scene.</param>
        /// <param name="kind">The kind of the input, for example <c>color_source_v3</c>, <c>image_source</c> or <c>wasapi_input_capture</c>. <see cref="ObsClient.GetInputKindList"/> lists them.</param>
        /// <param name="settings">The settings of the new input: an object whose fields are the settings of its kind (for example <c>new { width = 640, height = 360 }</c>; the names are used as they are written, and OBS Studio's settings have lower case names), or null for the defaults of the kind.</param>
        /// <param name="enabled">Whether the scene item that shows the input is shown.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>The input, which is in <see cref="ObsModel.Inputs"/>; its scene item is in <see cref="Items"/> (<see cref="ObsSource.GetSceneItems"/> of the input finds it), unless somebody removed it meanwhile.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="kind"/> is empty, or <paramref name="settings"/> is not an object (a <see cref="CancellationToken"/> passed in its place is refused with a message that says so: pass it as <paramref name="cancellationToken"/>), or can not be written as JSON.</exception>
        /// <exception cref="InvalidOperationException">The scene is a group (<see cref="IsGroup"/>): obs-websocket makes inputs in scenes only, not in a group. Nothing was changed.</exception>
        /// <exception cref="ObsNameInUseException">The name is taken (by a scene, by an input, by a group, or by an input that was removed and that OBS Studio keeps alive). Nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The scene was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the input.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the input, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change (for example because it has no input of that kind).</exception>
        /// <remarks>
        /// The call returns when the model shows the input and its scene item, so that what you read next is what you made. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the
        /// order of changes and the wait. The input is a new object of the model: it is complete (with its audio state) when you get it.
        /// </remarks>
        public Task<ObsInput> CreateInputAsync(string name, string kind, object? settings = null, bool enabled = true, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentException.ThrowIfNullOrEmpty(kind);
            var data = new Dictionary<string, object?>
            {
                ["sceneUuid"] = this.Uuid,
                ["inputName"] = name,
                ["inputKind"] = kind,
                ["sceneItemEnabled"] = enabled,
            };
            if (settings is not null)
            {
                data["inputSettings"] = ModelJson.AsObject(settings, nameof(settings), "the settings of an input", "new { width = 640, height = 360 }");
            }

            var description = $"creating input '{name}' in scene '{this.Name}'";
            var model = this.Model;
            return model.Writer.ExecuteAsync<ObsInput>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"scene '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckCanHoldItems,
                    BuildRequests = () => [new WriteRequest(RequestType.CreateInput, data)],
                    ReadResponses = static responses =>
                    {
                        var created = (CreateInputResponse)responses[0].ResponseData!;
                        return new CreatedInput(created.InputUuid, created.SceneItemId);
                    },
                    IsConfirmed = result => this.IsRemoved || (model.Inputs.TryGet(((CreatedInput)result!).Uuid, out _) && this.FindItem(((CreatedInput)result).ItemId) is not null),
                    Finish = result =>
                    {
                        if (model.Inputs.TryGet(((CreatedInput)result!).Uuid, out var input))
                        {
                            return input;
                        }

                        throw this.State != ObsObjectState.Live
                            ? new ObsRemovedException($"scene '{this.Name}'", this.State)
                            : new ObsNotConfirmedException(description, "the input is not in the model: it was removed again, OBS Studio changed its scene collection, or the model was not told of it in time");
                    },
                    Refresh = [new RefreshTarget(ReadKind.Inputs), new RefreshTarget(ReadKind.Items, this.Uuid)],
                    RefreshFor = static result =>
                    {
                        // What the model reads of an input before it shows it: its audio, its settings and, for a media input, the state of the media (a read that is no-op for an input that is no media input).
                        var uuid = ((CreatedInput)result!).Uuid;
                        return [new RefreshTarget(ReadKind.AudioMute, uuid), new RefreshTarget(ReadKind.InputSettings, uuid), new RefreshTarget(ReadKind.MediaStatus, uuid)];
                    },
                    Map = (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceAlreadyExists
                        ? ModelNames.InUse(model, description, name, forScene: false, requester: null)
                        : null,
                    RetriesAFreedName = true,
                },
                cancellationToken);
        }

        /// <summary>
        /// Shows a scene or an input in the scene (makes a scene item for it, on top), and returns the scene item when the model shows it.
        /// </summary>
        /// <param name="source">The scene or the input to show. A scene that would contain itself, directly or through the scenes that it contains, is refused by OBS Studio.</param>
        /// <param name="enabled">Whether the scene item is shown.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>The scene item, which is in <see cref="Items"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="source"/> is this scene, or belongs to another <see cref="ObsModel"/>.</exception>
        /// <exception cref="InvalidOperationException">This scene is a group (<see cref="IsGroup"/>): obs-websocket makes scene items in scenes only, not in a group. Nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The scene or the source was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the scene item.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the scene item, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change (for example because the scene would contain itself).</exception>
        /// <remarks>
        /// The same source can be in a scene several times: each call makes another scene item. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task<ObsSceneItem> AddAsync(ObsSource source, bool enabled = true, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (!ReferenceEquals(source.Model, this.Model))
            {
                throw new ArgumentException("The source belongs to another ObsModel.", nameof(source));
            }

            if (ReferenceEquals(source, this))
            {
                throw new ArgumentException("A scene can not show itself.", nameof(source));
            }

            var description = $"adding {source.Label} to scene '{this.Name}'";
            return this.Model.Writer.ExecuteAsync<ObsSceneItem>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"scene '{this.Name}' or {source.Label}",
                    SubjectGone = () => this.IsRemoved || source.IsRemoved,
                    Check = () =>
                    {
                        this.CheckCanHoldItems();
                        if (source.State != ObsObjectState.Live)
                        {
                            throw new ObsRemovedException(source.Label, source.State);
                        }
                    },
                    BuildRequests = () => [new WriteRequest(RequestType.CreateSceneItem, new { sceneUuid = this.Uuid, sourceUuid = source.Uuid, sceneItemEnabled = enabled })],
                    ReadResponses = static responses => ((SceneItemIdResponse)responses[0].ResponseData!).SceneItemId,
                    IsConfirmed = result => this.IsRemoved || this.FindItem((int)result!) is not null,
                    Finish = result => this.FindItem((int)result!) ?? throw (this.State != ObsObjectState.Live
                        ? new ObsRemovedException($"scene '{this.Name}'", this.State)
                        : new ObsNotConfirmedException(description, "the scene item is not in the model: it was removed again, OBS Studio changed its scene collection, or the model was not told of it in time")),
                    Refresh = [new RefreshTarget(ReadKind.Items, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Gives the scene another name, and returns when the model shows it.
        /// </summary>
        /// <param name="name">The new name: it can not be the name of another scene. With OBS Studio that has obs-websocket 5.6 or older it can not be the name of an input either. The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="ObsSource.Name"/> is <paramref name="name"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The scene is a group (<see cref="IsGroup"/>): obs-websocket can not rename a group (<c>SetSceneName</c> refuses a group, and a group is not an input), so rename it in OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNameInUseException">The name is taken, also when it is the name that the scene has already. Nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The scene was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task RenameAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"renaming scene '{this.Name}' to '{name}'";
            var model = this.Model;
            return model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"scene '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckCanBeRenamed,
                    BuildRequests = () => [new WriteRequest(RequestType.SetSceneName, new { sceneUuid = this.Uuid, newSceneName = name })],
                    IsConfirmed = _ => this.IsRemoved || string.Equals(this.Name, name, StringComparison.Ordinal),
                    Refresh = [new RefreshTarget(ReadKind.Scenes)],
                    Map = (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceAlreadyExists
                        ? ModelNames.InUse(model, description, name, forScene: true, requester: this)
                        : null,
                    RetriesAFreedName = true,
                },
                cancellationToken);
        }

        /// <summary>
        /// Removes the scene, and the scene items that show it in other scenes, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when the scene is removed (<see cref="ObsSource.State"/> is not <see cref="ObsObjectState.Live"/>) and the scene items that were showing it are gone.</returns>
        /// <exception cref="InvalidOperationException">The scene is a group (<see cref="IsGroup"/>): remove the scene item that shows it. Or a scene item in a group shows the scene: obs-websocket removes the scene items of scenes only, not those of a group, so the scene can not be removed for good from here (take it out of the group in OBS Studio first); nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The scene does not exist any more.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change (it does not remove the last scene of a collection).</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio keeps a scene alive after it is removed for as long as scene items in other scenes show it (the scene is <see cref="ObsObjectState.Detached"/>: it is
        /// not in <see cref="ObsModel.Scenes"/> and it can not be changed; with OBS Studio 31.0 and older its name stays taken, and OBS Studio 31.1 and newer free the name at once). This call removes those scene items too, so that OBS Studio destroys the scene
        /// (<see cref="ObsObjectState.Gone"/>). The scene is removed first, and the scene items after it, one request after the other: when OBS Studio refuses one of the
        /// scene items (it was removed meanwhile: by the user, or by OBS Studio itself, which takes the scene items of a removed scene out of the scenes that it is rendering on the next
        /// frame), the call ends with the exception once the model shows what the requests before it made (the scene is Detached, and the scene items that were
        /// removed are gone), so that calling <see cref="RemoveAsync"/> again removes what is left.
        /// A scene that a scene item shows that the model did not know yet (OBS Studio announces it a few milliseconds late) stays Detached in the same way.
        /// </para>
        /// <para>
        /// The call returns when the model shows the scene removed and the scene items that showed it gone, and the scene can still be <see cref="ObsObjectState.Detached"/> at that moment: OBS Studio destroys it when the last
        /// of those scene items is gone, and the model learns of that from an event of OBS Studio. When the events are slower than the confirmation time of the options (they can be, on a busy machine) the model learns that the
        /// scene items are gone from a read instead, which is ahead of the events, so it reads OBS Studio once more to be sure that it destroyed the scene: the scene is <see cref="ObsObjectState.Gone"/> a moment after the call
        /// returned. Wait for that when what follows needs the scene to be destroyed (<see cref="ObsSource.PropertyChanged"/> is raised for <see cref="ObsSource.State"/>, and <see cref="ObsModel.Changed"/> for <see cref="ObsChangeKind.StateChanged"/>).
        /// </para>
        /// <para>
        /// What the scene shows is destroyed with it: the inputs that only this scene showed are destroyed by OBS Studio a moment after the scene, and the model learns of that from its
        /// events. If this call removes the scene (a scene that is Detached already has no such wait: the model learns from the events of OBS Studio which scene is the program scene now) and the scene is the program scene, OBS Studio makes another scene the program scene about one frame after it has answered (16 ms were measured, and a read of the program scene in between still says the
        /// scene that was removed): the call returns when the model shows that scene as the program scene, so that <see cref="ObsModel.ProgramScene"/> is the scene that OBS Studio has now. The model is read for
        /// that after the answer whatever the events said, also when a switch to this scene was asked for just before and the model did not show it yet. When the events do not show it within the confirmation time of
        /// the options (1 second by default) the model is read again, and the call returns when that read has been applied, whatever it shows (the model goes on following OBS Studio by its events); when no read is applied in time
        /// (the read timeout of the options, 10 seconds by default, counts twice, after the confirmation time) the call returns all the same, with a warning in the log.
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of
        /// changes and the wait.
        /// </para>
        /// <para>
        /// A scene that is <see cref="ObsObjectState.Detached"/> because a list of scenes did not have it, while no event of OBS Studio removed it, can still be a live scene in OBS Studio (OBS Studio makes
        /// that list from its user interface, which can be busy for seconds, right after it was started for one). The call asks OBS Studio to remove such a scene first, and then removes the scene items that show it.
        /// A scene that OBS Studio removed already and keeps alive for a scene item refuses that request (701 with OBS Studio 33.0.0), and that is taken for the answer: the scene items are removed all the same.
        /// A scene that OBS Studio does not have at all (600) ends the call with <see cref="ObsRemovedException"/>. A scene that the model only knows as the source of a scene item was never seen live, and is taken for removed.
        /// The two steps are two changes in the line of changes (see <see cref="ObsSceneItem.SetEnabledAsync"/>): a change that you ask for right after this call is made between them, not after both.
        /// </para>
        /// </remarks>
        public async Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            var asked = false;
            if (this.State == ObsObjectState.Detached && this.RemovalUnverified)
            {
                // The model took the scene for removed, and OBS Studio did not say so: it can still have it as a live scene (its list of scenes is made from its user interface, which can be busy for
                // seconds), and then removing the scene items that show it would leave it there while the call returns. It is asked to remove the scene first. A scene that it removed already and keeps alive
                // for a scene item refuses that with 701 (measured with OBS Studio 33.0.0): that is what the scene items are for.
                asked = true;
                try
                {
                    await this.RemoveTheSceneAloneAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (ObsResponseException exception) when (exception.ErrorCode == RequestStatusCode.ResourceActionFailed)
                {
                    // Removed already, and kept alive.
                }
            }

            await this.RemoveWithItsSceneItemsAsync(asked, cancellationToken).ConfigureAwait(false);
        }

        private Task RemoveTheSceneAloneAsync(CancellationToken cancellationToken)
        {
            var description = $"removing scene '{this.Name}'";
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"scene '{this.Name}'",
                    Check = this.CheckCanBeRemoved,
                    BuildRequests = () => ModelRemovals.Requests(new WriteRequest(RequestType.RemoveScene, new { sceneUuid = this.Uuid }), []),
                    IsConfirmed = _ => this.IsRemoved,
                    Refresh = [new RefreshTarget(ReadKind.Scenes)],
                    RefreshAtTurn = () => [new RefreshTarget(ReadKind.Scenes), new RefreshTarget(ReadKind.Program)],
                    ObserveAtTurn = () => [new RefreshTarget(ReadKind.Program)],
                    ObservedShown = () => !this.IsProgram,
                    Reconcile = (index, code) => ModelRemovals.Reconciled(this, true, [], index, code),
                },
                cancellationToken);
        }

        private Task RemoveWithItsSceneItemsAsync(bool asked, CancellationToken cancellationToken)
        {
            var description = $"removing scene '{this.Name}'";
            ObsSceneItem[] showing = [];
            var includesRemoval = false;
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"scene '{this.Name}'",

                    // When the first step asked OBS Studio to remove the scene, it did the checks, and what it did shows by now: the model can show the scene gone (OBS Studio destroyed it) before this step has its turn.
                    Check = asked ? static () => { } : this.CheckCanBeRemoved,
                    BuildRequests = () =>
                    {
                        // The scene was asked to be removed already when it was in doubt at the call. When it was Live at the call and is in doubt at its turn, it is asked here, and a refusal is the answer.
                        showing = this.GetSceneItems().Where(item => !item.IsRemoved).ToArray();
                        includesRemoval = !asked && (this.State == ObsObjectState.Live || (this.State == ObsObjectState.Detached && this.RemovalUnverified));
                        return ModelRemovals.Requests(includesRemoval ? new WriteRequest(RequestType.RemoveScene, new { sceneUuid = this.Uuid }) : null, showing);
                    },
                    ReadResponses = _ => showing,
                    IsConfirmed = result => this.IsRemoved && ((ObsSceneItem[])result!).All(item => item.IsRemoved),
                    Finish = static _ => null,
                    Refresh = [new RefreshTarget(ReadKind.Scenes)],
                    RefreshAtTurn = () =>
                    [
                        new RefreshTarget(ReadKind.Scenes),
                        new RefreshTarget(ReadKind.Program),
                        .. this.GetSceneItems().Select(item => new RefreshTarget(ReadKind.Items, item.Scene.Uuid)).Distinct(),
                    ],
                    ObserveAtTurn = () => includesRemoval ? [new RefreshTarget(ReadKind.Program)] : [],
                    ObservedShown = () => !this.IsProgram,
                    Map = (index, response) => ModelRemovals.Refusal(description, includesRemoval, showing, index, response),
                    Reconcile = (index, code) => ModelRemovals.Reconciled(this, includesRemoval, showing, index, code),
                },
                cancellationToken);
        }

        private ObsSceneItem? FindItem(int id)
        {
            foreach (var item in this.Items)
            {
                if (item.Id == id)
                {
                    return item;
                }
            }

            return null;
        }

        private void CheckLive()
        {
            if (this.State != ObsObjectState.Live)
            {
                throw new ObsRemovedException(this.Label, this.State);
            }
        }

        private void CheckCanBeRenamed()
        {
            this.CheckLive();
            if (this.IsGroup)
            {
                throw new InvalidOperationException($"The group '{this.Name}' can not be renamed from here: obs-websocket renames scenes (SetSceneName) and inputs (SetInputName), and both refuse a group. Rename it in OBS Studio. Nothing was changed.");
            }
        }

        // obs-websocket makes inputs and scene items in scenes only: CreateInput and CreateSceneItem refuse a group ("The specified source is not a scene. (Is group)").
        private void CheckCanHoldItems()
        {
            this.CheckLive();
            if (this.IsGroup)
            {
                throw new InvalidOperationException($"The group '{this.Name}' can not be given a new scene item: obs-websocket makes inputs and scene items in scenes only, not in a group. Nothing was changed.");
            }
        }

        // A scene that was removed and that OBS Studio keeps alive can be removed for good (its scene items), and one that is gone can not be removed. A group is removed by removing the
        // scene item that shows it. Nor can a scene be removed that a group shows.
        private void CheckCanBeRemoved()
        {
            if (this.State == ObsObjectState.Gone)
            {
                throw new ObsRemovedException(this.Label, this.State);
            }

            if (this.IsGroup)
            {
                throw new InvalidOperationException($"The group '{this.Name}' is removed by removing the scene item that shows it. Nothing was changed.");
            }

            ModelRemovals.ThrowIfAGroupShows(this);
        }

        private readonly record struct CreatedInput(Guid Uuid, int ItemId);
    }
}
