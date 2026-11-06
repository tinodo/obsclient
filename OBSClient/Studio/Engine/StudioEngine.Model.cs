namespace OBSStudioClient.Studio.Engine
{
    using OBSStudioClient.Enums;

    internal sealed partial class StudioEngine
    {
        // The number of the message that is being processed (an event), or that the read that is being applied was sent at: what the values
        // that the model derives itself (the indexes that shift when an item goes) are stamped with.
        private long _seq;

        private int RefCount(Guid sourceUuid)
        {
            return this._refs.TryGetValue(sourceUuid, out var set) ? set.Count : 0;
        }

        private void AddRef(ItemNode item)
        {
            if (!this._refs.TryGetValue(item.SourceUuid, out var set))
            {
                set = new HashSet<ItemKey>();
                this._refs[item.SourceUuid] = set;
            }

            set.Add(item.Key);
        }

        // Removes an item from its scene and from the references of its source, and follows up on the source: a source that nothing uses any
        // more is destroyed by OBS when it was removed. The indexes of the other items are the business of the caller. When a read said that the
        // item is gone, the scene items that the model knows can be behind OBS (the events of a creation can still be on their way), so
        // that source is not destroyed by counting: a read says whether OBS still has it.
        private void ReleaseItem(ItemNode item, bool fromRead = false)
        {
            item.Scene.Items.Remove(item.Id);
            this._goneItems.Add(item.Key);
            if (this._refs.TryGetValue(item.SourceUuid, out var set))
            {
                set.Remove(item.Key);
                if (set.Count == 0)
                {
                    this._refs.Remove(item.SourceUuid);
                }
            }

            if (item.Announced)
            {
                this.Emit(StudioChangeKind.ItemRemoved, item.Scene.Uuid, item.Id);
            }

            this.OnReferencesChanged(item.SourceUuid, fromRead);
        }

        // Called when a scene item that used a source is gone.
        private void OnReferencesChanged(Guid sourceUuid, bool fromRead = false)
        {
            if (this.RefCount(sourceUuid) > 0)
            {
                return;
            }

            if (this._inputs.TryGetValue(sourceUuid, out var input))
            {
                if (input.State == SourceState.Detached)
                {
                    // OBS removed it earlier, and keeps it alive for its scene items: the last one went, so it is destroyed (and OBS says nothing).
                    // When the model does not know all scene items yet, one that it does not know may still use it.
                    if (fromRead)
                    {
                        this.RequireCheck(input);
                        this._inputsScope.MarkDirty();
                    }
                    else if (input.CheckAfter == 0 && (!input.RemovalUnresolved || this.ReferencesComplete()))
                    {
                        this.DestroyInput(input);
                    }
                }
                else
                {
                    // A live input that nothing uses is destroyed by OBS, which sends InputRemoved for it. An input that OBS had removed
                    // before this connection (the model cannot tell it from a live one) is destroyed without an event: the list of inputs shows.
                    this._inputsScope.MarkDirtyNotBefore(this._now + this._options.InputCheckDelay);
                }
            }
            else if (this._scenes.TryGetValue(sourceUuid, out var scene) && scene.State == SourceState.Detached)
            {
                // A scene that no event removed was found Detached by a read: it was only inferred to exist (a scene item that a read found shows it), or a
                // list of scenes lacked it. A read is ahead of the events: when OBS removed the scene, and which scene items used it then, are for the events
                // that are still on their way to say. An older event can take away the only scene item that the model knows, while another one that was made
                // before the removal keeps the scene alive. Not a count, but a read says whether OBS still has it, and the event that removes it counts the
                // scene items as they are then (a scene that an event removed is destroyed by a count: see above).
                if (fromRead || !scene.RemovedAnnounced)
                {
                    this.RequireCheck(scene);
                    scene.ItemsScope.MarkDirty();
                }
                else if (scene.CheckAfter == 0 && (!scene.RemovalUnresolved || this.ReferencesComplete()))
                {
                    this.DestroyScene(scene);
                }
            }
        }

        // The model doubts whether OBS still has a source that it removed: a read that is taken from now on says (see SourceNode.CheckAfter).
        private void RequireCheck(SourceNode source)
        {
            source.CheckAfter = this._nextTicket;
            if (!source.RemovalUnresolved)
            {
                source.RemovalUnresolved = true;
                this._unresolved.Add(source.Uuid);
            }
        }

        // Whether the model knows the scene items of every scene that it holds, so that the number of scene items that use a source is right.
        // It does not before the list of scenes was read, and not while a scene that was found later has not been read.
        private bool ReferencesComplete()
        {
            if (!this._scenesRead)
            {
                return false;
            }

            foreach (var scene in this._scenes.Values)
            {
                if (!scene.ItemsKnown)
                {
                    return false;
                }
            }

            return true;
        }

        // Whether OBS removed the source (and was told of it), or destroyed it.
        private bool Removed(Guid uuid)
        {
            return this._gone.Contains(uuid) || this._removedUnknown.Contains(uuid);
        }

        // Decides what OBS did with the sources that were removed while the model did not know all scene items: when it does, a source that
        // nothing uses was destroyed, and one that a scene item uses is kept alive.
        private void ResolveRemovals()
        {
            if (this._unresolved.Count == 0 || !this.ReferencesComplete())
            {
                return;
            }

            foreach (var uuid in this._unresolved.ToArray())
            {
                if (this._inputs.TryGetValue(uuid, out var input) && input.RemovalUnresolved)
                {
                    if (input.CheckAfter > 0)
                    {
                        // A read of the list of inputs decides (ApplyInputs), not a count.
                        continue;
                    }

                    input.RemovalUnresolved = false;
                    this._unresolved.Remove(uuid);
                    if (this.RefCount(uuid) == 0)
                    {
                        this.DestroyInput(input);
                    }
                    else
                    {
                        // Kept alive, so it exists: its audio state is read, and it is announced when that is known.
                        if (input.HasAudio && !input.AudioKnown)
                        {
                            input.AudioScope.MarkDirty();
                        }

                        this.TryAnnounce(input);
                    }
                }
                else if (this._scenes.TryGetValue(uuid, out var scene) && scene.RemovalUnresolved)
                {
                    if (scene.CheckAfter > 0)
                    {
                        // A read of the scene items of the scene decides (ApplyItemsResponse), not a count.
                        continue;
                    }

                    scene.RemovalUnresolved = false;
                    this._unresolved.Remove(uuid);
                    if (this.RefCount(uuid) == 0)
                    {
                        this.DestroyScene(scene);
                    }
                    else if (!scene.Announced)
                    {
                        scene.Announced = true;
                        this.Emit(StudioChangeKind.SceneAdded, scene.Uuid, 0, scene.Name.Value);
                    }
                }
                else
                {
                    this._unresolved.Remove(uuid);
                }
            }
        }

        // What happened to a source that OBS removed: it was destroyed, it is kept alive, or the model cannot tell yet.
        private RemovalOutcome OutcomeOfRemoval(Guid uuid, bool authoritative)
        {
            var users = this.RefCount(uuid);
            if (authoritative)
            {
                return users > 0 ? RemovalOutcome.KeptAlive : RemovalOutcome.Destroyed;
            }

            // Until every scene item is known a count is what is known so far: a scene item that is not read yet can use the source, and the one
            // that is known can go and leave it with none that the model knows of.
            if (!this.ReferencesComplete())
            {
                return RemovalOutcome.Unknown;
            }

            return users > 0 ? RemovalOutcome.KeptAlive : RemovalOutcome.Destroyed;
        }

        private enum RemovalOutcome
        {
            Destroyed,
            KeptAlive,
            Unknown,
        }

        private void DestroyInput(InputNode input)
        {
            if (input.State == SourceState.Gone)
            {
                return;
            }

            input.State = SourceState.Gone;
            input.RemovalUnresolved = false;
            input.CheckAfter = 0;
            this._unresolved.Remove(input.Uuid);
            this._removedUnknown.Remove(input.Uuid);
            this._inputs.Remove(input.Uuid);
            this._gone.Add(input.Uuid);
            if (input.Announced)
            {
                this.Emit(StudioChangeKind.InputRemoved, input.Uuid, 0, input.Name.Value);
            }
        }

        private void DestroyScene(SceneNode scene)
        {
            if (scene.State == SourceState.Gone)
            {
                return;
            }

            var wasLive = scene.State == SourceState.Live;
            scene.State = SourceState.Gone;
            scene.RemovalUnresolved = false;
            scene.CheckAfter = 0;
            this._unresolved.Remove(scene.Uuid);
            this._removedUnknown.Remove(scene.Uuid);
            this._scenes.Remove(scene.Uuid);
            this._gone.Add(scene.Uuid);
            if (wasLive && !scene.IsGroup)
            {
                this.CloseSceneGap(scene);
            }

            // OBS sends no SceneItemRemoved for the items of a scene that goes, and destroys the inputs that only these items used.
            foreach (var item in scene.Items.Values.ToArray())
            {
                this.ReleaseItem(item);
            }

            if (this._programUuid.Value == scene.Uuid)
            {
                this._programScope.MarkDirty();
            }

            if (scene.Announced)
            {
                this.Emit(StudioChangeKind.SceneRemoved, scene.Uuid, 0, scene.Name.Value);
            }
        }

        // The scenes after a scene that leaves the list move up.
        private void CloseSceneGap(SceneNode removed)
        {
            var index = removed.Index.Value;
            foreach (var other in this._scenes.Values)
            {
                if (!other.IsGroup && other.State == SourceState.Live && !ReferenceEquals(other, removed) && other.Index.Value > index
                    && other.Index.ApplyDerived(other.Index.Value - 1, this._seq))
                {
                    this.Emit(StudioChangeKind.SceneOrderChanged, other.Uuid, 0, other.Name.Value);
                }
            }
        }

        // A scene that a list of scenes does not have, while no event told that OBS removed it (the event can be on its way, or the connection
        // missed it). The list does not say whether OBS destroyed it or keeps it alive for a scene item, and what the model knows of the scene items
        // can be behind what OBS has: a read of the scene items of the scene says (ApplyItemsResponse). The scene is Detached until it comes.
        private void RemoveSceneUnverified(SceneNode scene)
        {
            if (scene.State != SourceState.Live)
            {
                return;
            }

            this.Emit(StudioChangeKind.Repaired, scene.Uuid, 0, scene.Name.Value);
            this.RequireCheck(scene);
            scene.State = SourceState.Detached;
            scene.ItemsScope.MarkDirty();
            if (!scene.IsGroup)
            {
                this.CloseSceneGap(scene);
            }

            if (this._programUuid.Value == scene.Uuid)
            {
                this._programScope.MarkDirty();
            }

            if (scene.Announced)
            {
                this.Emit(StudioChangeKind.SourceDetached, scene.Uuid, 0, scene.Name.Value);
            }
        }

        // A scene that OBS removed, because an event told so, or because a read showed that it does not exist (repaired). A scene that is Detached already
        // (a read showed that the list of scenes lacks it, or it was only inferred to exist: see EnsureItemSource) is not announced as Detached again, and its
        // place in the list of scenes is not closed again; what OBS did with it is decided as for any scene.
        private void RemoveScene(SceneNode scene, bool repaired, bool detachedBefore)
        {
            if (scene.State != SourceState.Live && !detachedBefore)
            {
                return;
            }

            scene.Provisional = false;

            // A check that was under way asked whether OBS has the scene (it asked about a scene that was only inferred, and a creation event can have made
            // it Live since). An answer that was taken before this event says that OBS had it then: nothing about what OBS did with it when it removed it.
            // The event is the word now, and the scene items decide what it did.
            scene.CheckAfter = 0;
            scene.RemovalUnresolved = false;
            this._unresolved.Remove(scene.Uuid);
            if (repaired)
            {
                this.Emit(StudioChangeKind.Repaired, scene.Uuid, 0, scene.Name.Value);
            }

            var outcome = this.OutcomeOfRemoval(scene.Uuid, repaired);
            if (outcome == RemovalOutcome.Destroyed)
            {
                this.DestroyScene(scene);
                return;
            }

            // Nested in another scene: OBS keeps it alive, with everything in it. Or the model does not know all scene items yet, and it is
            // kept until it does (ResolveRemovals).
            if (outcome == RemovalOutcome.Unknown)
            {
                scene.RemovalUnresolved = true;
                this._unresolved.Add(scene.Uuid);
            }

            scene.State = SourceState.Detached;
            if (!detachedBefore && !scene.IsGroup)
            {
                this.CloseSceneGap(scene);
            }

            if (this._programUuid.Value == scene.Uuid)
            {
                this._programScope.MarkDirty();
            }

            // A scene that was Detached already was announced as a detached scene then.
            if (!detachedBefore && scene.Announced)
            {
                this.Emit(StudioChangeKind.SourceDetached, scene.Uuid, 0, scene.Name.Value);
            }
        }

        // An input that OBS removed, because an event told so, or because a read showed that it does not exist (repaired).
        private void RemoveInput(InputNode input, bool repaired)
        {
            if (input.State != SourceState.Live)
            {
                return;
            }

            if (repaired)
            {
                this.Emit(StudioChangeKind.Repaired, input.Uuid, 0, input.Name.Value);
            }

            var outcome = this.OutcomeOfRemoval(input.Uuid, repaired);
            if (outcome == RemovalOutcome.Destroyed)
            {
                this.DestroyInput(input);
                return;
            }

            // Scene items still use it: OBS keeps it alive, and so does the model. Or the model does not know all scene items yet, and it is
            // kept until it does (ResolveRemovals).
            if (outcome == RemovalOutcome.Unknown)
            {
                input.RemovalUnresolved = true;
                this._unresolved.Add(input.Uuid);
            }

            input.State = SourceState.Detached;
            if (input.Announced)
            {
                this.Emit(StudioChangeKind.SourceDetached, input.Uuid, 0, input.Name.Value);
            }
        }

        // The indexes of the items of a scene are 0, 1, 2, ... in order. When they are not, something that the model holds is out of date: the
        // scene is read again.
        private void NormalizeItems(SceneNode scene)
        {
            var expected = 0;
            foreach (var item in scene.Items.Values.OrderBy(i => i.Index.Value).ThenBy(i => i.Id))
            {
                if (item.Index.Value != expected++)
                {
                    scene.ItemsScope.MarkDirty();
                    return;
                }
            }
        }

        private void TryAnnounce(InputNode input)
        {
            if (!input.Announced && input.State != SourceState.Gone && !input.RemovalUnresolved && input.IsComplete)
            {
                input.Announced = true;
                this.Emit(StudioChangeKind.InputAdded, input.Uuid, 0, input.Name.Value);
            }
        }

        private void TryAnnounce(ItemNode item)
        {
            if (!item.Announced && item.Known)
            {
                item.Announced = true;
                this.Emit(StudioChangeKind.ItemAdded, item.Scene.Uuid, item.Id);
            }
        }

        // A scene item whose source the model does not know: both lists are read to find it.
        private void EnsureSourceKnown(Guid sourceUuid)
        {
            if (sourceUuid == Guid.Empty || this._inputs.ContainsKey(sourceUuid) || this._scenes.ContainsKey(sourceUuid) || this._gone.Contains(sourceUuid))
            {
                return;
            }

            this._inputsScope.MarkDirty();
            this._scenesScope.MarkDirty();
        }

        // Gives the scenes the positions of the list, in the order of the list: the scenes of the list first, then the live scenes that the
        // list does not have (they are newer than the list), in the order that they had.
        private void ApplySceneOrder(IEnumerable<Guid> listed, bool fromEvent, long seq)
        {
            var ordered = new List<SceneNode>();
            var seen = new HashSet<Guid>();
            foreach (var uuid in listed)
            {
                if (this._scenes.TryGetValue(uuid, out var scene) && scene.State == SourceState.Live && !scene.IsGroup && seen.Add(uuid))
                {
                    ordered.Add(scene);
                }
            }

            ordered.AddRange(this._scenes.Values.Where(s => s.State == SourceState.Live && !s.IsGroup && !seen.Contains(s.Uuid)).OrderBy(s => s.Index.Value).ThenBy(s => s.Uuid));
            for (var i = 0; i < ordered.Count; i++)
            {
                var changed = fromEvent ? ordered[i].Index.ApplyEvent(i, seq) : ordered[i].Index.ApplyRead(i, seq);
                if (changed)
                {
                    this.Emit(StudioChangeKind.SceneOrderChanged, ordered[i].Uuid, 0, ordered[i].Name.Value);
                }
            }
        }
    }
}
