namespace OBSStudioClient.Tests.Studio
{
    using System.Text.Json;
    using OBSStudioClient.Messages;
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
            this.Engine.OnSessionStarted(1, ++this._seq, this._now);
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
        public void Complete(ReadTicket ticket, Func<ReadRequest, (int Code, object? Data)>? answer = null)
        {
            answer ??= this.World.Answer;
            var responses = new List<RequestResponseMessage>();
            foreach (var request in ticket.Requests)
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

        public Guid AddScene(string name)
        {
            var uuid = Guid.NewGuid();
            this.Scenes.Insert(0, (uuid, name));
            this.Items[uuid] = [];
            this.Program ??= uuid;
            return uuid;
        }

        public Guid AddInput(string name, string kind = "color_source_v3", uint caps = 32777)
        {
            var uuid = Guid.NewGuid();
            this.Inputs.Add((uuid, name, kind, caps, false, 1.0));
            return uuid;
        }

        public int AddItem(Guid scene, Guid source, string sourceName, bool isScene = false)
        {
            var items = this.Items[scene];
            var id = items.Count == 0 ? 1 : items.Max(i => i.Id) + 1;
            items.Add((id, source, isScene, sourceName, items.Count, true, false));
            return id;
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
                        inputs = this.Inputs.Select(i => new { inputKind = i.Kind, inputKindCaps = i.Caps, inputName = i.Name, inputUuid = i.Uuid, unversionedInputKind = i.Kind }).ToArray(),
                    });
                case ReadKind.Program:
                    var program = this.Scenes.First(s => s.Uuid == this.Program);
                    return (100, new { sceneName = program.Name, sceneUuid = program.Uuid });
                case ReadKind.StudioMode:
                    return (100, new { studioModeEnabled = this.StudioMode });
                case ReadKind.Items:
                    return this.Items.TryGetValue(request.Target, out var items)
                        ? (100, new { sceneItems = items.OrderBy(i => i.Index).Select(Item).ToArray() })
                        : (600, null);
                case ReadKind.AudioMute:
                    return this.Inputs.Any(i => i.Uuid == request.Target) ? (100, new { inputMuted = this.Inputs.First(i => i.Uuid == request.Target).Muted }) : (600, null);
                default:
                    return this.Inputs.Any(i => i.Uuid == request.Target)
                        ? (100, new { inputVolumeMul = this.Inputs.First(i => i.Uuid == request.Target).VolumeMul, inputVolumeDb = 0.0 })
                        : (600, null);
            }
        }

        private static object Item((int Id, Guid Source, bool IsScene, string SourceName, int Index, bool Enabled, bool Locked) item)
        {
            return new
            {
                inputKind = item.IsScene ? null : "color_source_v3",
                isGroup = (bool?)null,
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
