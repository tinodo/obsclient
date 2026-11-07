namespace OBSStudioClient.Tests.Studio
{
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
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
        private readonly List<string> _requestLog = [];
        private readonly List<JsonElement> _held = [];
        private readonly CancellationTokenSource _stopping = new();
        private Task _loop = Task.CompletedTask;
        private int _batches;
        private int _handshakes;

        public FakeWorld World { get; } = new();

        public int Port => this._server.Port;

        /// <summary>Gets or sets the obs-websocket version that GetVersion answers.</summary>
        public string WebSocketVersion { get; set; } = "5.7.6";

        /// <summary>Gets or sets whether the batches of reads are answered.</summary>
        public bool AnswerBatches { get; set; } = true;

        /// <summary>
        /// Gets or sets what answers a request before the world does (the changes of the application are not in the world): null leaves the request to the world, a code of
        /// 0 means that the batch is not answered at all, a code of -1 holds the batch until <see cref="AnswerHeldAsync"/> answers it (as a success without data, which is what
        /// a change gets; the loop goes on reading meanwhile, so what the studio sends next shows in <see cref="RequestLog"/>), a code of -2 answers the request with an entry
        /// that is null (a malformed answer), and anything else is the code and the data of the answer. It runs on the thread that answers, so it can hold the answers back by
        /// blocking.
        /// </summary>
        public Func<string, JsonElement?, (int Code, object? Data)?>? OnRequest { get; set; }

        /// <summary>Gets the number of batches that <see cref="OnRequest"/> held, and that <see cref="AnswerHeldAsync"/> has not answered yet.</summary>
        public int HeldCount
        {
            get
            {
                lock (this._worldLock)
                {
                    return this._held.Count;
                }
            }
        }

        /// <summary>Gets the types of the requests that were received, in the order in which they were read.</summary>
        public string[] RequestLog
        {
            get
            {
                lock (this._worldLock)
                {
                    return this._requestLog.ToArray();
                }
            }
        }

        /// <summary>Gets the number of batches that were received.</summary>
        public int Batches => Volatile.Read(ref this._batches);

        /// <summary>Gets the number of connections that the server has taken (the handshake is done): the studio that reconnects makes another.</summary>
        public int Handshakes => Volatile.Read(ref this._handshakes);

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

        /// <summary>
        /// Answers the batches that <see cref="OnRequest"/> held, in the order in which they came. Every request of them is answered with success and without data, unless
        /// <paramref name="answer"/> says what the answer to a request type is.
        /// </summary>
        /// <param name="answer">What answers a request, by its type, or null.</param>
        public async Task AnswerHeldAsync(Func<string, (int Code, object? Data)>? answer = null)
        {
            JsonElement[] held;
            lock (this._worldLock)
            {
                held = this._held.ToArray();
                this._held.Clear();
            }

            foreach (var batch in held)
            {
                var results = batch.GetProperty("requests").EnumerateArray().Select(request =>
                {
                    var type = request.GetProperty("requestType").GetString()!;
                    var (code, data) = answer?.Invoke(type) ?? (100, (object?)null);
                    return (object)new
                    {
                        requestType = type,
                        requestId = request.GetProperty("requestId").GetString(),
                        requestStatus = new { result = code == 100, code },
                        responseData = data,
                    };
                }).ToArray();
                var json = JsonSerializer.Serialize(new { op = 9, d = new { requestId = batch.GetProperty("requestId").GetString(), results } });
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

        /// <summary>Ends the connection with a close frame that carries a close code of obs-websocket (4011 is what the Kick button of OBS Studio sends).</summary>
        public async Task CloseConnectionAsync(int code, string description)
        {
            await this._write.WaitAsync();
            try
            {
                await this._server.CloseWithCodeAsync(code, description);
            }
            finally
            {
                this._write.Release();
            }
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
                    Interlocked.Increment(ref this._handshakes);
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

            // OBS Studio stops a batch at its first refusal when the batch says so, and the answer holds the results of the requests that were run.
            var haltOnFailure = d.TryGetProperty("haltOnFailure", out var halt) && halt.ValueKind == JsonValueKind.True;
            foreach (var request in d.GetProperty("requests").EnumerateArray())
            {
                var type = request.GetProperty("requestType").GetString()!;
                JsonElement? data = request.TryGetProperty("requestData", out var requestData) ? requestData : null;
                lock (this._worldLock)
                {
                    this._requestLog.Add(type);
                }

                var custom = this.OnRequest?.Invoke(type, data);
                if (custom is { Code: 0 })
                {
                    // Nobody answers this batch.
                    return;
                }

                if (custom is { Code: -1 })
                {
                    // The batch is kept (the message goes away when the loop reads the next one): AnswerHeldAsync answers it.
                    lock (this._worldLock)
                    {
                        this._held.Add(d.Clone());
                    }

                    return;
                }

                if (custom is { Code: -2 })
                {
                    // A malformed answer: the entry of this request is null (the client hands such an answer on as it came).
                    results.Add(null!);
                    continue;
                }

                var (code, answer) = custom ?? this.Answer(type, data);
                results.Add(new
                {
                    requestType = type,
                    requestId = request.GetProperty("requestId").GetString(),
                    requestStatus = new { result = code == 100, code },
                    responseData = answer,
                });
                if (haltOnFailure && code != 100)
                {
                    break;
                }
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

            if (type == "GetSceneItemEnabled")
            {
                // The state that a toggle asks for: what the world holds, whatever the model has been told so far.
                var itemId = data is { } requested && requested.TryGetProperty("sceneItemId", out var idElement) && idElement.TryGetInt32(out var parsed) ? parsed : 0;
                lock (this._worldLock)
                {
                    var found = this.World.Items.TryGetValue(Target("sceneUuid"), out var items) ? items.Where(i => i.Id == itemId).Select(i => (bool?)i.Enabled).FirstOrDefault() : null;
                    return found is { } enabled ? (100, new { sceneItemEnabled = enabled }) : (600, null);
                }
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

        private StudioRig(ObsStudioOptions? options, ILoggerFactory? loggerFactory)
        {
            this.Studio = new ObsStudio(options, loggerFactory);
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
        public static StudioRig Create(Action<FakeWorld>? setup = null, ObsStudioOptions? options = null, ILoggerFactory? loggerFactory = null)
        {
            var rig = new StudioRig(options, loggerFactory);
            rig.Responder.World.AddScene("Scene");
            setup?.Invoke(rig.Responder.World);
            rig.Responder.Start();
            return rig;
        }

        public static async Task<StudioRig> ConnectedAsync(Action<FakeWorld>? setup = null, ObsStudioOptions? options = null, ILoggerFactory? loggerFactory = null)
        {
            var rig = Create(setup, options, loggerFactory);
            try
            {
                await rig.Studio.ConnectAsync("127.0.0.1", rig.Responder.Port).WithTimeoutAsync(TimeSpan.FromSeconds(15));
            }
            catch
            {
                // The caller never gets the rig, so it cannot dispose of it: its stand-in server and its model would go on running after the test failed.
                await rig.DisposeAsync();
                throw;
            }

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
