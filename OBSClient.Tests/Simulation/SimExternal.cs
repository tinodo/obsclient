namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;

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

        /// <summary>Gets the names of the profiles, the current one included, in the order in which OBS Studio lists them.</summary>
        public IReadOnlyList<string> Profiles => _obs.Profiles;

        /// <summary>Makes the changes work on another scene collection: the one that OBS Studio has loaded now.</summary>
        internal void Use(SimWorld world)
        {
            _world = world;
        }

        /// <summary>
        /// Switches to another profile, as the user does in the menu: <c>CurrentProfileChanging</c> and <c>CurrentProfileChanged</c> come together. Switching to the profile that is current does nothing.
        /// </summary>
        public void SwitchProfile(string name)
        {
            _obs.RunExternal(() =>
            {
                RequireName(name);
                if (!_obs.Profiles.Contains(name))
                {
                    throw SimFailure.NotFound($"There is no profile named \"{name}\".");
                }

                if (!string.Equals(name, _obs.CurrentProfile, StringComparison.Ordinal))
                {
                    _obs.SwitchProfile(name);
                }

                return 0;
            });
        }

        /// <summary>
        /// Makes a new profile and switches to it, as the user does in the menu: <c>ProfileListChanged</c>, and a moment later <c>CurrentProfileChanged</c>.
        /// </summary>
        public void CreateProfile(string name)
        {
            _obs.RunExternal(() =>
            {
                RequireName(name);
                if (_obs.Profiles.Contains(name))
                {
                    throw SimFailure.Exists($"A profile named \"{name}\" exists already.");
                }

                _obs.MakeProfile(name);
                return 0;
            });
        }

        /// <summary>
        /// Removes a profile, as the user does in the menu. For the profile that is current OBS Studio asks first, and the user says yes: it switches to the profile that comes first by the bytes of
        /// its name, and does not say that the list changed. It refuses to remove the only profile.
        /// </summary>
        public void RemoveProfile(string name)
        {
            _obs.RunExternal(() =>
            {
                RequireName(name);
                if (!_obs.Profiles.Contains(name))
                {
                    throw SimFailure.NotFound($"There is no profile named \"{name}\".");
                }

                if (_obs.Profiles.Count < 2)
                {
                    throw new SimFailure(SimStatus.NotEnoughResources, "OBS Studio does not remove the only profile.");
                }

                if (string.Equals(name, _obs.CurrentProfile, StringComparison.Ordinal))
                {
                    _obs.DropCurrentProfile();
                }
                else
                {
                    _obs.DropProfile(name);
                }

                return 0;
            });
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

        /// <summary>Changes the transform of a scene item, as the user does when it moves or resizes the item in the preview.</summary>
        public void SetItemTransform(Guid sceneUuid, int itemId, SimTransformUpdate update)
        {
            _obs.RunExternal(() =>
            {
                _world.SetItemTransform(FindItem(sceneUuid, itemId), update);
                return 0;
            });
        }

        /// <summary>Sets the blend mode of a scene item.</summary>
        public void SetItemBlendMode(Guid sceneUuid, int itemId, string mode)
        {
            _obs.RunExternal(() =>
            {
                _world.SetItemBlendMode(FindItem(sceneUuid, itemId), mode);
                return 0;
            });
        }

        /// <summary>Makes a copy of a scene item on top of a scene (the scene of the item when it is not given), and returns the ID of the copy.</summary>
        public int DuplicateItem(Guid sceneUuid, int itemId, Guid? destinationUuid = null)
        {
            return _obs.RunExternal(() =>
            {
                var item = FindItem(sceneUuid, itemId);
                return _world.DuplicateItem(item, destinationUuid is { } destination ? FindScene(destination) : item.Scene).Id;
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

        /// <summary>Changes the settings of an input, as its properties window does: the given fields are laid over the ones that it has.</summary>
        public void SetSettings(Guid inputUuid, JsonObject given)
        {
            _obs.RunExternal(() =>
            {
                _world.SetSettings(FindInput(inputUuid), given, overlay: true);
                return 0;
            });
        }

        /// <summary>Sets the balance of the audio of an input (from 0 to 1).</summary>
        public void SetBalance(Guid inputUuid, float balance)
        {
            _obs.RunExternal(() =>
            {
                _world.SetBalance(FindInput(inputUuid), balance);
                return 0;
            });
        }

        /// <summary>Sets the sync offset of the audio of an input, in milliseconds.</summary>
        public void SetSyncOffset(Guid inputUuid, int milliseconds)
        {
            _obs.RunExternal(() =>
            {
                _world.SetSyncOffset(FindInput(inputUuid), milliseconds);
                return 0;
            });
        }

        /// <summary>Sets the monitor type of an input.</summary>
        public void SetMonitorType(Guid inputUuid, string monitorType)
        {
            _obs.RunExternal(() =>
            {
                _world.SetMonitorType(FindInput(inputUuid), monitorType);
                return 0;
            });
        }

        /// <summary>Sets the audio tracks of an input, one bit for each.</summary>
        public void SetTracks(Guid inputUuid, int tracks)
        {
            _obs.RunExternal(() =>
            {
                _world.SetTracks(FindInput(inputUuid), tracks);
                return 0;
            });
        }

        /// <summary>Plays, pauses, stops or restarts the media of an input, as its controls (or a hotkey) do; the action is the name of an <c>ObsMediaInputAction</c>.</summary>
        public void TriggerMedia(Guid inputUuid, string action)
        {
            _obs.RunExternal(() =>
            {
                _world.TriggerMedia(FindInput(inputUuid), action);
                return 0;
            });
        }

        /// <summary>The media of an input plays to its end, or fails to open, and OBS Studio announces that it ended.</summary>
        public void EndMedia(Guid inputUuid)
        {
            _obs.RunExternal(() =>
            {
                _world.EndMedia(FindInput(inputUuid));
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

        /// <summary>Switches studio mode on or off, as its button does.</summary>
        public void SetStudioMode(bool enabled)
        {
            _obs.RunExternal(() =>
            {
                _world.SetStudioMode(enabled);
                return 0;
            });
        }

        /// <summary>Makes a scene the preview scene, as a click on it does while studio mode is on.</summary>
        public void SwitchPreviewScene(Guid sceneUuid)
        {
            _obs.RunExternal(() =>
            {
                _world.SwitchPreview(FindScene(sceneUuid));
                return 0;
            });
        }

        /// <summary>Triggers the transition of studio mode, as its button does.</summary>
        public void TriggerTransition()
        {
            _obs.RunExternal(() =>
            {
                _world.TriggerTransition();
                return 0;
            });
        }

        /// <summary>Makes a transition the current one, as its dropdown does.</summary>
        public void SetTransition(string name)
        {
            _obs.RunExternal(() =>
            {
                _world.SetCurrentTransition(name);
                return 0;
            });
        }

        /// <summary>Sets how long the transitions take, as its box does.</summary>
        public void SetTransitionDuration(int milliseconds)
        {
            _obs.RunExternal(() =>
            {
                _world.SetTransitionDuration(milliseconds);
                return 0;
            });
        }

        /// <summary>
        /// Sends the levels of the audio of the inputs that are on the air (<c>InputVolumeMeters</c>), as obs-websocket does every 50 ms to a client that subscribed to them. The simulation has no clock that ticks, so
        /// it sends them when this is called.
        /// </summary>
        public void MeterTick()
        {
            _obs.RunExternal(() =>
            {
                _world.MeterTick();
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
