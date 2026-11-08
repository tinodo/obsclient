namespace OBSStudioClient.Studio.Engine
{
    using System.Collections.Frozen;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Messages;

    internal sealed partial class StudioEngine
    {
        // The events that the model acts on, and what it does with each of them. The list that Handles answers from is this table, so that an event that the engine
        // acts on can never be left out of the events that the host passes to it. An event whose data is not of the type that its handler expects is ignored.
        private static readonly FrozenDictionary<EventType, Action<StudioEngine, EventMessage>> Handlers = new Dictionary<EventType, Action<StudioEngine, EventMessage>>
        {
            // The name in these two is not used (yet), so they are acted on whatever their data: a switch that is missed would leave the model of a collection that is gone.
            [EventType.CurrentSceneCollectionChanging] = static (engine, _) => engine.OnCollectionChanging(),
            [EventType.CurrentSceneCollectionChanged] = static (engine, _) => engine.OnCollectionChanged(),
            [EventType.SceneCreated] = On<SceneModifiedEventArgs>(static (engine, e) => engine.OnSceneCreated(e)),
            [EventType.SceneRemoved] = On<SceneModifiedEventArgs>(static (engine, e) => engine.OnSceneRemoved(e)),
            [EventType.SceneNameChanged] = On<SceneNameChangedEventArgs>(static (engine, e) => engine.OnSceneNameChanged(e)),
            [EventType.SceneListChanged] = On<SceneListEventArgs>(static (engine, e) => engine.OnSceneListChanged(e)),

            // Never taken from the payload: the event comes at the end of a transition, and a read says what the program scene is now.
            [EventType.CurrentProgramSceneChanged] = static (engine, _) => engine._programScope.MarkDirty(),
            [EventType.InputCreated] = On<InputCreatedEventArgs>(static (engine, e) => engine.OnInputCreated(e)),
            [EventType.InputRemoved] = On<InputNameEventArgs>(static (engine, e) => engine.OnInputRemoved(e)),
            [EventType.InputNameChanged] = On<InputNameChangedEventArgs>(static (engine, e) => engine.OnInputNameChanged(e)),
            [EventType.InputMuteStateChanged] = On<InputMuteStateChangedEventArgs>(static (engine, e) => engine.OnInputMuteChanged(e)),
            [EventType.InputVolumeChanged] = On<InputVolumeChangedEventArgs>(static (engine, e) => engine.OnInputVolumeChanged(e)),
            [EventType.SceneItemCreated] = On<SceneItemCreatedEventArgs>(static (engine, e) => engine.OnItemCreated(e)),
            [EventType.SceneItemRemoved] = On<SceneItemRemovedEventArgs>(static (engine, e) => engine.OnItemRemoved(e)),
            [EventType.SceneItemListReindexed] = On<SceneItemListReindexedEventArgs>(static (engine, e) => engine.OnItemsReindexed(e)),
            [EventType.SceneItemEnableStateChanged] = On<SceneItemEnableStateChangedEventArgs>(static (engine, e) => engine.OnItemEnabledChanged(e)),
            [EventType.SceneItemLockStateChanged] = On<SceneItemLockStateChangedEventArgs>(static (engine, e) => engine.OnItemLockedChanged(e)),
            [EventType.StudioModeStateChanged] = On<StudioModeStateChangedEventArgs>(static (engine, e) => engine.OnStudioModeChanged(e)),
        }.ToFrozenDictionary();

        /// <summary>
        /// Tells the engine that an event arrived. Events are processed in the order of their numbers.
        /// </summary>
        /// <param name="message">The event.</param>
        /// <param name="seq">The number of the event.</param>
        /// <param name="now">The time.</param>
        public void OnEvent(EventMessage message, long seq, TimeSpan now)
        {
            this._now = now;
            this._seq = seq;
            if (this._state == StudioEngineState.Disconnected)
            {
                return;
            }

            // While OBS Studio changes its scene collection it has nothing to say about the collection that the model will hold: what it sends is about the one that
            // it is leaving, or (when a server does send them) about the sources that load, and the model of the new collection is read when the switch is over.
            // Only the end of the switch counts.
            if (this._switching && message.EventType != EventType.CurrentSceneCollectionChanged)
            {
                return;
            }

            if (Handlers.TryGetValue(message.EventType, out var handler))
            {
                handler(this, message);
            }

            this.UpdateState();
            this.CleanUp();
        }

        private static Action<StudioEngine, EventMessage> On<TData>(Action<StudioEngine, TData> handle)
            where TData : class
        {
            return (engine, message) =>
            {
                if (message.EventData is TData data)
                {
                    handle(engine, data);
                }
            };
        }

        private void OnStudioModeChanged(StudioModeStateChangedEventArgs e)
        {
            if (this._studioMode.ApplyEvent(e.StudioModeEnabled, this._seq))
            {
                this.Emit(StudioChangeKind.StudioModeChanged, Guid.Empty, 0, null, !e.StudioModeEnabled, e.StudioModeEnabled);
            }
        }

        private void OnSceneCreated(SceneModifiedEventArgs e)
        {
            if (this.Removed(e.SceneUuid))
            {
                // A late copy of the event of a scene that is gone already (obs-websocket sends it twice).
                return;
            }

            if (this._scenes.TryGetValue(e.SceneUuid, out var known))
            {
                // The second copy of the event, or a scene that a read found first. A scene that was only inferred to exist is real now.
                if (known.Provisional && known.State == SourceState.Detached && !known.RemovedAnnounced)
                {
                    known.Provisional = false;
                    known.State = SourceState.Live;

                    // What inferred it was a read of scene items: it says that the scene exists, not that the list of scenes has it, and the list
                    // lags behind creations. Now that an event announced it, it gets the grace that an announced scene gets.
                    known.Confirmed = false;
                    known.CreatedSeq = this._seq;
                    known.CreatedAt = this._now;
                    this._scenesScope.MarkDirty();
                }

                return;
            }

            var scene = new SceneNode(e.SceneUuid, e.SceneName, 0, this._seq, this._now, confirmed: false) { IsGroup = e.IsGroup };
            this._scenes[scene.Uuid] = scene;
            scene.Announced = true;
            this.Emit(StudioChangeKind.SceneAdded, scene.Uuid, 0, scene.Name.Value);
            if (e.IsGroup)
            {
                scene.Index = new Cell<int>(-1, this._seq);
            }
            else
            {
                // OBS puts a new scene at the top of the list.
                foreach (var other in this._scenes.Values)
                {
                    if (!ReferenceEquals(other, scene) && !other.IsGroup && other.State == SourceState.Live && other.Index.ApplyDerived(other.Index.Value + 1, this._seq))
                    {
                        this.Emit(StudioChangeKind.SceneOrderChanged, other.Uuid, 0, other.Name.Value);
                    }
                }
            }

            // The list of scenes confirms that the scene exists (it can lag behind the event), and says where it is; the items are read too.
            scene.ItemsScope.MarkDirty();
            this._scenesScope.MarkDirty();
        }

        private void OnSceneRemoved(SceneModifiedEventArgs e)
        {
            if (!this._scenes.TryGetValue(e.SceneUuid, out var scene))
            {
                // Not known (yet): a copy of its creation that arrives later must not bring it back. A list that has it still decides whether
                // OBS keeps it alive for a scene item.
                this._removedUnknown.Add(e.SceneUuid, SourceKind.Scene, this._seq, this._now);
                return;
            }

            // The name in the event is the name that the scene had when OBS removed it. The model may not have it: a rename that OBS made while the connection
            // was down is not sent again, and no list of scenes has a scene that OBS keeps alive for the scene items that show it.
            if (!string.IsNullOrEmpty(e.SceneName))
            {
                var old = scene.Name.Value;
                if (scene.Name.ApplyEvent(e.SceneName, this._seq))
                {
                    this.Emit(StudioChangeKind.SceneRenamed, scene.Uuid, 0, scene.Name.Value, old, scene.Name.Value);
                }
            }

            // A scene that a read found Detached (a scene item showed it, or the list of scenes lacked it) is removed by this event as any other: the event
            // says where in the order of what happened OBS removed it, and the scene items that the model knows now decide what OBS did with it.
            var detachedBefore = scene.State == SourceState.Detached && !scene.RemovedAnnounced;
            scene.RemovedAnnounced = true;
            this.RemoveScene(scene, repaired: false, detachedBefore);
        }

        private void OnSceneNameChanged(SceneNameChangedEventArgs e)
        {
            if (this._scenes.TryGetValue(e.SceneUuid, out var scene))
            {
                var old = scene.Name.Value;
                if (scene.Name.ApplyEvent(e.SceneName, this._seq))
                {
                    this.Emit(StudioChangeKind.SceneRenamed, scene.Uuid, 0, scene.Name.Value, old, scene.Name.Value);
                }
            }
            else if (!this.Removed(e.SceneUuid))
            {
                this._scenesScope.MarkDirty();
            }
        }

        private void OnSceneListChanged(SceneListEventArgs e)
        {
            var listed = new List<Guid>();
            foreach (var entry in e.Scenes)
            {
                if (this.Removed(entry.SceneUuid))
                {
                    // The list can still have scenes that were removed.
                    continue;
                }

                if (!this._scenes.TryGetValue(entry.SceneUuid, out var known))
                {
                    // A scene that the model does not know yet: a read finds it.
                    this._scenesScope.MarkDirty();
                    continue;
                }

                if (known.State == SourceState.Detached && !known.Provisional && !known.RemovedAnnounced && !known.IsGroup)
                {
                    // The model gave up on this scene (an earlier list did not have it in time) and no event removed it, and the list has it now: a read of the list says whether it is live.
                    this._scenesScope.MarkDirty();
                    continue;
                }

                listed.Add(entry.SceneUuid);
            }

            this.ApplySceneOrder(listed, fromEvent: true, this._seq);
        }

        private void OnInputCreated(InputCreatedEventArgs e)
        {
            if (this.Removed(e.InputUuid) || this._inputs.ContainsKey(e.InputUuid))
            {
                return;
            }

            // The event has the capabilities of the kind from obs-websocket 5.6.0; without them the input has audio until OBS says that it has not.
            var input = new InputNode(e.InputUuid, e.InputName, e.InputKind, e.UnversionedInputKind, e.InputKindCaps, this._seq, this._now, confirmed: false);
            this._inputs[input.Uuid] = input;
            this._inputsScope.MarkDirty();
            if (input.HasAudio)
            {
                input.AudioScope.MarkDirty();
            }

            this.TryAnnounce(input);
        }

        private void OnInputRemoved(InputNameEventArgs e)
        {
            if (!this._inputs.TryGetValue(e.InputUuid, out var input))
            {
                // Not known (yet): see OnSceneRemoved.
                this._removedUnknown.Add(e.InputUuid, SourceKind.Input, this._seq, this._now);
                return;
            }

            // The name that the input had when OBS removed it: see OnSceneRemoved.
            if (!string.IsNullOrEmpty(e.InputName))
            {
                var old = input.Name.Value;
                if (input.Name.ApplyEvent(e.InputName, this._seq))
                {
                    this.Emit(StudioChangeKind.InputRenamed, input.Uuid, 0, input.Name.Value, old, input.Name.Value);
                }
            }

            input.RemovedAnnounced = true;
            this.RemoveInput(input, repaired: false);
        }

        private void OnInputNameChanged(InputNameChangedEventArgs e)
        {
            if (this._inputs.TryGetValue(e.InputUuid, out var input))
            {
                var old = input.Name.Value;
                if (input.Name.ApplyEvent(e.InputName, this._seq))
                {
                    this.Emit(StudioChangeKind.InputRenamed, input.Uuid, 0, input.Name.Value, old, input.Name.Value);
                }
            }
            else if (!this.Removed(e.InputUuid))
            {
                this._inputsScope.MarkDirty();
            }
        }

        private void OnInputMuteChanged(InputMuteStateChangedEventArgs e)
        {
            if (this._inputs.TryGetValue(e.InputUuid, out var input))
            {
                var old = input.Muted.Value;
                if (input.Muted.ApplyEvent(e.InputMuted, this._seq) && input.Announced)
                {
                    this.Emit(StudioChangeKind.InputMuteChanged, input.Uuid, 0, input.Name.Value, old, input.Muted.Value);
                }
            }
            else if (!this.Removed(e.InputUuid))
            {
                this._inputsScope.MarkDirty();
            }
        }

        private void OnInputVolumeChanged(InputVolumeChangedEventArgs e)
        {
            if (this._inputs.TryGetValue(e.InputUuid, out var input))
            {
                var old = input.VolumeDb.Value;
                var mulChanged = input.VolumeMul.ApplyEvent(e.InputVolumeMul, this._seq);
                var dbChanged = input.VolumeDb.ApplyEvent(e.InputVolumeDb, this._seq);
                if ((mulChanged || dbChanged) && input.Announced)
                {
                    this.Emit(StudioChangeKind.InputVolumeChanged, input.Uuid, 0, input.Name.Value, old, input.VolumeDb.Value);
                }
            }
            else if (!this.Removed(e.InputUuid))
            {
                this._inputsScope.MarkDirty();
            }
        }

        private void OnItemCreated(SceneItemCreatedEventArgs e)
        {
            if (!this._scenes.TryGetValue(e.SceneUuid, out var scene))
            {
                if (!this.Removed(e.SceneUuid))
                {
                    this._scenesScope.MarkDirty();
                }

                return;
            }

            var key = new ItemKey(scene.Uuid, e.SceneItemId);
            if (scene.Items.ContainsKey(e.SceneItemId) || this._goneItems.Contains(key))
            {
                return;
            }

            // The new item takes its place: the items from there upwards move up.
            foreach (var other in scene.Items.Values)
            {
                if (other.Index.Value >= e.SceneItemIndex && other.Index.ApplyDerived(other.Index.Value + 1, this._seq) && other.Announced)
                {
                    this.Emit(StudioChangeKind.ItemIndexChanged, scene.Uuid, other.Id);
                }
            }

            var item = new ItemNode(scene, e.SceneItemId, e.SourceUuid, e.SceneItemIndex, this._seq, fromRead: false);
            scene.Items[item.Id] = item;
            this.AddRef(item);
            this.EnsureSourceKnown(item.SourceUuid);

            // The event has neither enabled nor locked: a read has.
            scene.ItemsScope.MarkDirty();
            this.NormalizeItems(scene);
        }

        private void OnItemRemoved(SceneItemRemovedEventArgs e)
        {
            var key = new ItemKey(e.SceneUuid, e.SceneItemId);

            // A read was ahead of this event (see HeldByRead): as far as the events go the scene item is gone now too. When it was the last that was held for its
            // source, the source settles (see ReadReleasedItems).
            this._readReleased.Remove(key);
            if (!this._scenes.TryGetValue(e.SceneUuid, out var scene) || !scene.Items.TryGetValue(e.SceneItemId, out var item))
            {
                // Not known (yet): a read that was sent before the removal must not bring the item back.
                this._goneItems.Add(key, this._now);
                return;
            }

            var index = item.Index.Value;
            this.ReleaseItem(item);

            // The items above it move down, and OBS does not say so.
            foreach (var other in scene.Items.Values)
            {
                if (other.Index.Value > index && other.Index.ApplyDerived(other.Index.Value - 1, this._seq) && other.Announced)
                {
                    this.Emit(StudioChangeKind.ItemIndexChanged, scene.Uuid, other.Id);
                }
            }

            this.NormalizeItems(scene);
        }

        private void OnItemsReindexed(SceneItemListReindexedEventArgs e)
        {
            if (!this._scenes.TryGetValue(e.SceneUuid, out var scene))
            {
                if (!this.Removed(e.SceneUuid))
                {
                    this._scenesScope.MarkDirty();
                }

                return;
            }

            foreach (var entry in e.SceneItems)
            {
                if (scene.Items.TryGetValue(entry.SceneItemId, out var item))
                {
                    if (item.Index.ApplyEvent(entry.SceneItemIndex, this._seq) && item.Announced)
                    {
                        this.Emit(StudioChangeKind.ItemIndexChanged, scene.Uuid, item.Id);
                    }
                }
                else if (!this._goneItems.Contains(new ItemKey(scene.Uuid, entry.SceneItemId)))
                {
                    scene.ItemsScope.MarkDirty();
                }
            }

            this.NormalizeItems(scene);
        }

        private void OnItemEnabledChanged(SceneItemEnableStateChangedEventArgs e)
        {
            if (this.TryGetItem(e.SceneUuid, e.SceneItemId, out var item))
            {
                var old = item.Enabled.Value;
                if (item.Enabled.ApplyEvent(e.SceneItemEnabled, this._seq) && item.Announced)
                {
                    this.Emit(StudioChangeKind.ItemEnabledChanged, item.Scene.Uuid, item.Id, null, old, item.Enabled.Value);
                }
            }
        }

        private void OnItemLockedChanged(SceneItemLockStateChangedEventArgs e)
        {
            if (this.TryGetItem(e.SceneUuid, e.SceneItemId, out var item))
            {
                var old = item.Locked.Value;
                if (item.Locked.ApplyEvent(e.SceneItemLocked, this._seq) && item.Announced)
                {
                    this.Emit(StudioChangeKind.ItemLockedChanged, item.Scene.Uuid, item.Id, null, old, item.Locked.Value);
                }
            }
        }

        // Finds an item for an event about it. An item that the model does not know (and that was not removed) makes its scene be read.
        private bool TryGetItem(Guid sceneUuid, int itemId, out ItemNode item)
        {
            item = null!;
            if (!this._scenes.TryGetValue(sceneUuid, out var scene))
            {
                if (!this.Removed(sceneUuid))
                {
                    this._scenesScope.MarkDirty();
                }

                return false;
            }

            if (scene.Items.TryGetValue(itemId, out var found))
            {
                item = found;
                return true;
            }

            if (!this._goneItems.Contains(new ItemKey(sceneUuid, itemId)))
            {
                scene.ItemsScope.MarkDirty();
            }

            return false;
        }
    }
}
