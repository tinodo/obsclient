namespace OBSStudioClient.Model.Engine
{
    using OBSStudioClient.Enums;

    internal sealed partial class ModelEngine
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
        // that source is not destroyed by counting: a read says whether OBS still has it. That holds for the scene items that go with a scene that a
        // read said OBS does not have (doubtful) too: the read is about the scene, and it says nothing about what the other scenes hold by now, nor about
        // the events that are on their way (OBS can have removed the source, and made another scene item that uses it). Both are held, until the events
        // have caught up (see ReadReleasedItems).
        private void ReleaseItem(ItemNode item, bool fromRead = false, bool doubtful = false)
        {
            item.Scene.Items.Remove(item.Id);
            this._goneItems.Add(item.Key, this._now);
            if (fromRead || doubtful)
            {
                this._readReleased.Add(item.Key, item.SourceUuid, this._now);
            }

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
                this.Emit(ModelChangeKind.ItemRemoved, item.Scene.Uuid, item.Id);
            }

            this.OnReferencesChanged(item.SourceUuid, fromRead || doubtful);
        }

        // Whether a scene item that a read found gone, and whose removal the events have not told yet, used the source. A read is ahead of the events: as far
        // as the events go that scene item is still there, so a source that was removed while it was may be alive in OBS for a scene item that the model does
        // not know yet (made after the removal, and its event is on its way). What the model counts then is behind OBS, and a count that says that nothing uses
        // the source would destroy it for good: a read says whether OBS still has it.
        private bool HeldByRead(Guid sourceUuid)
        {
            return this._readReleased.Holds(sourceUuid);
        }

        // The scene items of a scene that an event removed went with it: no event tells about them. The sources that they were the last held for settle (see
        // ReadReleasedItems).
        private void ForgetReadReleased(Guid sceneUuid)
        {
            this._readReleased.RemoveScene(sceneUuid);
        }

        // Called when a scene item that used a source is gone.
        private void OnReferencesChanged(Guid sourceUuid, bool fromRead = false)
        {
            if (this.RefCount(sourceUuid) > 0)
            {
                return;
            }

            fromRead |= this.HeldByRead(sourceUuid);
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

        // What was held for a source ended (its time was up). A removed input that no scene item uses is judged as when its last scene item goes (JudgeUnusedInput),
        // and a scene as OnReferencesChanged says.
        private void JudgeAgain(Guid sourceUuid)
        {
            if (this._inputs.TryGetValue(sourceUuid, out var input))
            {
                this.JudgeUnusedInput(input);
            }
            else if (this._scenes.TryGetValue(sourceUuid, out var scene) && scene.State == SourceState.Detached)
            {
                this.OnReferencesChanged(sourceUuid);
            }
        }

        // A removed input that nothing that the model knows uses, and that nothing is held for: OBS destroyed it, or keeps it alive for something that nothing shows
        // (a global audio input that is assigned to a channel), and in neither case can the application reach it. It is destroyed, as when its last scene item goes.
        // Not while a check of the list of inputs is pending (its result judges the input again), nor while something is held for it (the end of that judges it again),
        // nor while it is settling: an event let go of the last scene item that was held for it, and the events that were on their way when the read was taken can
        // still follow, among them the scene item that OBS made for the input meanwhile (a list says that OBS has the input, not who uses it). The time ends that
        // (JudgeAgain).
        private void JudgeUnusedInput(InputNode input)
        {
            if (input.State != SourceState.Detached || input.CheckAfter != 0 || this.RefCount(input.Uuid) > 0 || this.HeldByRead(input.Uuid) || this._readReleased.Settling(input.Uuid))
            {
                return;
            }

            if (this.ReferencesComplete())
            {
                this.DestroyInput(input);
            }
            else if (!input.RemovalUnresolved)
            {
                // The count says nothing until the scene items of every scene are known: ResolveRemovals decides then.
                input.RemovalUnresolved = true;
                this._unresolved.Add(input.Uuid);
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

                    if (this.RefCount(uuid) == 0 && (this.HeldByRead(uuid) || this._readReleased.Settling(uuid)))
                    {
                        // See HeldByRead: the count is behind OBS (for an input that is settling, the events that were on their way when the read was taken can still
                        // follow), so a read of the list of inputs says. A list that lacks the input proves that OBS destroyed it; one that has it leaves the verdict to
                        // JudgeUnusedInput, which does not judge an input that is settling.
                        this.RequireCheck(input);
                        this._inputsScope.MarkDirty();
                        continue;
                    }

                    input.RemovalUnresolved = false;
                    this._unresolved.Remove(uuid);
                    if (this.RefCount(uuid) == 0)
                    {
                        this.JudgeUnusedInput(input);
                    }

                    if (input.State != SourceState.Gone && !input.RemovalUnresolved)
                    {
                        // It survived: a scene item uses it, or the judgment leaves the verdict to something else (the time that it settles). It exists, so its audio state
                        // and its settings are read, and it is announced when they are known. Nothing else asks for that: the removal was unresolved, so the list of inputs did not.
                        if (input.HasAudio && !input.AudioKnown)
                        {
                            input.AudioScope.MarkDirty();
                        }

                        if (!input.SettingsKnown)
                        {
                            input.SettingsScope.MarkDirty();
                        }

                        if (input.IsMedia && !input.MediaKnown)
                        {
                            input.MediaScope.MarkDirty();
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

                    if (this.RefCount(uuid) == 0 && (this.HeldByRead(uuid) || this._readReleased.Settling(uuid)))
                    {
                        // See HeldByRead: the count is behind OBS (for a scene that is settling, the events that were on their way when the read was taken can still
                        // follow), so a read of the scene items of the scene says: OBS has the scene, and it is kept, or it does not, and it is destroyed.
                        this.RequireCheck(scene);
                        scene.ItemsScope.MarkDirty();
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
                        this.Emit(ModelChangeKind.SceneAdded, scene.Uuid, 0, scene.Name.Value);
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
            this._gone.Add(input.Uuid, SourceKind.Input, this._seq, this._now);
            this._readReleased.RemoveSource(input.Uuid);
            if (input.Announced)
            {
                this.Emit(ModelChangeKind.InputRemoved, input.Uuid, 0, input.Name.Value);
            }
        }

        // A scene that OBS destroyed: because an event told that it removed it and nothing shows it, or because the count of the scene items that show it says
        // so, or because a read said that OBS does not have it (decidedByRead).
        private void DestroyScene(SceneNode scene, bool decidedByRead = false)
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
            this._gone.Add(scene.Uuid, SourceKind.Scene, this._seq, this._now);
            this._readReleased.RemoveSource(scene.Uuid);
            if (wasLive && !scene.IsGroup)
            {
                this.CloseSceneGap(scene);
            }

            // OBS sends no SceneItemRemoved for the items of a scene that goes, and destroys the inputs that only these items used. When a read decided
            // it, that is not known of the inputs and scenes that these scene items showed: the scene items that the model knows of the other scenes can be
            // behind what OBS has by then, and a read says whether OBS still has them.
            // What an earlier read took away from the scene is let go of with it when an event removed the scene: the event of a scene item that OBS removed before
            // arrived before this one, so what is still held went with the scene, and nothing tells about it. When a read decided it, the events lag behind the
            // reads: the removal of a scene item that an earlier read found gone (and the events about the sources that it showed) can still be on its way,
            // and what is held for it stays until that event arrives, the source is destroyed, or the time is up.
            if (!decidedByRead)
            {
                this.ForgetReadReleased(scene.Uuid);
            }

            foreach (var item in scene.Items.Values.ToArray())
            {
                this.ReleaseItem(item, doubtful: decidedByRead);
            }

            if (this._programUuid.Value == scene.Uuid)
            {
                this._programScope.MarkDirty();
            }

            if (scene.Announced)
            {
                this.Emit(ModelChangeKind.SceneRemoved, scene.Uuid, 0, scene.Name.Value);
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
                    this.Emit(ModelChangeKind.SceneOrderChanged, other.Uuid, 0, other.Name.Value);
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

            this.Emit(ModelChangeKind.Repaired, scene.Uuid, 0, scene.Name.Value);
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
                this.Emit(ModelChangeKind.SourceDetached, scene.Uuid, 0, scene.Name.Value);
            }
        }

        // A scene that the model held as Detached, and that a list of scenes has while no event removed it: OBS Studio has it as a live scene. It was only inferred to exist, or an
        // earlier list did not have it in time (the model gave up on it and said so: the repair is reported too).
        private void Revive(SceneNode scene)
        {
            var inferred = scene.Provisional;
            scene.Provisional = false;
            scene.State = SourceState.Live;
            scene.RemovalUnresolved = false;
            scene.CheckAfter = 0;
            this._unresolved.Remove(scene.Uuid);
            if (!inferred)
            {
                this.Emit(ModelChangeKind.Repaired, scene.Uuid, 0, scene.Name.Value);
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
                this.Emit(ModelChangeKind.Repaired, scene.Uuid, 0, scene.Name.Value);
            }

            var outcome = this.OutcomeOfRemoval(scene.Uuid, repaired);
            if (outcome == RemovalOutcome.Destroyed && !repaired && this.HeldByRead(scene.Uuid))
            {
                // See HeldByRead: nothing that the model knows uses the scene, but a scene item that a read took away did when OBS removed it. A read says.
                this.RequireCheck(scene);
                scene.ItemsScope.MarkDirty();
                outcome = RemovalOutcome.KeptAlive;
            }

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
                this.Emit(ModelChangeKind.SourceDetached, scene.Uuid, 0, scene.Name.Value);
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
                this.Emit(ModelChangeKind.Repaired, input.Uuid, 0, input.Name.Value);
            }

            var outcome = this.OutcomeOfRemoval(input.Uuid, repaired);
            if (outcome == RemovalOutcome.Destroyed && !repaired && this.HeldByRead(input.Uuid))
            {
                // See HeldByRead: nothing that the model knows uses the input, but a scene item that a read took away did when OBS removed it. A read says.
                this.RequireCheck(input);
                this._inputsScope.MarkDirty();
                outcome = RemovalOutcome.KeptAlive;
            }

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
                this.Emit(ModelChangeKind.SourceDetached, input.Uuid, 0, input.Name.Value);
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
                this.Emit(ModelChangeKind.InputAdded, input.Uuid, 0, input.Name.Value);
            }
        }

        private void TryAnnounce(ItemNode item)
        {
            if (!item.Announced && item.Known)
            {
                item.Announced = true;
                this.Emit(ModelChangeKind.ItemAdded, item.Scene.Uuid, item.Id);
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

        // Gives the scenes the positions of the list, in the order of the list: the live scenes that the list does not have first (they are newer than the list, which OBS Studio makes from its
        // user interface, and it puts a new scene first), then the scenes of the list. The scenes that the list does not have keep the order that they had.
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

            ordered.InsertRange(0, this._scenes.Values.Where(s => s.State == SourceState.Live && !s.IsGroup && !seen.Contains(s.Uuid)).OrderBy(s => s.Index.Value).ThenBy(s => s.Uuid));
            for (var i = 0; i < ordered.Count; i++)
            {
                var changed = fromEvent ? ordered[i].Index.ApplyEvent(i, seq) : ordered[i].Index.ApplyRead(i, seq);
                if (changed)
                {
                    this.Emit(ModelChangeKind.SceneOrderChanged, ordered[i].Uuid, 0, ordered[i].Name.Value);
                }
            }
        }
    }
}
