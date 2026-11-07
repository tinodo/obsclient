namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// An <see cref="ObsStudio"/> that follows a stand-in for OBS Studio (<see cref="WorldResponder"/>) with a small collection, for the tests of the changes that make and remove
    /// scenes, inputs and scene items by name: what a test tells the stand-in to answer is made in its world by the test, as OBS Studio would, so that the model can be told of it
    /// by a read when no event comes (<see cref="StudioWriterTests"/> shows that the events and the reads end a change in the same way).
    /// </summary>
    /// <remarks>
    /// The collection: "Gaming" shows a camera (scene item 1, an input without audio), a microphone (2, an input with audio), an overlay (3, an input without audio), and the scene
    /// "Chatting" (4). "Chatting" shows the microphone (1) and the overlay (2). "Spare" is a scene that nothing shows, and "Desktop Audio" an input that no scene item shows (OBS Studio
    /// keeps its audio devices that way). "Scene" is empty, and is the program scene. With a group: "Gaming" shows a group, "Group" (5), that shows nothing.
    /// </remarks>
    internal sealed class ByNameFixture : IAsyncDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly object _announcements = new();
        private readonly HashSet<Guid> _removedInputs = [];
        private Task _announced = Task.CompletedTask;

        private ByNameFixture(StudioRig rig, Guid gaming, Guid chatting, Guid spare, Guid camera, Guid mic, Guid overlay, Guid desktop, Guid group)
        {
            this.Rig = rig;
            this.Gaming = gaming;
            this.Chatting = chatting;
            this.Spare = spare;
            this.Camera = camera;
            this.Mic = mic;
            this.Overlay = overlay;
            this.Desktop = desktop;
            this.Group = group;
        }

        public StudioRig Rig { get; }

        public ObsStudio Studio => this.Rig.Studio;

        public WorldResponder Responder => this.Rig.Responder;

        public Guid Gaming { get; }

        public Guid Chatting { get; }

        public Guid Spare { get; }

        public Guid Camera { get; }

        public Guid Mic { get; }

        public Guid Overlay { get; }

        public Guid Desktop { get; }

        public Guid Group { get; }

        public static async Task<ByNameFixture> StartAsync(ObsStudioOptions? options = null, bool withGroup = false)
        {
            Guid gaming = default, chatting = default, spare = default, camera = default, mic = default, overlay = default, desktop = default, group = default;
            var rig = await StudioRig.ConnectedAsync(
                world =>
                {
                    gaming = world.AddScene("Gaming");
                    chatting = world.AddScene("Chatting");
                    spare = world.AddScene("Spare");
                    camera = world.AddInput("Camera");
                    mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    overlay = world.AddInput("Overlay");
                    desktop = world.AddInput("Desktop Audio", "wasapi_output_capture", 2);
                    world.AddItem(gaming, camera, "Camera");
                    world.AddItem(gaming, mic, "Mic");
                    world.AddItem(gaming, overlay, "Overlay");
                    world.AddItem(gaming, chatting, "Chatting", isScene: true);
                    world.AddItem(chatting, mic, "Mic");
                    world.AddItem(chatting, overlay, "Overlay");
                    if (withGroup)
                    {
                        group = Guid.NewGuid();
                        world.Groups.Add(group);
                        world.Items[group] = [];
                        world.AddItem(gaming, group, "Group", isScene: true);
                    }
                },
                options ?? new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromSeconds(5) });
            return new ByNameFixture(rig, gaming, chatting, spare, camera, mic, overlay, desktop, group);
        }

        public int Count(string type)
        {
            return this.Responder.RequestLog.Count(logged => logged == type);
        }

        /// <summary>OBS Studio makes a scene.</summary>
        public Guid MakeScene(string name)
        {
            var uuid = Guid.Empty;
            this.Responder.Change(world => uuid = world.AddScene(name));
            return uuid;
        }

        /// <summary>OBS Studio makes an input, and a scene item that shows it in a scene.</summary>
        public (Guid Input, int Item) MakeInput(Guid scene, string name, string kind = "color_source_v3", uint caps = 32777, bool muted = false, double volumeMul = 1.0)
        {
            var input = Guid.Empty;
            var item = 0;
            this.Responder.Change(world =>
            {
                input = world.AddInput(name, kind, caps, muted, volumeMul);
                item = world.AddItem(scene, input, name);
            });
            return (input, item);
        }

        /// <summary>OBS Studio makes a scene item that shows a source in a scene.</summary>
        public int MakeItem(Guid scene, Guid source, string sourceName, bool isScene = false)
        {
            var item = 0;
            this.Responder.Change(world => item = world.AddItem(scene, source, sourceName, isScene));
            return item;
        }

        /// <summary>OBS Studio gives a scene or an input another name.</summary>
        public void Rename(Guid uuid, string name)
        {
            this.Responder.Change(world => world.Rename(uuid, name));
        }

        /// <summary>
        /// OBS Studio removes a scene item, and the scene items above it move down by one. What the scene item showed is destroyed when nothing else shows it: an input, and a scene or a
        /// group that is not in the list of scenes any more. A moment after the world changed, OBS Studio announces the scene item, and the input that was destroyed without having been
        /// removed (a scene that is destroyed says nothing).
        /// </summary>
        public void RemoveItem(Guid scene, int id)
        {
            var sceneName = "Removed";
            var sourceName = string.Empty;
            var source = Guid.Empty;
            var destroyedInput = false;
            this.Responder.Change(world =>
            {
                var removed = world.Items[scene].First(item => item.Id == id);
                source = removed.Source;
                sourceName = removed.SourceName;
                sceneName = world.Scenes.Where(s => s.Uuid == scene).Select(s => s.Name).FirstOrDefault() ?? sceneName;
                world.Items[scene] = world.Items[scene]
                    .Where(item => item.Id != id)
                    .OrderBy(item => item.Index)
                    .Select((item, index) => (item.Id, item.Source, item.IsScene, item.SourceName, index, item.Enabled, item.Locked))
                    .ToList();
                if (!world.Items.Values.Any(items => items.Any(item => item.Source == source)))
                {
                    destroyedInput = world.Inputs.RemoveAll(input => input.Uuid == source) > 0;
                    if (!world.Scenes.Any(s => s.Uuid == source))
                    {
                        world.Items.Remove(source);
                    }
                }
            });
            this.Announce("SceneItemRemoved", new { sceneName, sceneUuid = scene, sourceName, sourceUuid = source, sceneItemId = id }, 128);
            if (destroyedInput && this.WasNotRemoved(source))
            {
                this.Announce("InputRemoved", new { inputName = sourceName, inputUuid = source }, 8);
            }
        }

        /// <summary>OBS Studio shows or hides a scene item.</summary>
        public void SetEnabled(Guid scene, int id, bool enabled)
        {
            this.Responder.Change(world =>
            {
                var items = world.Items[scene];
                var position = items.FindIndex(item => item.Id == id);
                var item = items[position];
                items[position] = (item.Id, item.Source, item.IsScene, item.SourceName, item.Index, enabled, item.Locked);
            });
        }

        /// <summary>
        /// OBS Studio removes a scene: the list of scenes does not have it any more, and it is destroyed with its scene items when no scene item shows it (and the others stay alive). A
        /// moment after the world changed, OBS Studio announces it.
        /// </summary>
        public void RemoveScene(Guid scene)
        {
            var name = string.Empty;
            this.Responder.Change(world =>
            {
                name = world.Scenes.First(s => s.Uuid == scene).Name;
                world.Scenes.RemoveAll(s => s.Uuid == scene);
                if (!world.Items.Values.Any(items => items.Any(item => item.Source == scene)))
                {
                    world.Items.Remove(scene);
                }
            });
            this.Announce("SceneRemoved", new { sceneName = name, sceneUuid = scene, isGroup = false }, 4);
        }

        /// <summary>
        /// OBS Studio removes an input: it is destroyed when no scene item shows it, and it stays alive, and listed, when scene items show it. A moment after the world changed, OBS Studio
        /// announces it.
        /// </summary>
        public void RemoveInput(Guid input)
        {
            var name = string.Empty;
            this.Responder.Change(world =>
            {
                name = world.Inputs.First(i => i.Uuid == input).Name;
                if (!world.Items.Values.Any(items => items.Any(item => item.Source == input)))
                {
                    world.Inputs.RemoveAll(i => i.Uuid == input);
                }
            });
            this.WasNotRemoved(input);
            this.Announce("InputRemoved", new { inputName = name, inputUuid = input }, 8);
        }

        // Whether OBS Studio has not announced the removal of the input yet (an input is announced once: when it is removed, or when it is destroyed without having been removed).
        private bool WasNotRemoved(Guid input)
        {
            lock (this._announcements)
            {
                return this._removedInputs.Add(input);
            }
        }

        // OBS Studio answers a request, and the events that it caused come a moment later, in the order in which they were made.
        private void Announce(string eventType, object data, int intent)
        {
            lock (this._announcements)
            {
                this._announced = this._announced.ContinueWith(
                    async _ =>
                    {
                        try
                        {
                            await Task.Delay(30);
                            await this.Responder.SendEventAsync(eventType, data, intent);
                        }
                        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or IOException or OperationCanceledException)
                        {
                            // The test ended, and the connection with it.
                        }
                    },
                    TaskScheduler.Default).Unwrap();
            }
        }

        /// <summary>
        /// OBS Studio removes an input that scene items show, and keeps it alive: the event that says so comes, and the model calls the input <see cref="ObsObjectState.Detached"/>.
        /// </summary>
        public async Task DetachAsync(string name, Guid uuid)
        {
            this.WasNotRemoved(uuid);
            await this.Responder.SendEventAsync("InputRemoved", new { inputName = name, inputUuid = uuid }, 8);
            await StudioRig.EventuallyAsync(() => this.FindDetached(uuid).State == ObsObjectState.Detached, $"the input '{name}' to be detached", Timeout);
        }

        /// <summary>Finds an input that the model shows through a scene item (the input is not among the inputs when it is detached).</summary>
        public ObsInput FindDetached(Guid uuid)
        {
            foreach (var scene in this.Studio.Scenes)
            {
                foreach (var item in scene.Items)
                {
                    if (item.Source is ObsInput input && input.Uuid == uuid)
                    {
                        return input;
                    }
                }
            }

            throw new InvalidOperationException($"No scene item shows the input {uuid}.");
        }

        public ValueTask DisposeAsync()
        {
            return this.Rig.DisposeAsync();
        }
    }
}
