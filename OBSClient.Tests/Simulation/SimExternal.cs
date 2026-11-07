namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>
    /// The changes of someone else: the user interface of OBS Studio, or another websocket client. Each is made at <see cref="SimObs.Clock"/>,
    /// announces itself with the same events and the same latencies as a request does, and is not answered. It is the same change as the
    /// request of the same name: it gives the same events and has the same refusals, which are thrown as <see cref="InvalidOperationException"/>
    /// instead of being answered with a status.
    /// </summary>
    internal sealed class SimExternal
    {
        private readonly SimObs _obs;
        private SimWorld _world;

        internal SimExternal(SimObs obs, SimWorld world)
        {
            _obs = obs;
            _world = world;
        }

        /// <summary>Gets the names of the scene collections, the current one included, in alphabetical order.</summary>
        public IReadOnlyList<string> Collections => _obs.Collections;

        /// <summary>Makes the changes work on another scene collection: the one that OBS Studio has loaded now.</summary>
        internal void Use(SimWorld world)
        {
            _world = world;
        }

        /// <summary>
        /// Switches to another scene collection, as the user does in the menu. It announces <c>CurrentSceneCollectionChanging</c> now, and
        /// <c>CurrentSceneCollectionChanged</c> when the switch is done (<see cref="SimQuirks.CollectionSwitchTime"/>); in between nothing can be changed
        /// and every request is answered with 207. Switching to the collection that is current does nothing.
        /// </summary>
        public void SwitchCollection(string name)
        {
            _obs.RunExternal(() =>
            {
                RequireName(name);
                if (!_obs.Collections.Contains(name))
                {
                    throw SimFailure.NotFound($"There is no scene collection named \"{name}\".");
                }

                if (!string.Equals(name, _obs.CurrentCollection, StringComparison.Ordinal))
                {
                    _obs.BeginSwitch(name, create: false);
                }

                return 0;
            });
        }

        /// <summary>Makes a new scene collection and switches to it, as the user does in the menu: it has one scene. It is announced like any other switch.</summary>
        public void CreateCollection(string name)
        {
            _obs.RunExternal(() =>
            {
                RequireName(name);
                if (_obs.Collections.Contains(name))
                {
                    throw SimFailure.Exists($"A scene collection named \"{name}\" exists already.");
                }

                _obs.BeginSwitch(name, create: true);
                return 0;
            });
        }

        /// <summary>Creates a scene, which is first in the list of scenes.</summary>
        public Guid CreateScene(string name)
        {
            return _obs.RunExternal(() =>
            {
                RequireName(name);
                return _world.CreateScene(name).Uuid;
            });
        }

        /// <summary>Removes a scene. A removed scene that something refers to stays alive, detached from the list of scenes.</summary>
        public void RemoveScene(Guid sceneUuid)
        {
            _obs.RunExternal(() =>
            {
                _world.RemoveScene(FindScene(sceneUuid));
                return 0;
            });
        }

        /// <summary>Renames a scene.</summary>
        public void RenameScene(Guid sceneUuid, string newName)
        {
            _obs.RunExternal(() =>
            {
                RequireName(newName);
                _world.RenameScene(FindScene(sceneUuid), newName);
                return 0;
            });
        }

        /// <summary>Creates an input, with an item for it in a scene.</summary>
        public (Guid InputUuid, int ItemId) CreateInput(Guid sceneUuid, string name, string kind)
        {
            return _obs.RunExternal(() =>
            {
                RequireName(name);
                var scene = FindScene(sceneUuid);
                var inputKind = FindKind(kind);
                var input = _world.CreateInput(scene, name, inputKind, null, true, out var item);
                return (input.Uuid, item.Id);
            });
        }

        /// <summary>Creates an input that is pinned and has no scene item, as the global audio devices of OBS Studio are (Desktop Audio).</summary>
        public Guid CreateGlobalInput(string name, string kind)
        {
            return _obs.RunExternal(() =>
            {
                RequireName(name);
                return _world.CreateGlobalInput(name, FindKind(kind)).Uuid;
            });
        }

        /// <summary>Lets go of the pin of an input, as the settings of OBS Studio do when a global audio device is set to disabled.</summary>
        public void DisableGlobalInput(Guid inputUuid)
        {
            _obs.RunExternal(() =>
            {
                _world.ReleasePin(FindInput(inputUuid));
                return 0;
            });
        }

        /// <summary>Removes an input. It stays alive, and in the list of inputs, while a scene item or a pin refers to it.</summary>
        public void RemoveInput(Guid inputUuid)
        {
            _obs.RunExternal(() =>
            {
                _world.RemoveInput(FindInput(inputUuid));
                return 0;
            });
        }

        /// <summary>Renames an input.</summary>
        public void RenameInput(Guid inputUuid, string newName)
        {
            _obs.RunExternal(() =>
            {
                RequireName(newName);
                _world.RenameInput(FindInput(inputUuid), newName);
                return 0;
            });
        }

        /// <summary>Puts a source (an input or a scene) in a scene, on top, and returns the ID of the new item.</summary>
        public int AddItem(Guid sceneUuid, Guid sourceUuid)
        {
            return _obs.RunExternal(() => _world.AddItem(FindScene(sceneUuid), FindSource(sourceUuid), true).Id);
        }

        /// <summary>Removes a scene item.</summary>
        public void RemoveItem(Guid sceneUuid, int itemId)
        {
            _obs.RunExternal(() =>
            {
                _world.RemoveItem(FindItem(sceneUuid, itemId));
                return 0;
            });
        }

        /// <summary>Enables or disables a scene item.</summary>
        public void SetItemEnabled(Guid sceneUuid, int itemId, bool enabled)
        {
            _obs.RunExternal(() =>
            {
                _world.SetItemEnabled(FindItem(sceneUuid, itemId), enabled);
                return 0;
            });
        }

        /// <summary>Locks or unlocks a scene item.</summary>
        public void SetItemLocked(Guid sceneUuid, int itemId, bool locked)
        {
            _obs.RunExternal(() =>
            {
                _world.SetItemLocked(FindItem(sceneUuid, itemId), locked);
                return 0;
            });
        }

        /// <summary>Moves a scene item to an index.</summary>
        public void SetItemIndex(Guid sceneUuid, int itemId, int index)
        {
            _obs.RunExternal(() =>
            {
                if (index is < 0 or > 8192)
                {
                    throw new SimFailure(SimStatus.RequestFieldOutOfRange, "The index of a scene item has to be from 0 to 8192.");
                }

                _world.SetItemIndex(FindItem(sceneUuid, itemId), index);
                return 0;
            });
        }

        /// <summary>Mutes or unmutes an input.</summary>
        public void SetMute(Guid inputUuid, bool muted)
        {
            _obs.RunExternal(() =>
            {
                _world.SetMuted(FindInput(inputUuid), muted);
                return 0;
            });
        }

        /// <summary>Sets the volume of an input, in dB (from -100 to 26, as obs-websocket allows).</summary>
        public void SetVolumeDb(Guid inputUuid, double db)
        {
            _obs.RunExternal(() =>
            {
                if (!(db is >= -100 and <= 26))
                {
                    throw new SimFailure(SimStatus.RequestFieldOutOfRange, "The volume has to be from -100 to 26 dB.");
                }

                _world.SetVolume(FindInput(inputUuid), SimWorld.DbToMul(db));
                return 0;
            });
        }

        /// <summary>Switches the program scene, with a transition if there is one.</summary>
        public void SwitchProgramScene(Guid sceneUuid)
        {
            _obs.RunExternal(() =>
            {
                _world.SwitchProgram(FindScene(sceneUuid));
                return 0;
            });
        }

        private static void RequireName(string name)
        {
            // The user interface of OBS Studio does not accept an empty name, and obs-websocket answers 403.
            if (name.Length == 0)
            {
                throw new SimFailure(SimStatus.RequestFieldEmpty, "A name cannot be empty.");
            }
        }

        private static SimInputKind FindKind(string kind)
        {
            return SimInputKinds.Find(kind) ?? throw new SimFailure(SimStatus.InvalidInputKind, $"The simulation does not have the input kind `{kind}`.");
        }

        private SimSource FindSource(Guid uuid)
        {
            return _world.FindByUuid(uuid.ToString("D")) ?? throw SimFailure.NotFound($"No source was found by the UUID of `{uuid:D}`.");
        }

        private SimScene FindScene(Guid uuid)
        {
            return FindSource(uuid) as SimScene ?? throw new SimFailure(SimStatus.InvalidResourceType, $"The source `{uuid:D}` is not a scene.");
        }

        private SimInput FindInput(Guid uuid)
        {
            return FindSource(uuid) as SimInput ?? throw new SimFailure(SimStatus.InvalidResourceType, $"The source `{uuid:D}` is not an input.");
        }

        private SimItem FindItem(Guid sceneUuid, int itemId)
        {
            var scene = FindScene(sceneUuid);
            return scene.Items.FirstOrDefault(item => item.Id == itemId)
                ?? throw SimFailure.NotFound($"No scene items were found in scene `{scene.Name}` with the ID `{itemId}`.");
        }
    }
}
