namespace OBSStudioClient.Tests.Studio
{
    using System.Text.Json;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Studio.Engine;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// Plays OBS Studio for an <see cref="ObsStudio"/>: it answers GetVersion and the batches of reads of the model from a <see cref="FakeWorld"/>,
    /// and sends the events that a test asks it to send. Everything that is written to the connection goes through it, one at a time.
    /// </summary>
    internal sealed class WorldResponder : IAsyncDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly FakeObsWebSocketServer _server = new();
        private readonly SemaphoreSlim _write = new(1, 1);
        private readonly object _worldLock = new();
        private readonly CancellationTokenSource _stopping = new();
        private Task _loop = Task.CompletedTask;
        private int _batches;

        public FakeWorld World { get; } = new();

        public int Port => this._server.Port;

        /// <summary>Gets or sets the obs-websocket version that GetVersion answers.</summary>
        public string WebSocketVersion { get; set; } = "5.7.6";

        /// <summary>Gets or sets whether the batches of reads are answered.</summary>
        public bool AnswerBatches { get; set; } = true;

        /// <summary>Gets the number of batches that were received.</summary>
        public int Batches => Volatile.Read(ref this._batches);

        public void Start()
        {
            this._server.Start();
            this._loop = Task.Run(this.RunAsync);
        }

        /// <summary>Changes the world, between the answers.</summary>
        public void Change(Action<FakeWorld> change)
        {
            lock (this._worldLock)
            {
                change(this.World);
            }
        }

        public async Task SendEventAsync(string eventType, object data, int intent = 1)
        {
            await this._write.WaitAsync();
            try
            {
                await this._server.SendEventAsync(eventType, data, intent);
            }
            finally
            {
                this._write.Release();
            }
        }

        public void AbortConnection()
        {
            this._server.AbortConnection();
        }

