namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// Feeds a <see cref="ModelEngine"/> what a connection to OBS Studio would: it numbers what arrives, builds the messages as OBS Studio sends
    /// them, and answers the reads of the engine from a description of OBS Studio.
    /// </summary>
    internal sealed class EngineHarness
    {
        private long _seq;
        private TimeSpan _now;
        private int _requestId;
        private int _session;

        public EngineHarness(ModelEngineOptions? options = null)
        {
            this.Engine = new ModelEngine(options);
        }

        public ModelEngine Engine { get; }

        public FakeWorld World { get; } = new();

        public TimeSpan Now => this._now;

        public long Seq => this._seq;

        public void Advance(TimeSpan by)
        {
            this._now += by;
        }

        public void Start()
        {
            this.Engine.OnSessionStarted(++this._session, ++this._seq, this._now);
        }

        /// <summary>The connection ends.</summary>
        public void End()
        {
            this.Engine.OnSessionEnded(this._session, ++this._seq, this._now);
        }

        /// <summary>Delivers an event, which gets the next number.</summary>
        public void Event(string eventType, object data, int intent = 1)
        {
            var json = JsonSerializer.Serialize(new { op = 5, d = new { eventType, eventIntent = intent, eventData = data } });
            var message = JsonSerializer.Deserialize<ObsMessage>(json)!;
            this.Engine.OnEvent((EventMessage)message.Data!, ++this._seq, this._now);
        }

        /// <summary>Takes the next read of the engine, as it would be sent now.</summary>
        public ReadTicket? Take()
        {
            return this.Engine.TryTakeRead(this._seq, this._now, out var ticket) ? ticket : null;
        }

        /// <summary>Answers a read: the response messages arrive (they get the next number) and the engine is told.</summary>
        /// <param name="ticket">The read.</param>
        /// <param name="answer">What OBS answers to a request; the world by default.</param>
        /// <param name="only">The number of responses that arrive, when it is not all of them (a batch that ended early).</param>
        public void Complete(ReadTicket ticket, Func<ReadRequest, (int Code, object? Data)>? answer = null, int? only = null)
        {
            answer ??= this.World.Answer;
            var responses = new List<RequestResponseMessage>();
            foreach (var request in ticket.Requests.Take(only ?? int.MaxValue))
            {
                var (code, data) = answer(request);
                var json = JsonSerializer.Serialize(new
                {
                    op = 7,
                    d = new
                    {
                        requestType = request.Type.ToString(),
                        requestId = $"r{++this._requestId}",
                        requestStatus = new { result = code == 100, code },
                        responseData = data,
                    },
                });
                var message = JsonSerializer.Deserialize<ObsMessage>(json)!;
                responses.Add((RequestResponseMessage)message.Data!);
            }

            this._seq++;
            this.Engine.OnReadCompleted(ticket, responses, this._now);
        }

        /// <summary>Takes and answers reads until the engine has none left to take now.</summary>
        public int Settle(Func<ReadRequest, (int Code, object? Data)>? answer = null, int max = 200)
        {
            var reads = 0;
            while (reads < max && this.Take() is { } ticket)
            {
                this.Complete(ticket, answer);
                reads++;
            }

            return reads;
        }

        /// <summary>
        /// Settles, and then lets time pass until the engine has nothing to wait for: for the reads that are repeated after a wait.
        /// </summary>
        public void SettleWithTime(Func<ReadRequest, (int Code, object? Data)>? answer = null, int maxRounds = 100)
        {
            for (var round = 0; round < maxRounds; round++)
            {
                this.Settle(answer);
                if (this.Engine.NextWake is not { } wake)
                {
                    return;
                }

                this._now = wake > this._now ? wake : this._now;
            }

            throw new InvalidOperationException("The engine never settled.");
        }
    }

    /// <summary>
    /// A description of OBS Studio that answers the reads of an engine.
    /// </summary>
    internal sealed class FakeWorld
    {
        public Guid? Program { get; set; }

        public List<(Guid Uuid, string Name)> Scenes { get; } = [];

        public List<(Guid Uuid, string Name, string Kind, uint Caps, bool Muted, double VolumeMul)> Inputs { get; } = [];

        public Dictionary<Guid, List<(int Id, Guid Source, bool IsScene, string SourceName, int Index, bool Enabled, bool Locked)>> Items { get; } = new();

        public bool StudioMode { get; set; }

        /// <summary>Gets or sets the preview scene while studio mode is on; the program scene when it is null (OBS Studio makes the program scene the preview scene when it switches studio mode on).</summary>
        public Guid? Preview { get; set; }

        /// <summary>Gets or sets the current scene transition: OBS Studio has a cut and a fade to begin with, and the fade is the current one.</summary>
        public (string Name, Guid Uuid, string Kind, bool IsFixed, bool IsConfigurable)? Transition { get; set; } = ("Fade", new Guid("b40ea4bd-c1de-492b-9083-5055b9bfb0fe"), "fade_transition", false, false);

        /// <summary>Gets or sets how long the transitions take, in milliseconds; a fixed transition does not say it.</summary>
        public int TransitionDuration { get; set; } = 300;

        /// <summary>Gets what the outputs of OBS Studio are doing; an output that is not in it is stopped.</summary>
        public Dictionary<ObsOutputKind, ObsOutputStatus> Outputs { get; } = new();

        /// <summary>Gets the filters of the scenes and the inputs, by the UUID of the source, in the order of their positions (the first is at position 0).</summary>
        public Dictionary<Guid, List<(string Name, string Kind, bool Enabled)>> Filters { get; } = new();

        /// <summary>Gets the groups: scenes that a scene item shows, and that the list of scenes does not have (a group says <c>isGroup</c> in the scene items that show it).</summary>
        public HashSet<Guid> Groups { get; } = [];

        /// <summary>Gets or sets whether the list of inputs leaves out the capabilities of the kinds, as obs-websocket before 5.6.0 does.</summary>
        public bool OmitInputKindCaps { get; set; }

        /// <summary>Gets the settings of the inputs that have some set, by UUID (an object that is written as JSON); an input that is not in it has none.</summary>
        public Dictionary<Guid, object> InputSettings { get; } = new();

        /// <summary>Gets the audio balance of the inputs that have another than the middle (0.5), by UUID.</summary>
        public Dictionary<Guid, float> AudioBalances { get; } = new();

        /// <summary>Gets the sync offset, in milliseconds, of the inputs that have one, by UUID.</summary>
        public Dictionary<Guid, int> SyncOffsets { get; } = new();

        /// <summary>Gets the monitor type of the inputs that have another than none, by UUID, as obs-websocket writes it.</summary>
        public Dictionary<Guid, string> MonitorTypes { get; } = new();

        /// <summary>Gets the audio tracks of the inputs that are not on all six, by UUID, as a bit for each track.</summary>
        public Dictionary<Guid, int> Tracks { get; } = new();

        /// <summary>Gets what the media inputs that are not in the state none do, by UUID, as obs-websocket writes it (<c>OBS_MEDIA_STATE_PLAYING</c>).</summary>
        public Dictionary<Guid, string> MediaStates { get; } = new();

        /// <summary>Gets the length and the position, in milliseconds, of the media inputs that play or are paused, by UUID; the others say neither (null).</summary>
        public Dictionary<Guid, (long Duration, long Cursor)> MediaTimes { get; } = new();

        /// <summary>Gets the settings of the filters that have some set, by the UUID of their source and their name (an object that is written as JSON).</summary>
        public Dictionary<(Guid Source, string Name), object> FilterSettings { get; } = new();

        /// <summary>Gets the transform of the scene items that have another than <see cref="DefaultTransform"/>, by the UUID of their scene and their id.</summary>
        public Dictionary<(Guid Scene, int Id), SceneItemTransform> Transforms { get; } = new();

        /// <summary>Gets whether the inputs are on the air and whether they are shown, by UUID, as <c>GetSourceActive</c> says it; an input that is not in it is neither.</summary>
        public Dictionary<Guid, (bool Active, bool Showing)> Activity { get; } = new();

        /// <summary>Gets or sets the name of the current profile of OBS Studio.</summary>
        public string CurrentProfile { get; set; } = "Untitled";

        /// <summary>Gets the names of the profiles, in the order that OBS Studio lists them.</summary>
        public List<string> Profiles { get; } = ["Untitled"];

        /// <summary>Gets or sets the name of the scene collection that OBS Studio has loaded.</summary>
        public string CurrentSceneCollection { get; set; } = "Untitled";

        /// <summary>Gets the names of the scene collections, in the order that OBS Studio lists them.</summary>
        public List<string> SceneCollections { get; } = ["Untitled"];

        /// <summary>
        /// Gets the canvases of OBS Studio, in the order that <c>GetCanvasList</c> lists them: the main canvas, which OBS Studio always has, and then the ones that a plugin made. The flags are the numbers of libobs
        /// (main 1, activate 2, mix audio 4, scene ref 8, ephemeral 16); a canvas that has no video says null for its video settings.
        /// </summary>
        public List<(Guid Uuid, string Name, int Flags, bool HasVideo)> Canvases { get; } = [(new Guid("6c69626f-6273-4c00-9d88-c5136d61696e"), "Main", 15, true)];

        public Guid AddScene(string name)
        {
            var uuid = Guid.NewGuid();
            this.Scenes.Insert(0, (uuid, name));
            this.Items[uuid] = [];
            this.Program ??= uuid;
            return uuid;
        }

        public Guid AddInput(string name, string kind = "color_source_v3", uint caps = 32777, bool muted = false, double volumeMul = 1.0)
        {
            var uuid = Guid.NewGuid();
            this.Inputs.Add((uuid, name, kind, caps, muted, volumeMul));
            return uuid;
        }

        public int AddItem(Guid scene, Guid source, string sourceName, bool isScene = false)
        {
            var items = this.Items[scene];
            var id = items.Count == 0 ? 1 : items.Max(i => i.Id) + 1;
            items.Add((id, source, isScene, sourceName, items.Count, true, false));
            return id;
        }

        /// <summary>Puts a filter on top of the filters of a scene or an input.</summary>
        public void AddFilter(Guid source, string name, string kind = "color_filter_v2", bool enabled = true)
        {
            if (!this.Filters.TryGetValue(source, out var filters))
            {
                this.Filters[source] = filters = [];
            }

            filters.Add((name, kind, enabled));
        }

        /// <summary>Gets the names of the filters of a source, in the order of their positions.</summary>
        public string[] FilterNames(Guid source)
        {
            return this.Filters.TryGetValue(source, out var filters) ? filters.Select(filter => filter.Name).ToArray() : [];
        }

        /// <summary>Enables or disables a filter of a scene or an input, as OBS Studio does when the user or a request does.</summary>
        public void SetFilterEnabled(Guid source, string name, bool enabled)
        {
            var filters = this.Filters[source];
            var position = filters.FindIndex(filter => filter.Name == name);
            filters[position] = (name, filters[position].Kind, enabled);
        }

        /// <summary>Gives a filter another name.</summary>
        public void RenameFilter(Guid source, string name, string newName)
        {
            var filters = this.Filters[source];
            var position = filters.FindIndex(filter => filter.Name == name);
            filters[position] = (newName, filters[position].Kind, filters[position].Enabled);
        }

        /// <summary>Takes a filter away.</summary>
        public void RemoveFilter(Guid source, string name)
        {
            this.Filters[source].RemoveAll(filter => filter.Name == name);
        }

        /// <summary>Moves a filter to a position among the filters of its source.</summary>
        public void MoveFilter(Guid source, string name, int index)
        {
            var filters = this.Filters[source];
            var position = filters.FindIndex(filter => filter.Name == name);
            var moved = filters[position];
            filters.RemoveAt(position);
            filters.Insert(index, moved);
        }

        private bool IsSource(Guid uuid)
        {
            return this.Scenes.Any(s => s.Uuid == uuid) || this.Inputs.Any(i => i.Uuid == uuid) || this.Groups.Contains(uuid) || this.Items.ContainsKey(uuid);
        }

        /// <summary>Renames a scene or an input, and so the name that the scene items that show it have.</summary>
        public void Rename(Guid uuid, string name)
        {
            for (var i = 0; i < this.Scenes.Count; i++)
            {
                if (this.Scenes[i].Uuid == uuid)
                {
                    this.Scenes[i] = (uuid, name);
                }
            }

            for (var i = 0; i < this.Inputs.Count; i++)
            {
                var input = this.Inputs[i];
                if (input.Uuid == uuid)
                {
                    this.Inputs[i] = (uuid, name, input.Kind, input.Caps, input.Muted, input.VolumeMul);
                }
            }

            foreach (var items in this.Items.Values)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item.Source == uuid)
                    {
                        items[i] = (item.Id, item.Source, item.IsScene, name, item.Index, item.Enabled, item.Locked);
                    }
                }
            }
        }

        public (int Code, object? Data) Answer(ReadRequest request)
        {
            switch (request.Kind)
            {
                case ReadKind.Scenes:
                    return (100, new
                    {
                        currentProgramSceneName = this.Scenes.FirstOrDefault(s => s.Uuid == this.Program).Name,
                        currentProgramSceneUuid = this.Program,
                        currentPreviewSceneName = (string?)null,
                        currentPreviewSceneUuid = (Guid?)null,
                        scenes = this.Scenes.Select((s, i) => new { sceneIndex = i, sceneName = s.Name, sceneUuid = s.Uuid }).ToArray(),
                    });
                case ReadKind.Inputs:
                    return (100, new
                    {
                        inputs = this.Inputs.Select(i => this.OmitInputKindCaps
                            ? (object)new { inputKind = i.Kind, inputName = i.Name, inputUuid = i.Uuid, unversionedInputKind = i.Kind }
                            : new { inputKind = i.Kind, inputKindCaps = i.Caps, inputName = i.Name, inputUuid = i.Uuid, unversionedInputKind = i.Kind }).ToArray(),
                    });
                case ReadKind.Program:
                    var program = this.Scenes.First(s => s.Uuid == this.Program);
                    return (100, new { sceneName = program.Name, sceneUuid = program.Uuid });
                case ReadKind.StudioMode:
                    return (100, new { studioModeEnabled = this.StudioMode });
                case ReadKind.Preview:
                    if (!this.StudioMode)
                    {
                        return (506, null);
                    }

                    var preview = this.Scenes.First(s => s.Uuid == (this.Preview ?? this.Program));
                    return (100, new { sceneName = preview.Name, sceneUuid = preview.Uuid });
                case ReadKind.Transition:
                    return this.Transition is { } transition
                        ? (100, new
                        {
                            transitionConfigurable = transition.IsConfigurable,
                            transitionDuration = transition.IsFixed ? null : (int?)this.TransitionDuration,
                            transitionFixed = transition.IsFixed,
                            transitionKind = transition.Kind,
                            transitionName = transition.Name,
                            transitionSettings = transition.IsConfigurable ? (object)new { } : null,
                            transitionUuid = transition.Uuid,
                        })
                        : (600, null);
                case ReadKind.Profiles:
                    return (100, new { currentProfileName = this.CurrentProfile, profiles = this.Profiles.ToArray() });
                case ReadKind.SceneCollections:
                    return (100, new { currentSceneCollectionName = this.CurrentSceneCollection, sceneCollections = this.SceneCollections.ToArray() });
                case ReadKind.Canvases:
                    return (100, new
                    {
                        canvases = this.Canvases.Select(canvas => new
                        {
                            canvasFlags = new { ACTIVATE = (canvas.Flags & 2) != 0, EPHEMERAL = (canvas.Flags & 16) != 0, MAIN = (canvas.Flags & 1) != 0, MIX_AUDIO = (canvas.Flags & 4) != 0, SCENE_REF = (canvas.Flags & 8) != 0 },
                            canvasName = canvas.Name,
                            canvasUuid = canvas.Uuid,
                            canvasVideoSettings = canvas.HasVideo
                                ? new { baseHeight = (int?)1080, baseWidth = (int?)1920, fpsDenominator = (int?)1, fpsNumerator = (int?)30, outputHeight = (int?)720, outputWidth = (int?)1280 }
                                : new { baseHeight = (int?)null, baseWidth = (int?)null, fpsDenominator = (int?)null, fpsNumerator = (int?)null, outputHeight = (int?)null, outputWidth = (int?)null },
                        }).ToArray(),
                    });
                case ReadKind.Stream:
                    var streamStatus = this.OutputStatus(ObsOutputKind.Stream);
                    return (100, new { outputActive = IsRunning(streamStatus), outputReconnecting = streamStatus == ObsOutputStatus.Reconnecting, outputTimecode = "00:00:00.000", outputDuration = 0L, outputCongestion = 0.0, outputBytes = 0L, outputSkippedFrames = 0L, outputTotalFrames = 0L });
                case ReadKind.Record:
                    var recordStatus = this.OutputStatus(ObsOutputKind.Record);
                    return (100, new { outputActive = IsRunning(recordStatus), outputPaused = recordStatus == ObsOutputStatus.Paused, outputTimecode = "00:00:00.000", outputDuration = 0L, outputBytes = 0L });
                case ReadKind.VirtualCamera:
                    return (100, new { outputActive = IsRunning(this.OutputStatus(ObsOutputKind.VirtualCamera)) });
                case ReadKind.ReplayBuffer:
                    return (100, new { outputActive = IsRunning(this.OutputStatus(ObsOutputKind.ReplayBuffer)) });
                case ReadKind.Filters:
                    return this.IsSource(request.Target)
                        ? (100, new
                        {
                            filters = (this.Filters.TryGetValue(request.Target, out var filters) ? filters : [])
                                .Select((filter, position) => new { filterEnabled = filter.Enabled, filterIndex = position, filterKind = filter.Kind, filterName = filter.Name, filterSettings = this.FilterSettings.TryGetValue((request.Target, filter.Name), out var filterSettings) ? filterSettings : new { } })
                                .ToArray(),
                        })
                        : (600, null);
                case ReadKind.Items:
                    return this.Items.TryGetValue(request.Target, out var items)
                        ? (100, new { sceneItems = items.OrderBy(i => i.Index).Select(item => this.Item(request.Target, item)).ToArray() })
                        : (600, null);
                case ReadKind.AudioMute:
                    return this.AudioAnswer(request.Target, input => new { inputMuted = input.Muted });
                case ReadKind.AudioVolume:
                    return this.AudioAnswer(request.Target, input => new { inputVolumeMul = input.VolumeMul, inputVolumeDb = 0.0 });
                case ReadKind.AudioBalance:
                    return this.AudioAnswer(request.Target, input => new { inputAudioBalance = this.AudioBalances.GetValueOrDefault(input.Uuid, 0.5f) });
                case ReadKind.AudioSyncOffset:
                    return this.AudioAnswer(request.Target, input => new { inputAudioSyncOffset = this.SyncOffsets.GetValueOrDefault(input.Uuid, 0) });
                case ReadKind.AudioMonitorType:
                    return this.AudioAnswer(request.Target, input => new { monitorType = this.MonitorTypes.GetValueOrDefault(input.Uuid, "OBS_MONITORING_TYPE_NONE") });
                case ReadKind.AudioTracks:
                    return this.AudioAnswer(request.Target, input => new { inputAudioTracks = TracksOf(this.Tracks.GetValueOrDefault(input.Uuid, 63)) });
                case ReadKind.InputSettings:
                    var found = this.Inputs.Where(input => input.Uuid == request.Target).Select(input => ((Guid, string, string, uint, bool, double)?)input).FirstOrDefault();
                    return found is { } settingsOf
                        ? (100, new { inputKind = settingsOf.Item3, inputSettings = this.InputSettings.TryGetValue(request.Target, out var settings) ? settings : new { } })
                        : (600, null);
                case ReadKind.MediaStatus:
                    return this.Inputs.Any(input => input.Uuid == request.Target)
                        ? (100, new
                        {
                            mediaState = this.MediaStates.GetValueOrDefault(request.Target, "OBS_MEDIA_STATE_NONE"),
                            mediaDuration = this.MediaTimes.TryGetValue(request.Target, out var times) ? (long?)times.Duration : null,
                            mediaCursor = this.MediaTimes.TryGetValue(request.Target, out var cursor) ? (long?)cursor.Cursor : null,
                        })
                        : (600, null);
                case ReadKind.Activity:
                    return this.Inputs.Any(input => input.Uuid == request.Target)
                        ? (100, new
                        {
                            videoActive = this.Activity.TryGetValue(request.Target, out var activity) && activity.Active,
                            videoShowing = activity.Showing,
                        })
                        : (600, null);
                default:
                    throw new InvalidOperationException($"The fake world does not know the read {request.Kind}.");
            }
        }

        // The state of the six tracks, from a bit for each: track 1 is bit 0.
        private static Dictionary<string, bool> TracksOf(int mask)
        {
            return Enumerable.Range(1, 6).ToDictionary(track => track.ToString(System.Globalization.CultureInfo.InvariantCulture), track => (mask & (1 << (track - 1))) != 0);
        }

        private ObsOutputStatus OutputStatus(ObsOutputKind kind)
        {
            return this.Outputs.TryGetValue(kind, out var status) ? status : ObsOutputStatus.Stopped;
        }

        // OBS says that an output is active while it runs, and also while it is stopping (it is not done until it has stopped); an output that is starting is not active yet.
        private static bool IsRunning(ObsOutputStatus status)
        {
            return status is ObsOutputStatus.Started or ObsOutputStatus.Stopping or ObsOutputStatus.Reconnecting or ObsOutputStatus.Paused;
        }

        // OBS reads the audio state of an input that has audio. For one that has none it refuses (604, InvalidResourceState), and for one that does not exist it says so (600).
        private (int Code, object? Data) AudioAnswer(Guid uuid, Func<(Guid Uuid, string Name, string Kind, uint Caps, bool Muted, double VolumeMul), object> data)
        {
            foreach (var input in this.Inputs)
            {
                if (input.Uuid == uuid)
                {
                    return (input.Caps & 2) != 0 ? (100, data(input)) : (604, null);
                }
            }

            return (600, null);
        }

        private object Item(Guid scene, (int Id, Guid Source, bool IsScene, string SourceName, int Index, bool Enabled, bool Locked) item)
        {
            return new
            {
                inputKind = item.IsScene ? null : "color_source_v3",
                isGroup = item.IsScene ? this.Groups.Contains(item.Source) : (bool?)null,
                sceneItemBlendMode = "OBS_BLEND_NORMAL",
                sceneItemEnabled = item.Enabled,
                sceneItemId = item.Id,
                sceneItemIndex = item.Index,
                sceneItemLocked = item.Locked,
                sceneItemTransform = this.Transforms.TryGetValue((scene, item.Id), out var transform) ? transform : DefaultTransform,
                sourceName = item.SourceName,
                sourceType = item.IsScene ? "OBS_SOURCE_TYPE_SCENE" : "OBS_SOURCE_TYPE_INPUT",
                sourceUuid = item.Source,
            };
        }

        /// <summary>The transform of a scene item that a test did not give one: what OBS Studio says of an item that shows a source of 1920 by 1080 that was just made.</summary>
        public static SceneItemTransform DefaultTransform { get; } = new(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 0, 0, 0, 1080f, 0f, 0f, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = false };
    }
}
