namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// One notification of a change: the record for <see cref="ObsModel.Changed"/>, the raising of a PropertyChanged event, or both; or, for something that happened and is not a
    /// state of the model (a file that was saved), the raising of an event of the model or of one of its objects.
    /// </summary>
    /// <param name="Args">The record for <see cref="ObsModel.Changed"/>, or null.</param>
    /// <param name="Entity">The object whose PropertyChanged event is raised, or null.</param>
    /// <param name="Property">The name of the property for the PropertyChanged event.</param>
    /// <param name="Raise">What raises the event of something that happened, or null.</param>
    /// <param name="Immediate">Whether the notification is told when it happens, also while the model is being read, and not held until the model is complete: it is about something that does not depend on the scene collection that is read (the file that an output wrote, that OBS Studio is exiting), and the events that go with it are told at once.</param>
    internal readonly record struct ModelNotice(ObsChangedEventArgs? Args, object? Entity, string? Property, Action? Raise = null, bool Immediate = false);

    /// <summary>
    /// Brings what the application sees (the <see cref="ObsScene"/>, <see cref="ObsInput"/> and <see cref="ObsSceneItem"/> objects and the collections
    /// of the <see cref="ObsModel"/>) in line with the engine, after every step that the model made, and says what changed. It runs on the thread that
    /// runs the model, and never calls code of the application: the notices that it returns are raised later, by the notifier.
    /// </summary>
    /// <remarks>
    /// It compares the whole model with what it published, so it does not depend on the engine having told every change. Objects that the engine
    /// has announced get an object for the application, which stays the same object for as long as the thing exists; a scene item appears when its
    /// source has appeared too.
    /// </remarks>
    internal sealed class ModelPublisher
    {
        // The most selections that wait for their scene item: a selection of an item that never appears does not hold the later ones back for ever.
        private const int MaxWaitingSelections = 1000;

        private static readonly IReadOnlyList<string> NoNames = Array.Empty<string>();

        private readonly ObsModel _model;
        private readonly bool _ignoreCase;
        private readonly Dictionary<Guid, ObsScene> _scenes = new();
        private readonly Dictionary<Guid, ObsInput> _inputs = new();
        private readonly Dictionary<ItemKey, ObsSceneItem> _items = new();
        private readonly Dictionary<long, ObsFilter> _filters = new();

        // The notices of a pass, in the order in which they are raised.
        private readonly List<ModelNotice> _repaired = new();
        private readonly List<ModelNotice> _removed = new();
        private readonly List<ModelNotice> _stateChanged = new();
        private readonly List<ModelNotice> _added = new();
        private readonly List<ModelNotice> _changed = new();
        private readonly List<ModelNotice> _notices = new();

        // What happened in the pass and is no state of the model (a file was saved): told after the notices, in the order of arrival, and also while the model is read.
        private readonly List<ModelNotice> _events = new();

        // The events of the pass that tell of something that happened, with their data, until what they are about is published.
        private readonly List<(EventType Type, object Data)> _told = new();

        // The selections of scene items that were not told yet, in the order of arrival, and the ones that are still waiting after a pass. An item that is made is selected at once, and the application is told of the
        // selection after it was told of the item, which can be some passes later: the item appears when its source does.
        private readonly List<SceneItemSelectedEventArgs> _selected = new();
        private readonly List<SceneItemSelectedEventArgs> _stillWaiting = new();

        // Reused by every pass.
        private readonly List<(int Index, ObsScene Scene)> _liveScenes = new();
        private readonly List<ObsScene> _otherScenes = new();
        private readonly List<ObsInput> _liveInputs = new();
        private readonly List<ObsInput> _keptInputs = new();
        private readonly List<ObsSceneItem> _sceneItems = new();
        private readonly List<ItemKey> _vanishedItems = new();
        private readonly List<ObsFilter> _sourceFilters = new();
        private readonly List<long> _vanishedFilters = new();
        private readonly List<Guid> _vanished = new();
        private ModelView _view;
        private int _pass;
        private bool _setOfScenesChanged;

        public ModelPublisher(ObsModel model, bool ignoreCase)
        {
            this._model = model;
            this._ignoreCase = ignoreCase;
            this._view = new ModelView(
                new ObsNamedCollection<ObsScene>("scene", [], ignoreCase),
                new ObsNamedCollection<ObsInput>("input", [], ignoreCase),
                [],
                null,
                false);
        }

        /// <summary>Gets what is published now.</summary>
        public ModelView View => this._view;

        /// <summary>Gets the notices of the last call of <see cref="Publish"/>, in the order in which they are to be raised.</summary>
        public IReadOnlyList<ModelNotice> Notices => this._notices;

        /// <summary>
        /// Gets what happened in the last call of <see cref="Publish"/> and is no state of the model (a file was saved, OBS Studio is exiting), in the order of arrival. They are raised after
        /// <see cref="Notices"/>, and unlike them they are not held back while the model is read: it happened, and the application is told.
        /// </summary>
        public IReadOnlyList<ModelNotice> Events => this._events;

        /// <summary>
        /// Publishes what the engine holds.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="changes">The changes that the engine made since the last call.</param>
        /// <returns>What is published now: the same object as before when nothing that is shown changed.</returns>
        public ModelView Publish(ModelEngine engine, IReadOnlyList<ModelChange> changes)
        {
            this._repaired.Clear();
            this._removed.Clear();
            this._stateChanged.Clear();
            this._added.Clear();
            this._changed.Clear();
            this._notices.Clear();
            this._events.Clear();
            this._setOfScenesChanged = false;
            var pass = ++this._pass;

            // Before anything is removed: the objects that the model corrected itself about are still known now.
            this._told.Clear();
            foreach (var change in changes)
            {
                if (change.Kind == ModelChangeKind.Repaired && this.FindSource(change.Subject) is { } source)
                {
                    this._repaired.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Repaired, source), null, null));
                }
                else if (change.Kind == ModelChangeKind.Notification && change.OldValue is EventType eventType && change.NewValue is { } data)
                {
                    this._told.Add((eventType, data));
                }
            }

            this.PublishSources(engine, pass);
            this.PublishItems(engine, pass);
            this.PublishFilters(engine, pass);
            this.RemoveVanished(pass);

            // What happened is told once what it is about is published: a scene item that was selected as it was made is the one that this pass published.
            this.TellWhatHappened(engine);

            var view = this.PublishView(engine, pass);
            this.PublishOutputs(engine);
            this.PublishTransitions(engine);
            this.PublishNameLists(engine);
            this.PublishExiting(engine);

            this._notices.AddRange(this._repaired);
            this._notices.AddRange(this._removed);
            this._notices.AddRange(this._stateChanged);
            this._notices.AddRange(this._added);
            this._notices.AddRange(this._changed);
            return view;
        }

        private static ObsObjectState ToState(SourceState state)
        {
            return state switch
            {
                SourceState.Live => ObsObjectState.Live,
                SourceState.Detached => ObsObjectState.Detached,
                _ => ObsObjectState.Gone,
            };
        }

        // A scene that the model knew as a live scene, and that a list of scenes did not have while no event removed it (RemoveSceneUnverified): OBS Studio can still have it. A scene that is only
        // inferred to be Detached (it is the source of a scene item and no list had it) is not one: the model never saw it live.
        private static bool IsRemovalUnverified(SceneNode node)
        {
            return node.State == SourceState.Detached && !node.RemovedAnnounced && !node.Provisional && !node.IsGroup;
        }

        private ObsSource? FindSource(Guid uuid)
        {
            if (this._scenes.TryGetValue(uuid, out var scene))
            {
                return scene;
            }

            return this._inputs.TryGetValue(uuid, out var input) ? input : null;
        }

        private void PublishSources(ModelEngine engine, int pass)
        {
            this._liveScenes.Clear();
            this._otherScenes.Clear();
            this._liveInputs.Clear();
            this._keptInputs.Clear();

            foreach (var node in engine.SceneNodes)
            {
                if (!node.Announced)
                {
                    continue;
                }

                var state = ToState(node.State);
                var unverified = IsRemovalUnverified(node);
                if (!this._scenes.TryGetValue(node.Uuid, out var scene))
                {
                    scene = new ObsScene(this._model, node.Uuid, node.Name.Value, state, node.IsGroup);
                    scene.SetRemovalUnverified(unverified);
                    this._scenes.Add(node.Uuid, scene);
                    this._setOfScenesChanged = true;
                    this._added.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Added, scene), null, null));
                }
                else
                {
                    // An application that sees the scene Detached has to see why: the flag is set before the state when the scene is taken for removed without a word of OBS Studio, and after it when it is not.
                    if (unverified)
                    {
                        scene.SetRemovalUnverified(true);
                    }

                    this.UpdateSource(scene, node.Name.Value, state);
                    if (!unverified)
                    {
                        scene.SetRemovalUnverified(false);
                    }
                }

                scene.Pass = pass;
                if (node.State == SourceState.Live && !node.IsGroup)
                {
                    this._liveScenes.Add((node.Index.Value, scene));
                }
                else
                {
                    this._otherScenes.Add(scene);
                }
            }

            foreach (var node in engine.InputNodes)
            {
                if (!node.Announced)
                {
                    continue;
                }

                var state = ToState(node.State);
                if (!this._inputs.TryGetValue(node.Uuid, out var input))
                {
                    input = new ObsInput(this._model, node.Uuid, node.Name.Value, state, node.Kind, node.HasAudio);
                    input.SetMuted(node.Muted.Value);
                    input.SetVolumeMul(node.VolumeMul.Value);
                    input.SetVolumeDb(node.VolumeDb.Value);
                    input.SetAudioBalance(node.AudioBalance.Value);
                    input.SetSyncOffset(node.SyncOffset.Value);
                    input.SetMonitorType(node.MonitorType.Value);
                    input.SetAudioTracks(node.Tracks.Value);
                    input.SetSettings(node.Settings.Value);
                    input.SetActive(node.Active.Value);
                    input.SetShowing(node.Showing.Value);
                    if (node.IsMedia && node.MediaKnown)
                    {
                        input.AttachMedia(node.MediaState.Value);
                    }

                    this._inputs.Add(node.Uuid, input);
                    this._added.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Added, input), null, null));
                }
                else
                {
                    this.UpdateSource(input, node.Name.Value, state);
                    var muted = input.IsMuted;
                    if (input.SetMuted(node.Muted.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.IsMuted), muted, node.Muted.Value, true);
                    }

                    var mul = input.VolumeMul;
                    if (input.SetVolumeMul(node.VolumeMul.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.VolumeMul), mul, node.VolumeMul.Value, true);
                    }

                    var db = input.VolumeDb;
                    if (input.SetVolumeDb(node.VolumeDb.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.VolumeDb), db, node.VolumeDb.Value, true);
                    }

                    var balance = input.AudioBalance;
                    if (input.SetAudioBalance(node.AudioBalance.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.AudioBalance), balance, node.AudioBalance.Value, true);
                    }

                    var offset = input.SyncOffset;
                    if (input.SetSyncOffset(node.SyncOffset.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.SyncOffset), offset, input.SyncOffset, true);
                    }

                    var monitor = input.MonitorType;
                    if (input.SetMonitorType(node.MonitorType.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.MonitorType), monitor, node.MonitorType.Value, true);
                    }

                    var tracks = input.AudioTracks;
                    if (input.SetAudioTracks(node.Tracks.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.AudioTracks), tracks, input.AudioTracks, true);
                    }

                    var settings = input.Settings;
                    if (input.SetSettings(node.Settings.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.Settings), settings, input.Settings, true);
                    }

                    var active = input.IsActive;
                    if (input.SetActive(node.Active.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.IsActive), active, node.Active.Value, true);
                    }

                    var showing = input.IsShowing;
                    if (input.SetShowing(node.Showing.Value))
                    {
                        this.AddProperty(input, nameof(ObsInput.IsShowing), showing, node.Showing.Value, true);
                    }

                    if (input.Media is { } media)
                    {
                        var mediaState = media.State;
                        if (media.SetState(node.MediaState.Value))
                        {
                            this.AddProperty(media, nameof(ObsMedia.State), mediaState, media.State, true);
                        }
                    }
                    else if (node.IsMedia && node.MediaKnown)
                    {
                        // The input turned out to be a media input after the model announced it (a list of inputs said so).
                        var attached = input.AttachMedia(node.MediaState.Value);
                        this.AddProperty(input, nameof(ObsInput.Media), null, attached, true);
                    }
                }

                input.Pass = pass;
                if (node.State == SourceState.Live)
                {
                    this._liveInputs.Add(input);
                }
                else if (node.State == SourceState.Detached)
                {
                    this._keptInputs.Add(input);
                }
            }
        }

        private void UpdateSource(ObsSource source, string name, ObsObjectState state)
        {
            var oldName = source.Name;
            if (source.SetName(name))
            {
                this.AddProperty(source, nameof(ObsSource.Name), oldName, name, true);
            }

            var oldState = source.State;
            if (source.SetState(state))
            {
                this._stateChanged.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.StateChanged, source, nameof(ObsSource.State), oldState, state), null, null));
                this._stateChanged.Add(new ModelNotice(null, source, nameof(ObsSource.State)));
                this._stateChanged.Add(new ModelNotice(null, source, nameof(ObsSource.IsRemoved)));
            }
        }

        private void PublishItems(ModelEngine engine, int pass)
        {
            foreach (var node in engine.SceneNodes)
            {
                if (!node.Announced || !this._scenes.TryGetValue(node.Uuid, out var scene))
                {
                    continue;
                }

                this._sceneItems.Clear();
                foreach (var itemNode in node.Items.Values)
                {
                    if (!itemNode.Announced)
                    {
                        continue;
                    }

                    var key = itemNode.Key;
                    if (!this._items.TryGetValue(key, out var item))
                    {
                        // The item appears when its source has: the application can always get to the source of an item.
                        ObsSource? source = this.FindSource(itemNode.SourceUuid);
                        if (source is null || source.Pass != pass)
                        {
                            continue;
                        }

                        item = new ObsSceneItem(scene, source, itemNode.Id, itemNode.Index.Value, itemNode.Enabled.Value, itemNode.Locked.Value, itemNode.Transform.Value);
                        this._items.Add(key, item);
                        this._added.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Added, item), null, null));
                    }
                    else
                    {
                        var index = item.Index;
                        if (item.SetIndex(itemNode.Index.Value))
                        {
                            this.AddProperty(item, nameof(ObsSceneItem.Index), index, itemNode.Index.Value, false);
                        }

                        var enabled = item.IsEnabled;
                        if (item.SetEnabled(itemNode.Enabled.Value))
                        {
                            this.AddProperty(item, nameof(ObsSceneItem.IsEnabled), enabled, itemNode.Enabled.Value, true);
                        }

                        var locked = item.IsLocked;
                        if (item.SetLocked(itemNode.Locked.Value))
                        {
                            this.AddProperty(item, nameof(ObsSceneItem.IsLocked), locked, itemNode.Locked.Value, true);
                        }

                        var transform = item.Transform;
                        if (item.SetTransform(itemNode.Transform.Value))
                        {
                            this.AddProperty(item, nameof(ObsSceneItem.Transform), transform, itemNode.Transform.Value, true);
                        }
                    }

                    item.Pass = pass;
                    this._sceneItems.Add(item);
                }

                this._sceneItems.Sort(static (a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Id.CompareTo(b.Id));
                var items = this._sceneItems.ToArray();
                if (!scene.Items.HoldsExactly(items))
                {
                    var before = scene.Items;
                    var after = new ObsSceneItems(scene, items, this._ignoreCase);
                    scene.SetItems(after);
                    this.AddProperty(scene, nameof(ObsScene.Items), before, after, true);
                }
            }
        }

        // The filters of the scenes and the inputs that are published. A filter appears with its source, and one that the model no longer holds is gone.
        private void PublishFilters(ModelEngine engine, int pass)
        {
            foreach (var node in engine.SceneNodes)
            {
                if (node.Announced && this._scenes.TryGetValue(node.Uuid, out var scene))
                {
                    this.PublishFiltersOf(node, scene, pass);
                }
            }

            foreach (var node in engine.InputNodes)
            {
                if (node.Announced && this._inputs.TryGetValue(node.Uuid, out var input))
                {
                    this.PublishFiltersOf(node, input, pass);
                }
            }
        }

        private void PublishFiltersOf(SourceNode node, ObsSource source, int pass)
        {
            this._sourceFilters.Clear();
            foreach (var filterNode in node.Filters)
            {
                if (!this._filters.TryGetValue(filterNode.Id, out var filter))
                {
                    filter = new ObsFilter(source, filterNode.Name.Value, filterNode.Kind, filterNode.Index.Value, filterNode.Enabled.Value);
                    this._filters.Add(filterNode.Id, filter);
                    this._added.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Added, filter), null, null));
                }
                else
                {
                    var name = filter.Name;
                    if (filter.SetName(filterNode.Name.Value))
                    {
                        this.AddProperty(filter, nameof(ObsFilter.Name), name, filterNode.Name.Value, true);
                    }

                    var index = filter.Index;
                    if (filter.SetIndex(filterNode.Index.Value))
                    {
                        this.AddProperty(filter, nameof(ObsFilter.Index), index, filterNode.Index.Value, false);
                    }

                    var enabled = filter.IsEnabled;
                    if (filter.SetEnabled(filterNode.Enabled.Value))
                    {
                        this.AddProperty(filter, nameof(ObsFilter.IsEnabled), enabled, filterNode.Enabled.Value, true);
                    }
                }

                filter.Pass = pass;
                this._sourceFilters.Add(filter);
            }

            this._sourceFilters.Sort(static (a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : string.CompareOrdinal(a.Name, b.Name));
            var filters = this._sourceFilters.ToArray();
            if (!source.Filters.HoldsExactly(filters))
            {
                var before = source.Filters;
                var after = new ObsFilters(source, filters, this._ignoreCase);
                source.SetFilters(after);
                this.AddProperty(source, nameof(ObsSource.Filters), before, after, true);
            }
        }

        // What the model no longer holds is gone: items first, then the filters, then the inputs, then the scenes, so that an object is removed after what it contained.
        private void RemoveVanished(int pass)
        {
            this._vanishedItems.Clear();
            foreach (var pair in this._items)
            {
                if (pair.Value.Pass != pass)
                {
                    this._vanishedItems.Add(pair.Key);
                }
            }

            foreach (var key in this._vanishedItems)
            {
                var item = this._items[key];
                this._items.Remove(key);
                if (item.SetRemoved())
                {
                    this._removed.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Removed, item), null, null));
                    this._removed.Add(new ModelNotice(null, item, nameof(ObsSceneItem.IsRemoved)));
                }
            }

            this._vanishedFilters.Clear();
            foreach (var pair in this._filters)
            {
                if (pair.Value.Pass != pass)
                {
                    this._vanishedFilters.Add(pair.Key);
                }
            }

            foreach (var id in this._vanishedFilters)
            {
                var filter = this._filters[id];
                this._filters.Remove(id);
                if (filter.SetRemoved())
                {
                    this._removed.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Removed, filter), null, null));
                    this._removed.Add(new ModelNotice(null, filter, nameof(ObsFilter.IsRemoved)));
                }
            }

            this._vanished.Clear();
            foreach (var pair in this._inputs)
            {
                if (pair.Value.Pass != pass)
                {
                    this._vanished.Add(pair.Key);
                }
            }

            foreach (var uuid in this._vanished)
            {
                var input = this._inputs[uuid];
                this._inputs.Remove(uuid);
                this.MarkGone(input);
            }

            this._vanished.Clear();
            foreach (var pair in this._scenes)
            {
                if (pair.Value.Pass != pass)
                {
                    this._vanished.Add(pair.Key);
                }
            }

            foreach (var uuid in this._vanished)
            {
                var scene = this._scenes[uuid];
                this._scenes.Remove(uuid);
                this._setOfScenesChanged = true;
                var index = scene.Index;
                if (scene.SetIndex(-1))
                {
                    this.AddProperty(scene, nameof(ObsScene.Index), index, -1, false);
                }

                // The scene items of a scene that OBS destroyed went with it (they were removed above), and its engine node is gone, so nothing publishes its
                // items any more: what a reference to the scene shows is no scene items. A collection that was taken earlier is not changed.
                if (scene.Items.Count > 0)
                {
                    var before = scene.Items;
                    var after = new ObsSceneItems(scene, [], this._ignoreCase);
                    scene.SetItems(after);
                    this.AddProperty(scene, nameof(ObsScene.Items), before, after, true);
                }

                this.MarkGone(scene);
            }
        }

        private void MarkGone(ObsSource source)
        {
            // The filters of a source that OBS destroyed went with it (they were removed above), and its engine node is gone, so nothing publishes them any more: what a reference to the source
            // shows is no filters. A collection that was taken earlier is not changed.
            if (source.Filters.Count > 0)
            {
                var before = source.Filters;
                var after = new ObsFilters(source, [], this._ignoreCase);
                source.SetFilters(after);
                this.AddProperty(source, nameof(ObsSource.Filters), before, after, true);
            }

            if (source.SetState(ObsObjectState.Gone))
            {
                this._removed.Add(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Removed, source), null, null));
                this._removed.Add(new ModelNotice(null, source, nameof(ObsSource.State)));
                this._removed.Add(new ModelNotice(null, source, nameof(ObsSource.IsRemoved)));
            }
        }

        private ModelView PublishView(ModelEngine engine, int pass)
        {
            var old = this._view;

            // The scenes in the order of the list of OBS Studio; the position of a scene is its index.
            this._liveScenes.Sort(static (a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : a.Scene.Uuid.CompareTo(b.Scene.Uuid));
            var orderedScenes = new ObsScene[this._liveScenes.Count];
            for (var i = 0; i < orderedScenes.Length; i++)
            {
                var scene = this._liveScenes[i].Scene;
                orderedScenes[i] = scene;
                var index = scene.Index;
                if (scene.SetIndex(i))
                {
                    this.AddProperty(scene, nameof(ObsScene.Index), index, i, false);
                }
            }

            foreach (var scene in this._otherScenes)
            {
                var index = scene.Index;
                if (scene.SetIndex(-1))
                {
                    this.AddProperty(scene, nameof(ObsScene.Index), index, -1, false);
                }
            }

            var scenes = old.Scenes;
            var scenesChanged = !scenes.HoldsExactly(orderedScenes);
            if (scenesChanged)
            {
                scenes = new ObsNamedCollection<ObsScene>("scene", orderedScenes, this._ignoreCase);
                this.AddProperty(this._model, nameof(ObsModel.Scenes), old.Scenes, scenes, true);
            }

            // The inputs by name, so that a list of them reads well.
            this._liveInputs.Sort(static (a, b) =>
            {
                var byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (byName == 0)
                {
                    byName = string.CompareOrdinal(a.Name, b.Name);
                }

                return byName != 0 ? byName : a.Uuid.CompareTo(b.Uuid);
            });
            var orderedInputs = this._liveInputs.ToArray();
            var inputs = old.Inputs;
            var inputsChanged = !inputs.HoldsExactly(orderedInputs);
            if (inputsChanged)
            {
                inputs = new ObsNamedCollection<ObsInput>("input", orderedInputs, this._ignoreCase);
                this.AddProperty(this._model, nameof(ObsModel.Inputs), old.Inputs, inputs, true);
            }

            var allScenes = old.AllScenes;
            if (scenesChanged || this._setOfScenesChanged)
            {
                this._otherScenes.Sort(static (a, b) => a.Uuid.CompareTo(b.Uuid));
                allScenes = new ObsScene[orderedScenes.Length + this._otherScenes.Count];
                orderedScenes.CopyTo(allScenes, 0);
                this._otherScenes.CopyTo(allScenes, orderedScenes.Length);
            }

            // Every input that OBS Studio still has: the live ones, and the ones that were removed and are kept alive for a scene item that shows them. OBS Studio goes on sending the levels of
            // such an input for as long as it is on the air, so what looks inputs up by UUID needs them too. It is made again only when the set is another one.
            this._keptInputs.Sort(static (a, b) => a.Uuid.CompareTo(b.Uuid));
            var allInputs = old.AllInputs;
            var allInputsChanged = !HoldsInputs(allInputs, orderedInputs, this._keptInputs);
            if (allInputsChanged)
            {
                allInputs = new ObsInput[orderedInputs.Length + this._keptInputs.Count];
                orderedInputs.CopyTo(allInputs, 0);
                this._keptInputs.CopyTo(allInputs, orderedInputs.Length);
            }

            // A program scene that the model has heard of but that is not published yet (its event came first) is shown when it is.
            var program = old.ProgramScene;
            if (engine.ProgramSceneUuid is { } programUuid)
            {
                if (this._scenes.TryGetValue(programUuid, out var wanted) && wanted.Pass == pass)
                {
                    program = wanted;
                }
            }
            else
            {
                program = null;
            }

            if (!ReferenceEquals(program, old.ProgramScene))
            {
                this.AddProperty(this._model, nameof(ObsModel.ProgramScene), old.ProgramScene, program, true);
                if (old.ProgramScene is { } previous)
                {
                    this._changed.Add(new ModelNotice(null, previous, nameof(ObsScene.IsProgram)));
                }

                if (program is not null)
                {
                    this._changed.Add(new ModelNotice(null, program, nameof(ObsScene.IsProgram)));
                }
            }

            var studioMode = engine.StudioModeEnabled;
            if (studioMode != old.StudioMode)
            {
                this.AddProperty(this._model, nameof(ObsModel.IsStudioModeEnabled), old.StudioMode, studioMode, true);
            }

            // The preview scene, as the program scene: shown when it is published.
            var preview = old.PreviewScene;
            if (engine.PreviewSceneUuid is { } previewUuid)
            {
                if (this._scenes.TryGetValue(previewUuid, out var wantedPreview) && wantedPreview.Pass == pass)
                {
                    preview = wantedPreview;
                }
            }
            else
            {
                preview = null;
            }

            if (!ReferenceEquals(preview, old.PreviewScene))
            {
                this.AddProperty(this._model, nameof(ObsModel.PreviewScene), old.PreviewScene, preview, true);
                if (old.PreviewScene is { } previousPreview)
                {
                    this._changed.Add(new ModelNotice(null, previousPreview, nameof(ObsScene.IsPreview)));
                }

                if (preview is not null)
                {
                    this._changed.Add(new ModelNotice(null, preview, nameof(ObsScene.IsPreview)));
                }
            }

            if (!scenesChanged && !inputsChanged && !allInputsChanged && !this._setOfScenesChanged && ReferenceEquals(program, old.ProgramScene) && studioMode == old.StudioMode && ReferenceEquals(preview, old.PreviewScene))
            {
                return old;
            }

            this._view = new ModelView(scenes, inputs, allScenes, program, studioMode, preview, allInputs);
            return this._view;
        }

        // Whether an array holds exactly the inputs of two lists, one after the other and the same objects.
        private static bool HoldsInputs(ObsInput[] held, ObsInput[] first, List<ObsInput> second)
        {
            if (held.Length != first.Length + second.Count)
            {
                return false;
            }

            for (var i = 0; i < first.Length; i++)
            {
                if (!ReferenceEquals(held[i], first[i]))
                {
                    return false;
                }
            }

            for (var i = 0; i < second.Count; i++)
            {
                if (!ReferenceEquals(held[first.Length + i], second[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // The transitions are an object of the model that stays the same, so it is brought in line here and not through the view: a change is the PropertyChanged event of the object, and a record for
        // ObsModel.Changed.
        private void PublishTransitions(ModelEngine engine)
        {
            var transitions = this._model.Transitions;
            if (transitions.SetCurrent(engine.Transition, out var before))
            {
                this.AddProperty(transitions, nameof(ObsTransitions.Current), before, transitions.Current, true);
            }

            if (transitions.SetDuration(engine.TransitionDuration, out var durationBefore))
            {
                this.AddProperty(transitions, nameof(ObsTransitions.Duration), durationBefore, transitions.Duration, true);
            }

            if (transitions.SetRunning(engine.TransitionRunning, out var runningBefore))
            {
                this.AddProperty(transitions, nameof(ObsTransitions.IsRunning), runningBefore, engine.TransitionRunning, true);
            }

            transitions.SetStarts(engine.TransitionStarts);
        }

        // The profiles and the scene collections are objects of the model that stay the same, like the transitions: a change of the names is the PropertyChanged event of the object (Names, and then Current, so that
        // a profile that was made is in the names by the time that it is the current one), and a record for ObsModel.Changed.
        private void PublishNameLists(ModelEngine engine)
        {
            var profiles = this._model.Profiles;
            if (profiles.Set(engine.Profiles, out var profilesBefore))
            {
                this.AddNameChanges(profiles, profilesBefore, engine.Profiles, nameof(ObsProfiles.Names), nameof(ObsProfiles.Current));
            }

            var collections = this._model.SceneCollections;
            if (collections.Set(engine.SceneCollections, out var collectionsBefore))
            {
                this.AddNameChanges(collections, collectionsBefore, engine.SceneCollections, nameof(ObsSceneCollections.Names), nameof(ObsSceneCollections.Current));
            }
        }

        private void AddNameChanges(object entity, NameList? before, NameList? after, string namesProperty, string currentProperty)
        {
            var oldNames = before?.Names ?? NoNames;
            var newNames = after?.Names ?? NoNames;
            if (!oldNames.SequenceEqual(newNames, StringComparer.Ordinal))
            {
                this.AddProperty(entity, namesProperty, oldNames, newNames, true);
            }

            if (!string.Equals(before?.Current, after?.Current, StringComparison.Ordinal))
            {
                this.AddProperty(entity, currentProperty, before?.Current, after?.Current, true);
            }
        }

        // The outputs are objects of the model that stay the same, so they are brought in line here and not through the view: a change of the status is the PropertyChanged event of the output
        // (and of IsActive, and of IsPaused for the recording), and a record for ObsModel.Changed.
        private void PublishOutputs(ModelEngine engine)
        {
            foreach (var output in this._model.Outputs)
            {
                var node = engine.Output(output.Kind);
                this.PublishFile(output, node);
                var wasActive = output.IsActive;
                if (output.Publish(node.Status.Value, node.StartsSeen, node.StopsSeen, out var previous))
                {
                    var status = output.Status;
                    this.AddProperty(output, nameof(ObsOutput.Status), previous, status, true);
                    if (output.IsActive != wasActive)
                    {
                        this.AddProperty(output, nameof(ObsOutput.IsActive), wasActive, output.IsActive, false);
                    }

                    if (output is ObsRecordOutput && (previous == ObsOutputStatus.Paused) != (status == ObsOutputStatus.Paused))
                    {
                        this.AddProperty(output, nameof(ObsRecordOutput.IsPaused), previous == ObsOutputStatus.Paused, status == ObsOutputStatus.Paused, false);
                    }
                }
            }
        }

        // OBS Studio said that it is exiting (or a connection to another one was made): ObsModel.IsExiting, and a record for ObsModel.Changed.
        private void PublishExiting(ModelEngine engine)
        {
            var exiting = engine.Exiting;
            if (this._model.SetExiting(exiting, out var before))
            {
                this.AddProperty(this._model, nameof(ObsModel.IsExiting), before, exiting, true, immediate: true);
            }
        }

        // The file that an output last announced (the file of the recording, the replay that was saved): the property of the output that writes files, and a record for ObsModel.Changed.
        private void PublishFile(ObsOutput output, OutputNode node)
        {
            var file = node.LastFile.Value;
            switch (output)
            {
                case ObsRecordOutput record when record.SetCurrentFile(file, out var recordBefore):
                    this.AddProperty(record, nameof(ObsRecordOutput.CurrentFile), recordBefore, file, true, immediate: true);
                    break;
                case ObsReplayBufferOutput replay when replay.SetLastSavedPath(file, out var replayBefore):
                    this.AddProperty(replay, nameof(ObsReplayBufferOutput.LastSavedPath), replayBefore, file, true, immediate: true);
                    break;
            }
        }

        // Tells what happened in the pass, in the order of arrival: first the selections that waited from the passes before, which are older than anything in this one. A selection waits when its scene item is
        // in the model and the application has not been told of it yet (it appears when its source does), and the selections after it wait with it, so that they are told in order. A selection of an item that
        // is gone (it was removed, or the scene collection was left) is not told.
        private void TellWhatHappened(ModelEngine engine)
        {
            this._stillWaiting.Clear();
            var held = false;
            foreach (var selected in this._selected)
            {
                this.TellSelection(engine, selected, ref held);
            }

            foreach (var (eventType, data) in this._told)
            {
                if (eventType == EventType.SceneItemSelected && data is SceneItemSelectedEventArgs selected)
                {
                    this.TellSelection(engine, selected, ref held);
                }
                else
                {
                    this._events.Add(new ModelNotice(null, null, null, this.RaiseOf(eventType, data)));
                }
            }

            this._selected.Clear();
            for (var i = Math.Max(0, this._stillWaiting.Count - MaxWaitingSelections); i < this._stillWaiting.Count; i++)
            {
                this._selected.Add(this._stillWaiting[i]);
            }
        }

        private void TellSelection(ModelEngine engine, SceneItemSelectedEventArgs selected, ref bool held)
        {
            if (!held && this._items.TryGetValue(new ItemKey(selected.SceneUuid, selected.SceneItemId), out var item))
            {
                var model = this._model;
                this._events.Add(new ModelNotice(null, null, null, () => model.RaiseSceneItemSelected(new ObsSceneItemEventArgs(item))));
            }
            else if (engine.HasItem(selected.SceneUuid, selected.SceneItemId))
            {
                held = true;
                this._stillWaiting.Add(selected);
            }
        }

        // What tells the application of something that happened: the event of the model, or of the output that it is about. It is raised later, by the notifier.
        private Action RaiseOf(EventType eventType, object data)
        {
            var model = this._model;
            return eventType switch
            {
                EventType.CustomEvent when data is CustomEventEventArgs custom => () => model.RaiseCustomEvent(custom),
                EventType.VendorEvent when data is VendorEventEventArgs vendor => () => model.RaiseVendorEvent(vendor),
                EventType.ScreenshotSaved when data is ScreenshotSavedEventArgs screenshot => () => model.RaiseScreenshotSaved(screenshot),
                EventType.ExitStarted => model.RaiseExitStarted,
                EventType.RecordFileChanged when data is RecordFileChangedEventArgs file => () => model.Outputs.Record.RaiseFileChanged(file),
                EventType.ReplayBufferSaved when data is ReplayBufferSavedEventArgs replay => () => model.Outputs.ReplayBuffer.RaiseSaved(replay),
                EventType.SceneTransitionStarted when data is TransitionNameEventArgs started => () => model.Transitions.RaiseStarted(started),
                EventType.SceneTransitionEnded when data is TransitionNameEventArgs ended => () => model.Transitions.RaiseEnded(ended),
                EventType.SceneTransitionVideoEnded when data is TransitionNameEventArgs videoEnded => () => model.Transitions.RaiseVideoEnded(videoEnded),
                _ => static () => { },
            };
        }

        // A change of a property: the PropertyChanged event of the object, and when the change is one that the application is told about in
        // ObsModel.Changed, the record. (A change of the position of an object is not: the record of the list that it is in says that.) An immediate change is told when it happens,
        // also while the model is being read (see ModelNotice).
        private void AddProperty(object entity, string property, object? oldValue, object? newValue, bool record, bool immediate = false)
        {
            ObsChangedEventArgs? args = null;
            if (record)
            {
                args = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, ReferenceEquals(entity, this._model) ? null : entity, property, oldValue, newValue);
            }

            this._changed.Add(new ModelNotice(args, entity, property, null, immediate));
        }
    }
}