        public async ValueTask DisposeAsync()
        {
            this._stopping.Cancel();
            await this._server.DisposeAsync();
            try
            {
                await this._loop.WaitAsync(Timeout);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }

        private async Task RunAsync()
        {
            // One connection after the other: the studio that reconnects gets a new handshake.
            while (!this._stopping.IsCancellationRequested)
            {
                try
                {
                    await this._server.HandshakeAsync(timeout: Timeout);
                    while (!this._stopping.IsCancellationRequested)
                    {
                        using var message = await this._server.ReceiveJsonAsync(TimeSpan.FromMinutes(1));
                        var op = message.RootElement.GetProperty("op").GetInt32();
                        var d = message.RootElement.GetProperty("d");
                        if (op == 6)
                        {
                            await this.AnswerRequestAsync(d);
                        }
                        else if (op == 8)
                        {
                            Interlocked.Increment(ref this._batches);
                            if (this.AnswerBatches)
                            {
                                await this.AnswerBatchAsync(d);
                            }
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // Nobody connected within the time: wait again.
                }
                catch (Exception)
                {
                    // The connection ended (the studio closed it, or the test cut it), or the server was disposed.
                }
            }
        }

        private async Task AnswerRequestAsync(JsonElement d)
        {
            var type = d.GetProperty("requestType").GetString()!;
            var id = d.GetProperty("requestId").GetString()!;
            var (code, data) = this.Answer(type, d.TryGetProperty("requestData", out var requestData) ? requestData : null);
            await this._write.WaitAsync();
            try
            {
                if (code == 100)
                {
                    await this._server.SendRequestSuccessAsync(type, id, data);
                }
                else
                {
                    await this._server.SendRequestErrorAsync(type, id, code);
                }
            }
            finally
            {
                this._write.Release();
            }
        }

        private async Task AnswerBatchAsync(JsonElement d)
        {
            var results = new List<object>();
            foreach (var request in d.GetProperty("requests").EnumerateArray())
            {
                var type = request.GetProperty("requestType").GetString()!;
                var (code, data) = this.Answer(type, request.TryGetProperty("requestData", out var requestData) ? requestData : null);
                results.Add(new
                {
                    requestType = type,
                    requestId = request.GetProperty("requestId").GetString(),
                    requestStatus = new { result = code == 100, code },
                    responseData = data,
                });
            }

            var json = JsonSerializer.Serialize(new { op = 9, d = new { requestId = d.GetProperty("requestId").GetString(), results } });
            await this._write.WaitAsync();
            try
            {
                await this._server.SendRawAsync(json);
            }
            finally
            {
                this._write.Release();
            }
        }

        private (int Code, object? Data) Answer(string type, JsonElement? data)
        {
            if (type == "GetVersion")
            {
                return (100, new
                {
                    obsVersion = "31.0.0",
                    obsWebSocketVersion = this.WebSocketVersion,
                    rpcVersion = 1,
                    availableRequests = Array.Empty<string>(),
                    supportedImageFormats = Array.Empty<string>(),
                    platform = "windows",
                    platformDescription = "Windows",
                });
            }

            Guid Target(string property)
            {
                return data is { } value && value.TryGetProperty(property, out var element) && element.TryGetGuid(out var guid) ? guid : Guid.Empty;
            }

            ReadRequest? request = type switch
            {
                "GetSceneList" => new ReadRequest(ReadKind.Scenes, OBSStudioClient.Enums.RequestType.GetSceneList, null, Guid.Empty),
                "GetInputList" => new ReadRequest(ReadKind.Inputs, OBSStudioClient.Enums.RequestType.GetInputList, null, Guid.Empty),
                "GetCurrentProgramScene" => new ReadRequest(ReadKind.Program, OBSStudioClient.Enums.RequestType.GetCurrentProgramScene, null, Guid.Empty),
                "GetStudioModeEnabled" => new ReadRequest(ReadKind.StudioMode, OBSStudioClient.Enums.RequestType.GetStudioModeEnabled, null, Guid.Empty),
                "GetSceneItemList" => new ReadRequest(ReadKind.Items, OBSStudioClient.Enums.RequestType.GetSceneItemList, null, Target("sceneUuid")),
                "GetGroupSceneItemList" => new ReadRequest(ReadKind.Items, OBSStudioClient.Enums.RequestType.GetGroupSceneItemList, null, Target("sceneUuid")),
                "GetInputMute" => new ReadRequest(ReadKind.AudioMute, OBSStudioClient.Enums.RequestType.GetInputMute, null, Target("inputUuid")),
                "GetInputVolume" => new ReadRequest(ReadKind.AudioVolume, OBSStudioClient.Enums.RequestType.GetInputVolume, null, Target("inputUuid")),
                _ => null,
            };
            if (request is null)
            {
                return (204, null);
            }

            lock (this._worldLock)
            {
                return this.World.Answer(request);
            }
        }
    }

    /// <summary>
    /// An <see cref="ObsStudio"/> that is connected to a <see cref="WorldResponder"/>.
    /// </summary>
    internal sealed class StudioRig : IAsyncDisposable
    {
        private readonly object _lock = new();
        private readonly List<ObsChangedEventArgs> _changes = [];
        private readonly List<string> _properties = [];

        private StudioRig(ObsStudioOptions? options)
        {
            this.Studio = new ObsStudio(options);
            this.Studio.Changed += (_, e) =>
            {
                lock (this._lock)
                {
                    this._changes.Add(e);
                }
            };
            this.Studio.PropertyChanged += (_, e) =>
            {
                lock (this._lock)
                {
                    this._properties.Add(e.PropertyName!);
                }
            };
        }

        public WorldResponder Responder { get; } = new();

        public ObsStudio Studio { get; }

        public ObsChangedEventArgs[] Changes
        {
            get
            {
                lock (this._lock)
                {
                    return this._changes.ToArray();
                }
            }
        }

        public string[] Properties
        {
            get
            {
                lock (this._lock)
                {
                    return this._properties.ToArray();
                }
            }
        }

        // A rig whose studio is not connected: the world is set up by the test, and the connection is made by the test.
        public static StudioRig Create(Action<FakeWorld>? setup = null, ObsStudioOptions? options = null)
        {
            var rig = new StudioRig(options);
            rig.Responder.World.AddScene("Scene");
            setup?.Invoke(rig.Responder.World);
            rig.Responder.Start();
            return rig;
        }

        public static async Task<StudioRig> ConnectedAsync(Action<FakeWorld>? setup = null, ObsStudioOptions? options = null)
        {
            var rig = Create(setup, options);
            await rig.Studio.ConnectAsync("127.0.0.1", rig.Responder.Port).WithTimeoutAsync(TimeSpan.FromSeconds(15));
            return rig;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return this.Studio.ConnectAsync("127.0.0.1", this.Responder.Port, cancellationToken: cancellationToken);
        }

        /// <summary>Waits until something is true, which a notification or the model that runs on its own thread makes so.</summary>
        public static async Task EventuallyAsync(Func<bool> condition, string what, TimeSpan? timeout = null)
        {
            var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
            while (!condition())
            {
                if (DateTime.UtcNow > until)
                {
                    throw new TimeoutException($"Not true in time: {what}.");
                }

                await Task.Delay(10);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await this.Studio.DisposeAsync();
            await this.Responder.DisposeAsync();
        }
    }
}
