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
        // more is destroyed by OBS when it was removed. The indexes of the other items are the business of the caller.
        private void ReleaseItem(ItemNode item)
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

            this.OnReferencesChanged(item.SourceUuid);
        }

        // Called when a scene item that used a source is gone.
        private void OnReferencesChanged(Guid sourceUuid)
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
                    this.DestroyInput(input);
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
                this.DestroyScene(scene);
            }
        }

        private void DestroyInput(InputNode input)
        {
            if (input.State == SourceState.Gone)
            {
                return;
            }

            input.State = SourceState.Gone;
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
                    && other.Index.ApplyEvent(other.Index.Value - 1, this._seq))
                {
                    this.Emit(StudioChangeKind.SceneOrderChanged, other.Uuid, 0, other.Name.Value);
                }
            }
        }

        // A scene that OBS removed, because an event told so, or because a read showed that it does not exist (repaired).
        private void RemoveScene(SceneNode scene, bool repaired)
        {
            if (scene.State != SourceState.Live)
            {
                return;
            }

            if (repaired)
            {
                this.Emit(StudioChangeKind.Repaired, scene.Uuid, 0, scene.Name.Value);
            }

            if (this.RefCount(scene.Uuid) > 0)
            {
                // Nested in another scene: OBS keeps it alive, with everything in it.
                scene.State = SourceState.Detached;
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
            else
            {
                this.DestroyScene(scene);
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

            if (this.RefCount(input.Uuid) > 0)
            {
                // Scene items still use it: OBS keeps it alive, and so does the model.
                input.State = SourceState.Detached;
                if (input.Announced)
                {
                    this.Emit(StudioChangeKind.SourceDetached, input.Uuid, 0, input.Name.Value);
                }
            }
            else
            {
                this.DestroyInput(input);
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
            if (!input.Announced && input.State != SourceState.Gone && input.IsComplete)
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
