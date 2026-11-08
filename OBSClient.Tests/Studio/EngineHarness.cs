namespace OBSStudioClient.Tests.Studio
{
    using System.Text.Json;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// Feeds a <see cref="StudioEngine"/> what a connection to OBS Studio would: it numbers what arrives, builds the messages as OBS Studio sends
    /// them, and answers the reads of the engine from a description of OBS Studio.
    /// </summary>
    internal sealed class EngineHarness
    {
        private long _seq;
        private TimeSpan _now;
        private int _requestId;
        private int _session;

        public EngineHarness(StudioEngineOptions? options = null)
        {
            this.Engine = new StudioEngine(options);
        }

        public StudioEngine Engine { get; }

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

        /// <summary>Gets what the outputs of OBS Studio are doing; an output that is not in it is stopped.</summary>
        public Dictionary<ObsOutputKind, ObsOutputStatus> Outputs { get; } = new();

        /// <summary>Gets the groups: scenes that a scene item shows, and that the list of scenes does not have (a group says <c>isGroup</c> in the scene items that show it).</summary>
        public HashSet<Guid> Groups { get; } = [];

        /// <summary>Gets or sets whether the list of inputs leaves out the capabilities of the kinds, as obs-websocket before 5.6.0 does.</summary>
        public bool OmitInputKindCaps { get; set; }

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
                case ReadKind.Items:
                    return this.Items.TryGetValue(request.Target, out var items)
                        ? (100, new { sceneItems = items.OrderBy(i => i.Index).Select(this.Item).ToArray() })
                        : (600, null);
                case ReadKind.AudioMute:
                    return this.AudioAnswer(request.Target, input => new { inputMuted = input.Muted });
                default:
                    return this.AudioAnswer(request.Target, input => new { inputVolumeMul = input.VolumeMul, inputVolumeDb = 0.0 });
            }
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

        private object Item((int Id, Guid Source, bool IsScene, string SourceName, int Index, bool Enabled, bool Locked) item)
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
                sceneItemTransform = new
                {
                    alignment = 5,
                    boundsAlignment = 0,
                    boundsHeight = 0.0,
                    boundsType = "OBS_BOUNDS_NONE",
                    boundsWidth = 0.0,
                    cropBottom = 0,
                    cropLeft = 0,
                    cropRight = 0,
                    cropToBounds = false,
                    cropTop = 0,
                    height = 1080.0,
                    positionX = 0.0,
                    positionY = 0.0,
                    rotation = 0.0,
                    scaleX = 1.0,
                    scaleY = 1.0,
                    sourceHeight = 1080.0,
                    sourceWidth = 1920.0,
                    width = 1920.0,
                },
                sourceName = item.SourceName,
                sourceType = item.IsScene ? "OBS_SOURCE_TYPE_SCENE" : "OBS_SOURCE_TYPE_INPUT",
                sourceUuid = item.Source,
            };
        }
    }
}
