namespace OBSStudioClient.Studio.Engine
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    internal sealed partial class StudioEngine
    {
        /// <summary>
        /// Tells the engine what OBS answered to the requests of a read.
        /// </summary>
        /// <param name="ticket">The read.</param>
        /// <param name="responses">The responses, in the order of the requests (a serial batch).</param>
        /// <param name="now">The time.</param>
        public void OnReadCompleted(ReadTicket ticket, IReadOnlyList<RequestResponseMessage> responses, TimeSpan now)
        {
            this._now = now;
            if (!this.Owns(ticket))
            {
                return;
            }

            this._seq = ticket.SentSeq;
            for (var i = 0; i < ticket.Requests.Count; i++)
            {
                var request = ticket.Requests[i];
                if (request.Kind == ReadKind.AudioVolume)
                {
                    // Handled together with the GetInputMute before it.
                    continue;
                }

                var response = i < responses.Count ? responses[i] : null;
                var volumeResponse = request.Kind == ReadKind.AudioMute && i + 1 < responses.Count ? responses[i + 1] : null;
                this.ApplyResponse(ticket, request, response, volumeResponse);
            }

            this.ResolveRemovals();
            this.UpdateState();
        }

        /// <summary>
        /// Tells the engine that a read did not complete (the connection ended, or OBS did not answer in time): its parts are read again later.
        /// </summary>
        /// <param name="ticket">The read.</param>
        /// <param name="now">The time.</param>
        public void OnReadFailed(ReadTicket ticket, TimeSpan now)
        {
            this._now = now;
            if (!this.Owns(ticket))
            {
                return;
            }

            foreach (var request in ticket.Requests)
            {
                if (request.Kind != ReadKind.AudioVolume && this.ScopeOf(request) is { } scope)
                {
                    this.Retry(scope);
                }
            }
        }

        // A read belongs to the connection that it was sent on. What another connection's read says, or what a connection that ended would say,
        // means nothing to this one, and must not release the parts that this one is reading.
        private bool Owns(ReadTicket ticket)
        {
            return this._state != StudioEngineState.Disconnected && ticket.SessionId == this._sessionId;
        }

        private ScopeState? ScopeOf(ReadRequest request)
        {
            switch (request.Kind)
            {
                case ReadKind.Scenes:
                    return this._scenesScope;
                case ReadKind.Inputs:
                    return this._inputsScope;
                case ReadKind.Program:
                    return this._programScope;
                case ReadKind.StudioMode:
                    return this._studioScope;
                case ReadKind.Items:
                    return this._scenes.TryGetValue(request.Target, out var scene) ? scene.ItemsScope : null;
                default:
                    return this._inputs.TryGetValue(request.Target, out var input) ? input.AudioScope : null;
            }
        }

        // The part is read again, after a wait that gets longer every time.
        private void Retry(ScopeState scope)
        {
            scope.InFlight = false;
            scope.MarkDirtyNotBefore(this._now + this.Backoff(scope));
        }

        private void ApplyResponse(ReadTicket ticket, ReadRequest request, RequestResponseMessage? response, RequestResponseMessage? volumeResponse)
        {
            var scope = this.ScopeOf(request);
            var ok = response is { RequestStatus.Result: true };
            var notFound = response is { RequestStatus.Result: false } && response.RequestStatus.Code == RequestStatusCode.ResourceNotFound;
            if (scope is not null)
            {
                scope.InFlight = false;
            }

            switch (request.Kind)
            {
                case ReadKind.Scenes:
                    if (ok && response!.ResponseData is SceneListResponse scenes)
                    {
                        // The back-off starts again when nothing waits for the list any more (see ApplyScenes).
                        this.ApplyScenes(scenes, ticket);
                    }
                    else
                    {
                        this.Retry(this._scenesScope);
                    }

                    break;
                case ReadKind.Inputs:
                    if (ok && response!.ResponseData is InputsResponse inputs)
                    {
                        this._inputsScope.Attempts = 0;
                        this.ApplyInputs(inputs, ticket);
                    }
                    else
                    {
                        this.Retry(this._inputsScope);
                    }

                    break;
                case ReadKind.Program:
                    if (ok && response!.ResponseData is CurrentProgramSceneNameResponse program)
                    {
                        this._programScope.Attempts = 0;
                        this._programRead = true;
                        if (this._programUuid.ApplyRead(program.SceneUuid, ticket.SentSeq))
                        {
                            this.Emit(StudioChangeKind.ProgramSceneChanged, program.SceneUuid, 0, program.SceneName);
                        }

                        if (!this._scenes.ContainsKey(program.SceneUuid) && !this.Removed(program.SceneUuid))
                        {
                            this._scenesScope.MarkDirty();
                        }
                    }
                    else
                    {
                        this.Retry(this._programScope);
                    }

                    break;
                case ReadKind.StudioMode:
                    if (ok && response!.ResponseData is StudioModeEnabledResponse studio)
                    {
                        this._studioScope.Attempts = 0;
                        this._studioRead = true;
                        var old = this._studioMode.Value;
                        if (this._studioMode.ApplyRead(studio.StudioModeEnabled, ticket.SentSeq))
                        {
                            this.Emit(StudioChangeKind.StudioModeChanged, Guid.Empty, 0, null, old, studio.StudioModeEnabled);
                        }
                    }
                    else
                    {
                        this.Retry(this._studioScope);
                    }

                    break;
                case ReadKind.Items:
                    this.ApplyItemsResponse(ticket, request, response, ok, notFound);
                    break;
                case ReadKind.AudioMute:
                    this.ApplyAudioResponse(ticket, request, response, volumeResponse, notFound);
                    break;
            }
        }

        private void ApplyItemsResponse(ReadTicket ticket, ReadRequest request, RequestResponseMessage? response, bool ok, bool notFound)
        {
            if (!this._scenes.TryGetValue(request.Target, out var scene))
            {
                // The scene is gone: nothing to apply.
                return;
            }

            if (ok && response!.ResponseData is SceneItemsResponse items)
            {
                scene.ItemsScope.Attempts = 0;
                this.ApplyItems(scene, items.SceneItems, ticket);
                if (scene.RemovalUnresolved && scene.CheckAfter > 0)
                {
                    if (ticket.Id > scene.CheckAfter)
                    {
                        // OBS has the scene, so it keeps it alive for a scene item.
                        scene.RemovalUnresolved = false;
                        scene.CheckAfter = 0;
                        this._unresolved.Remove(scene.Uuid);
                        if (!scene.Announced)
                        {
                            scene.Announced = true;
                            this.Emit(StudioChangeKind.SceneAdded, scene.Uuid, 0, scene.Name.Value);
                        }
                    }
                    else
                    {
                        // The read was taken before the doubt: it says nothing about now.
                        scene.ItemsScope.MarkDirty();
                    }
                }
            }
            else if (notFound)
            {
                if (scene.RemovalUnresolved || scene.IsGroup || scene.State == SourceState.Detached)
                {
                    // A scene that was removed: OBS does not have it (a scene that it keeps alive can be read), so it was destroyed.
                    // A group is in no list of scenes, so no list can ever say that it is gone: its 600 does.
                    this.DestroyScene(scene, decidedByRead: true);
                }
                else
                {
                    // OBS does not have the scene any more (its removal may not have arrived yet): the list of scenes decides, and the scene
                    // items are read again after a wait for as long as the scene is there, so that nothing is left unread without a wake-up.
                    this._scenesScope.MarkDirty();
                    this.Retry(scene.ItemsScope);
                }
            }
            else
            {
                this.Retry(scene.ItemsScope);
            }
        }

        private void ApplyAudioResponse(ReadTicket ticket, ReadRequest request, RequestResponseMessage? muteResponse, RequestResponseMessage? volumeResponse, bool notFound)
        {
            if (!this._inputs.TryGetValue(request.Target, out var input))
            {
                return;
            }

            var muteOk = muteResponse is { RequestStatus.Result: true } && muteResponse.ResponseData is InputMutedResponse;
            var volumeOk = volumeResponse is { RequestStatus.Result: true } && volumeResponse.ResponseData is InputVolumeResponse;
            if (muteOk && volumeOk)
            {
                input.AudioScope.Attempts = 0;
                var muted = (InputMutedResponse)muteResponse!.ResponseData!;
                var volume = (InputVolumeResponse)volumeResponse!.ResponseData!;
                var oldMute = input.Muted.Value;
                var oldVolume = input.VolumeDb.Value;
                var muteChanged = input.Muted.ApplyRead(muted.InputMuted, ticket.SentSeq);
                var mulChanged = input.VolumeMul.ApplyRead(volume.InputVolumeMul, ticket.SentSeq);
                var dbChanged = input.VolumeDb.ApplyRead(volume.InputVolumeDb, ticket.SentSeq);
                input.AudioKnown = true;
                if (input.Announced)
                {
                    if (muteChanged)
                    {
                        this.Emit(StudioChangeKind.InputMuteChanged, input.Uuid, 0, input.Name.Value, oldMute, input.Muted.Value);
                    }

                    if (mulChanged || dbChanged)
                    {
                        this.Emit(StudioChangeKind.InputVolumeChanged, input.Uuid, 0, input.Name.Value, oldVolume, input.VolumeDb.Value);
                    }
                }

                this.TryAnnounce(input);
            }
            else if (notFound)
            {
                // The input is gone (its removal may not have arrived yet): the list of inputs decides, and the audio state is read again
                // after a wait for as long as the input is there (the list can still have it for a while).
                this._inputsScope.MarkDirty();
                this.Retry(input.AudioScope);
            }
            else if (muteResponse is { RequestStatus.Result: false } && muteResponse.RequestStatus.Code == RequestStatusCode.InvalidResourceState)
            {
                // The capabilities said that it has audio, and OBS says that it has not: there is no audio state to read.
                input.AudioKnown = true;
                this.TryAnnounce(input);
            }
            else
            {
                this.Retry(input.AudioScope);
            }
        }

        private void ApplyScenes(SceneListResponse response, ReadTicket ticket)
        {
            var sent = ticket.SentSeq;
            var listed = new HashSet<Guid>();
            var order = new List<Guid>();
            foreach (var entry in response.Scenes)
            {
                var uuid = entry.SceneUuid;
                if (this._gone.Contains(uuid))
                {
                    // The list lags: it still has a scene that was removed.
                    continue;
                }

                if (this._scenes.TryGetValue(uuid, out var scene))
                {
                    if (scene.State != SourceState.Live)
                    {
                        if (!(scene.Provisional && !scene.RemovedAnnounced))
                        {
                            continue;
                        }

                        // A scene that was only inferred to be Detached is in the list: it is a live scene.
                        scene.Provisional = false;
                        scene.State = SourceState.Live;
                    }

                    var old = scene.Name.Value;
                    if (scene.Name.ApplyRead(entry.SceneName, sent))
                    {
                        this.Emit(StudioChangeKind.SceneRenamed, scene.Uuid, 0, scene.Name.Value, old, scene.Name.Value);
                    }

                    if (scene.CreatedSeq <= sent)
                    {
                        scene.Confirmed = true;
                    }

                    listed.Add(uuid);
                    order.Add(uuid);
                }
                else
                {
                    // A scene that the model did not know: it exists as of the moment that the read was sent.
                    var created = new SceneNode(uuid, entry.SceneName, 0, sent, ticket.SentAt, confirmed: true) { Announced = true };
                    this._scenes[uuid] = created;
                    created.ItemsScope.MarkDirty();
                    if (this._removedUnknown.Contains(uuid))
                    {
                        // An event told that OBS removed it before the model knew it. The list lags behind removals, and does not have a scene that
                        // OBS keeps alive, so it says nothing about which: the scene items decide, when they are known.
                        created.Announced = false;
                        created.State = SourceState.Detached;
                        created.RemovedAnnounced = true;
                        created.RemovalUnresolved = true;
                        this._unresolved.Add(uuid);
                        continue;
                    }

                    this.Emit(StudioChangeKind.SceneAdded, uuid, 0, created.Name.Value);
                    listed.Add(uuid);
                    order.Add(uuid);
                }
            }

            this.ApplySceneOrder(order, fromEvent: false, sent);
            if (response.CurrentProgramSceneUuid is { } programUuid && this._programUuid.ApplyRead(programUuid, sent))
            {
                this.Emit(StudioChangeKind.ProgramSceneChanged, programUuid, 0, response.CurrentProgramSceneName);
            }

            // Scenes that the model holds and that the list does not have.
            var pending = false;
            foreach (var scene in this._scenes.Values.Where(s => s.State == SourceState.Live && !s.IsGroup && !listed.Contains(s.Uuid)).ToArray())
            {
                if (scene.CreatedSeq > sent)
                {
                    // Known after the read was sent: the read cannot say anything about it.
                    continue;
                }

                if (!scene.Confirmed && ticket.SentAt - scene.CreatedAt < this._options.CreationGrace)
                {
                    // An event announced it, and the list lags behind the events: it is read again.
                    pending = true;
                    continue;
                }

                // Listed before and gone now, or announced and still not listed after the grace: OBS does not have it in the list. Whether it
                // exists still (it is kept alive for a scene item) is for a read of its scene items to say.
                this.RemoveSceneUnverified(scene);
            }

            if (pending)
            {
                this._scenesScope.MarkDirtyNotBefore(this._now + this.Backoff(this._scenesScope));
            }
            else
            {
                // Nothing waits for the list any more: the next repeat starts the back-off again.
                this._scenesScope.Attempts = 0;
            }

            this._scenesRead = true;
        }

        private void ApplyInputs(InputsResponse response, ReadTicket ticket)
        {
            var sent = ticket.SentSeq;
            var listed = new HashSet<Guid>();
            foreach (var entry in response.Inputs)
            {
                if (entry.InputUuid is not { } uuid || this._gone.Contains(uuid))
                {
                    // An input without UUID is an OBS that is too old for the model; one that is gone is a lingering entry of the list.
                    continue;
                }

                listed.Add(uuid);
                if (this._inputs.TryGetValue(uuid, out var input))
                {
                    var old = input.Name.Value;
                    if (input.Name.ApplyRead(entry.InputName, sent))
                    {
                        this.Emit(StudioChangeKind.InputRenamed, uuid, 0, input.Name.Value, old, input.Name.Value);
                    }

                    input.Kind = entry.InputKind;
                    input.UnversionedKind = entry.UnversionedInputKind;
                    if (entry.InputKindCaps is { } caps)
                    {
                        input.Caps = caps;
                    }

                    if (input.State == SourceState.Live && input.CreatedSeq <= sent)
                    {
                        input.Confirmed = true;
                    }

                    if (input.RemovalUnresolved && input.CheckAfter > 0)
                    {
                        if (ticket.Id > input.CheckAfter)
                        {
                            // OBS has the input, so it keeps it alive for a scene item.
                            input.RemovalUnresolved = false;
                            input.CheckAfter = 0;
                            this._unresolved.Remove(uuid);
                        }
                        else
                        {
                            // The read was taken before the doubt: it says nothing about now.
                            this._inputsScope.MarkDirty();
                        }
                    }
                    if (input.HasAudio && !input.AudioKnown && !input.RemovalUnresolved && !input.AudioScope.Dirty && !input.AudioScope.InFlight)
                    {
                        input.AudioScope.MarkDirty();
                    }

                    this.TryAnnounce(input);
                }
                else
                {
                    var created = new InputNode(uuid, entry.InputName, entry.InputKind, entry.UnversionedInputKind, entry.InputKindCaps, sent, ticket.SentAt, confirmed: true);
                    this._inputs[uuid] = created;
                    if (this._removedUnknown.Contains(uuid))
                    {
                        // An event told that OBS removed it before the model knew it. The list has it while a scene item keeps it alive, and for a while
                        // after it was destroyed: the scene items decide, when they are known.
                        created.State = SourceState.Detached;
                        created.RemovedAnnounced = true;
                        created.RemovalUnresolved = true;
                        this._unresolved.Add(uuid);
                        continue;
                    }

                    if (created.HasAudio)
                    {
                        created.AudioScope.MarkDirty();
                    }

                    this.TryAnnounce(created);
                }
            }

            // Inputs that the model holds and that the list does not have: the list of inputs is right at once after a creation, and an input
            // that OBS removed stays in it while a scene item uses it, so an input that is missing does not exist, whatever the scene items that
            // the model knows say.
            foreach (var input in this._inputs.Values.Where(i => !listed.Contains(i.Uuid)).ToArray())
            {
                if (input.CreatedSeq > sent)
                {
                    continue;
                }

                if (input.State == SourceState.Live)
                {
                    this.Emit(StudioChangeKind.Repaired, input.Uuid, 0, input.Name.Value);
                }

                this.DestroyInput(input);
            }

            this._inputsRead = true;
        }

        private void ApplyItems(SceneNode scene, SceneItem[] entries, ReadTicket ticket)
        {
            var sent = ticket.SentSeq;
            var listed = new HashSet<int>();
            foreach (var entry in entries)
            {
                listed.Add(entry.SceneItemId);
                if (scene.Items.TryGetValue(entry.SceneItemId, out var item))
                {
                    this.ApplyItemEntry(item, entry, sent);

                    // An item that an event made has no word about its source being a group or a scene that the lists do not have: this read does.
                    this.EnsureItemSource(entry, item.SourceUuid);
                    continue;
                }

                var key = new ItemKey(scene.Uuid, entry.SceneItemId);
                if (this._goneItems.Contains(key))
                {
                    // The read was sent before the item was removed.
                    continue;
                }

                var sourceUuid = entry.SourceUuid ?? Guid.Empty;
                item = new ItemNode(scene, entry.SceneItemId, sourceUuid, entry.SceneItemIndex, sent, fromRead: true);
                scene.Items[item.Id] = item;
                this.AddRef(item);
                this.ApplyItemEntry(item, entry, sent);
                this.EnsureItemSource(entry, sourceUuid);
            }

            // Items that the model holds and that the list does not have. The list of items is right at once, so an item that is missing was
            // removed, and its event is on its way (or never comes).
            foreach (var item in scene.Items.Values.Where(i => !listed.Contains(i.Id)).ToArray())
            {
                if (item.CreatedSeq <= sent)
                {
                    this.ReleaseItem(item, fromRead: true);
                }
            }

            scene.ItemsKnown = true;
            this.NormalizeItems(scene);
        }

        private void ApplyItemEntry(ItemNode item, SceneItem entry, long sent)
        {
            if (item.Index.ApplyRead(entry.SceneItemIndex, sent) && item.Announced)
            {
                this.Emit(StudioChangeKind.ItemIndexChanged, item.Scene.Uuid, item.Id);
            }

            var oldEnabled = item.Enabled.Value;
            if (item.Enabled.ApplyRead(entry.SceneItemEnabled, sent) && item.Announced)
            {
                this.Emit(StudioChangeKind.ItemEnabledChanged, item.Scene.Uuid, item.Id, null, oldEnabled, item.Enabled.Value);
            }

            var oldLocked = item.Locked.Value;
            if (item.Locked.ApplyRead(entry.SceneItemLocked, sent) && item.Announced)
            {
                this.Emit(StudioChangeKind.ItemLockedChanged, item.Scene.Uuid, item.Id, null, oldLocked, item.Locked.Value);
            }

            item.Known = true;
            this.TryAnnounce(item);
        }

        // The source of an item that a read found: when it is a scene that the lists do not have, it is a scene that OBS removed and keeps alive.
        private void EnsureItemSource(SceneItem entry, Guid sourceUuid)
        {
            if (sourceUuid == Guid.Empty || this._inputs.ContainsKey(sourceUuid) || this._scenes.ContainsKey(sourceUuid) || this._gone.Contains(sourceUuid))
            {
                return;
            }

            if (entry.SourceType == SourceType.OBS_SOURCE_TYPE_SCENE || entry.IsGroup == true)
            {
                var group = entry.IsGroup == true;

                // An event told that OBS removed it before the model knew it: that stays true. Whether OBS keeps it alive is decided by the scene items.
                var removed = this._removedUnknown.Contains(sourceUuid);
                var scene = new SceneNode(sourceUuid, entry.SourceName, -1, this._seq, this._now, confirmed: true)
                {
                    IsGroup = group,
                    Provisional = !group && !removed,
                    State = group && !removed ? SourceState.Live : SourceState.Detached,
                    Announced = !removed,
                    RemovedAnnounced = removed,
                    RemovalUnresolved = removed,
                };
                this._scenes[sourceUuid] = scene;
                if (removed)
                {
                    this._unresolved.Add(sourceUuid);
                }

                scene.ItemsScope.MarkDirty();
                if (!group && !removed)
                {
                    // The list of scenes may only lag: it is read again, and a scene that it has is live.
                    this._scenesScope.MarkDirty();
                }

                return;
            }

            this._inputsScope.MarkDirty();
        }
    }
}
