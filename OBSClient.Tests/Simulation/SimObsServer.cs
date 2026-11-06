namespace OBSStudioClient.Tests.Simulation
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Channels;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// A WebSocket in front of a <see cref="SimObs"/>, so that a real <see cref="ObsClient"/> can connect to it: the Hello, Identify and
    /// Identified handshake (with a password if you want one), <c>Reidentify</c>, requests and batches, and the events that the connection
    /// subscribed to. Virtual time of the simulation follows the wall clock. It is built on a <see cref="TcpListener"/> on the loopback
    /// address, on a free port, with a handshake of its own, as <c>FakeObsWebSocketServer</c> is, so that <see cref="AbortConnection"/> can
    /// reset the connection.
    /// </summary>
    /// <remarks>
    /// One connection at a time: a new connection replaces the one before it, and gets a fresh handshake. What was due for a connection that
    /// is gone is dropped. Nothing is written to a connection from more than one place: a connection has one writer, and everything that
    /// is sent to it waits its turn in its queue.
    /// </remarks>
    internal sealed class SimObsServer : IAsyncDisposable
    {
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly TcpListener _listener;
        private readonly SimObs _sim;
        private readonly string? _password;
        private readonly object _gate = new();
        private readonly Stopwatch _wall = Stopwatch.StartNew();
        private readonly TimeSpan _origin;
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly List<(Func<bool> Condition, TaskCompletionSource Done)> _waiters = [];
        private readonly List<Task> _tasks = [];
        private Connection? _current;
        private int _connectionCount;
        private int _identifiedCount;
        private int _requestCount;
        private bool _started;

        /// <summary>Initializes a new instance of the <see cref="SimObsServer"/> class.</summary>
        /// <param name="sim">The simulated OBS Studio to serve. A new one with <see cref="SimQuirks.Real"/> is made when it is null.</param>
        /// <param name="password">The password that a client has to authenticate with, or null for a server without authentication.</param>
        public SimObsServer(SimObs? sim = null, string? password = null)
        {
            _sim = sim ?? new SimObs();
            _origin = _sim.Clock.Now;
            _password = password;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        /// <summary>Gets the port that the server listens on.</summary>
        public int Port { get; }

        /// <summary>
        /// Gets the simulated OBS Studio. It is not thread safe, and the server uses it from other threads: touch it through <see cref="Run(Action{SimObs})"/>.
        /// </summary>
        public SimObs Sim => _sim;

        /// <summary>Gets the number of connections that completed the WebSocket handshake, the one that is open included.</summary>
        public int ConnectionCount => Volatile.Read(ref _connectionCount);

        /// <summary>Gets the number of times that a connection identified itself.</summary>
        public int IdentifiedCount => Volatile.Read(ref _identifiedCount);

        /// <summary>Gets the number of requests and request batches that the simulation received from a connection.</summary>
        public int RequestCount => Volatile.Read(ref _requestCount);

        /// <summary>Gets a value indicating whether the connection that is open has identified itself.</summary>
        public bool IsIdentified => _current is { Identified: true } && _current.IsOpen;

        /// <summary>
        /// Gets the event subscription of the connection that is open and identified, or null. obs-websocket reads it as a 64 bit unsigned number, and
        /// this is the same 64 bits: a mask with the highest bit set reads as a negative number.
        /// </summary>
        public long? EventSubscriptions
        {
            get
            {
                var connection = _current;
                return connection is { Identified: true } && connection.IsOpen ? Interlocked.Read(ref connection.Subscriptions) : null;
            }
        }

        /// <summary>Starts to accept connections, and to send what the simulation has due.</summary>
        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            lock (_tasks)
            {
                _tasks.Add(Task.Run(() => AcceptLoopAsync(_stopping.Token)));
                _tasks.Add(Task.Run(() => PumpAsync(_stopping.Token)));
            }
        }

        /// <summary>Runs something on the simulation, which is locked meanwhile and brought to the time of the wall clock first.</summary>
        public void Run(Action<SimObs> action)
        {
            lock (_gate)
            {
                SyncClock();
                action(_sim);
            }

            _signal.Release();
        }

        /// <summary>Runs something on the simulation (see <see cref="Run(Action{SimObs})"/>) and returns what it comes to.</summary>
        public T Run<T>(Func<SimObs, T> action)
        {
            T result;
            lock (_gate)
            {
                SyncClock();
                result = action(_sim);
            }

            _signal.Release();
            return result;
        }

        /// <summary>Waits until a connection identified itself for the given number of times in all.</summary>
        public Task WaitForIdentifiedAsync(int count, TimeSpan timeout)
        {
            return WaitForAsync(() => IdentifiedCount >= count, timeout);
        }

        /// <summary>Waits until the simulation received the given number of requests and request batches in all.</summary>
        public Task WaitForRequestsAsync(int count, TimeSpan timeout)
        {
            return WaitForAsync(() => RequestCount >= count, timeout);
        }

        /// <summary>
        /// Kills the connection that is open without a WebSocket close handshake (a TCP reset), as a crashed OBS Studio or a lost link does.
        /// </summary>
        public void AbortConnection()
        {
            // Take the connection before anything is done to it: its client may reconnect at once, and the new connection must not be the
            // one that is closed here.
            var connection = _current;
            connection?.Abort();
        }

        public async ValueTask DisposeAsync()
        {
            _stopping.Cancel();
            try
            {
                _listener.Stop();
            }
            catch (SocketException)
            {
                // Stopped already.
            }

            _current?.Abort();
            Task[] tasks;
            lock (_tasks)
            {
                tasks = _tasks.ToArray();
            }

            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // What is left of the tasks ends with the cancellation, and this is the end of the server.
            }

            _signal.Dispose();
            _stopping.Dispose();
        }

        private static string ComputeAuthentication(string password, string salt, string challenge)
        {
            var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
        }

        private static string RandomBase64()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        private static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
        {
            var builder = new StringBuilder();
            var buffer = new byte[1];
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, 1), token).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("The connection closed before the WebSocket handshake completed.");
                }

                builder.Append((char)buffer[0]);
                if (builder.Length >= 4 && builder[^4] == '\r' && builder[^3] == '\n' && builder[^2] == '\r' && builder[^1] == '\n')
                {
                    return builder.ToString();
                }
            }
        }

        private static string? HeaderValue(string headers, string name)
        {
            foreach (var line in headers.Split("\r\n"))
            {
                var separator = line.IndexOf(':');
                if (separator > 0 && string.Equals(line[..separator].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return line[(separator + 1)..].Trim();
                }
            }

            return null;
        }

        private static int ReadIntent(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("d").GetProperty("eventIntent").GetInt32();
        }

        private static bool TryReadUnsigned(JsonObject data, string name, out ulong value)
        {
            return SimJson.TryGetUnsigned(data[name], out value);
        }

        private void SyncClock()
        {
            var now = _origin + _wall.Elapsed;
            if (now > _sim.Clock.Now)
            {
                _sim.AdvanceTo(now);
            }
        }

        private Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
        {
            TaskCompletionSource waiter;
            lock (_waiters)
            {
                if (condition())
                {
                    return Task.CompletedTask;
                }

                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((condition, waiter));
            }

            return waiter.Task.WaitAsync(timeout);
        }

        private void NotifyWaiters()
        {
            lock (_waiters)
            {
                foreach (var waiter in _waiters.Where(waiter => waiter.Condition()).ToArray())
                {
                    _waiters.Remove(waiter);
                    waiter.Done.TrySetResult();
                }
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    lock (_tasks)
                    {
                        _tasks.Add(Task.Run(() => ServeAsync(client, token)));
                    }
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                // The listener was stopped.
            }
        }

        private async Task ServeAsync(TcpClient client, CancellationToken token)
        {
            Connection? connection = null;
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var headers = await ReadHeadersAsync(stream, token).ConfigureAwait(false);
                var key = HeaderValue(headers, "Sec-WebSocket-Key") ?? throw new IOException("The handshake has no Sec-WebSocket-Key.");
                var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));
                var offered = HeaderValue(headers, "Sec-WebSocket-Protocol");
                var subProtocol = offered is not null && offered.Contains("obswebsocket.json", StringComparison.OrdinalIgnoreCase) ? "obswebsocket.json" : null;
                var response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                    + $"Sec-WebSocket-Accept: {accept}\r\n"
                    + (subProtocol is null ? string.Empty : $"Sec-WebSocket-Protocol: {subProtocol}\r\n")
                    + "\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), token).ConfigureAwait(false);

                var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, SubProtocol = subProtocol });
                connection = new Connection(client, socket, _password is null ? null : RandomBase64(), _password is null ? null : RandomBase64());
                Connection? replaced;
                lock (_gate)
                {
                    replaced = _current;
                    _current = connection;
                    connection.Number = Interlocked.Increment(ref _connectionCount);
                }

                // A connection that is replaced is gone: what OBS Studio had for it is dropped, and so is what it still has to send.
                replaced?.Abort();

                var writer = Task.Run(() => WriteLoopAsync(connection, token), token);
                connection.SendHello();
                await ReadLoopAsync(connection, token).ConfigureAwait(false);
                connection.Outgoing.Writer.TryComplete();
                await writer.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or SocketException or WebSocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                // The connection ended, or the server is stopping.
            }
            finally
            {
                connection?.Release();
                client.Dispose();
            }
        }

        private async Task WriteLoopAsync(Connection connection, CancellationToken token)
        {
            try
            {
                await foreach (var message in connection.Outgoing.Reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    if (message.CloseCode is { } code)
                    {
                        await connection.Socket.CloseOutputAsync((WebSocketCloseStatus)code, message.CloseReason, token).ConfigureAwait(false);
                        return;
                    }

                    await connection.Socket.SendAsync(Encoding.UTF8.GetBytes(message.Text!), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is IOException or SocketException or WebSocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                // The connection ended.
            }
        }

        private async Task ReadLoopAsync(Connection connection, CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            using var message = new MemoryStream();
            while (connection.IsOpen && !token.IsCancellationRequested)
            {
                var result = await connection.Socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // The acknowledgement goes through the writer, the only one that sends: it can be sending a message at this moment, and a
                    // WebSocket does not send two at once.
                    connection.Close((int)WebSocketCloseStatus.NormalClosure, "Goodbye");
                    return;
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                var isText = result.MessageType == WebSocketMessageType.Text;
                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                if (!isText)
                {
                    connection.Close(SimCloseCode.MessageDecodeError, "Your session encoding is set to Json, but a binary message was received.");
                    continue;
                }

                Handle(connection, text);
            }
        }

        private void Handle(Connection connection, string text)
        {
            var envelope = SimProtocol.Read(text, out var violation);
            if (envelope is null)
            {
                connection.Close(violation!.CloseCode, violation.Reason);
                return;
            }

            if (!connection.Identified && envelope.Op != SimOp.Identify)
            {
                connection.Close(SimCloseCode.NotIdentified, "You attempted to send a non-Identify message while not identified.");
                return;
            }

            switch (envelope.Op)
            {
                case SimOp.Identify:
                    HandleIdentify(connection, envelope.Data);
                    break;
                case SimOp.Reidentify:
                    HandleReidentify(connection, envelope.Data);
                    break;
                default:
                    HandleRequest(connection, envelope, text);
                    break;
            }
        }

        private void HandleIdentify(Connection connection, JsonObject data)
        {
            if (connection.Identified)
            {
                connection.Close(SimCloseCode.AlreadyIdentified, "You are already Identified with the obs-websocket server.");
                return;
            }

            if (connection.Challenge is not null)
            {
                if (!data.ContainsKey("authentication"))
                {
                    connection.Close(SimCloseCode.AuthenticationFailed, "Your payload's data is missing an `authentication` string, however authentication is required.");
                    return;
                }

                if (data["authentication"] is not JsonValue { } authentication || authentication.GetValueKind() != JsonValueKind.String)
                {
                    connection.Close(SimCloseCode.AuthenticationFailed, "Your `authentication` field is not a string.");
                    return;
                }

                if (authentication.GetValue<string>() != ComputeAuthentication(_password!, connection.Salt!, connection.Challenge))
                {
                    connection.Close(SimCloseCode.AuthenticationFailed, "Authentication failed.");
                    return;
                }
            }

            if (!data.ContainsKey("rpcVersion"))
            {
                connection.Close(SimCloseCode.MissingDataField, "Your payload's data is missing an `rpcVersion`.");
                return;
            }

            if (!TryReadUnsigned(data, "rpcVersion", out var rpcVersion))
            {
                connection.Close(SimCloseCode.InvalidDataFieldType, "Your `rpcVersion` is not an unsigned number.");
                return;
            }

            if (rpcVersion != 1)
            {
                connection.Close(SimCloseCode.UnsupportedRpcVersion, "Your requested RPC version is not supported by this server.");
                return;
            }

            if (!ApplySubscriptions(connection, data))
            {
                return;
            }

            // The flag, the count and the Identified message are one step, under the lock that Dispatch takes: whoever sees the count can make a
            // change, and the change is sent to this connection, after the Identified message. The count is up before the client can know that it is
            // identified, so that whoever waits for the client sees both.
            lock (connection.Gate)
            {
                connection.Identified = true;
                Interlocked.Increment(ref _identifiedCount);
                connection.Send(Serialize(Obj(("d", Obj(("negotiatedRpcVersion", 1))), ("op", SimOp.Identified))));
            }

            NotifyWaiters();
        }

        private void HandleReidentify(Connection connection, JsonObject data)
        {
            // ASSUMPTION: without eventSubscriptions the subscription stays as it is (SetSessionParameters of obs-websocket 5.7.6). The facts
            // file infers a reset to All, unproven. A Reidentify without eventSubscriptions against a real OBS Studio, with a client that
            // subscribed to less than All, would tell.
            if (!ApplySubscriptions(connection, data))
            {
                return;
            }

            connection.Send(Serialize(Obj(("d", Obj(("negotiatedRpcVersion", 1))), ("op", SimOp.Identified))));
        }

        private static bool ApplySubscriptions(Connection connection, JsonObject data)
        {
            if (!data.ContainsKey("eventSubscriptions"))
            {
                return true;
            }

            if (!TryReadUnsigned(data, "eventSubscriptions", out var subscriptions))
            {
                connection.Close(SimCloseCode.InvalidDataFieldType, "Your `eventSubscriptions` is not an unsigned number.");
                return false;
            }

            Interlocked.Exchange(ref connection.Subscriptions, unchecked((long)subscriptions));
            return true;
        }

        private void HandleRequest(Connection connection, SimEnvelope envelope, string text)
        {
            IReadOnlyList<SimProtocolViolation> violations;
            lock (_gate)
            {
                // The connection is attached to the request, and comes back with the response: the ID of a request is the client's own,
                // of any kind, and another connection may use the same one.
                SyncClock();
                _sim.Receive(text, connection.Number);
                violations = _sim.TakeViolations();
            }

            if (violations.Count == 0 && envelope.Op is SimOp.Request or SimOp.RequestBatch)
            {
                Interlocked.Increment(ref _requestCount);
                NotifyWaiters();
            }

            _signal.Release();
            if (violations.Count > 0)
            {
                connection.Close(violations[0].CloseCode, violations[0].Reason);
            }
        }

        private async Task PumpAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                IReadOnlyList<SimMessage> due;
                TimeSpan? wait;
                lock (_gate)
                {
                    SyncClock();
                    due = _sim.TakeDue();
                    var next = _sim.NextDeadline;
                    wait = next is null ? null : next.Value - (_origin + _wall.Elapsed);
                }

                Dispatch(due);

                try
                {
                    if (wait is null)
                    {
                        await _signal.WaitAsync(token).ConfigureAwait(false);
                    }
                    else if (wait > TimeSpan.Zero)
                    {
                        await _signal.WaitAsync(wait.Value, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void Dispatch(IReadOnlyList<SimMessage> due)
        {
            var connection = _current;
            if (due.Count == 0 || connection is null)
            {
                return;
            }

            foreach (var message in due)
            {
                // The check and the send are one step with the publication of Identified (HandleIdentify): an event can only be queued after the
                // Identified message.
                lock (connection.Gate)
                {
                    if (!connection.Identified)
                    {
                        continue;
                    }

                    if (message.Op == SimOp.Event)
                    {
                        if ((Interlocked.Read(ref connection.Subscriptions) & ReadIntent(message.Json)) != 0)
                        {
                            connection.Send(message.Json);
                        }

                        continue;
                    }

                    // A response goes to the connection that asked for it, if that is the one that is open.
                    if (message.Tag is long owner && connection.Number == owner)
                    {
                        connection.Send(message.Json);
                    }
                }
            }
        }

        private sealed record Frame(string? Text, int? CloseCode, string? CloseReason);

        private sealed class Connection
        {
            private readonly TcpClient _client;
            private int _closing;
            private int _released;

            public Connection(TcpClient client, WebSocket socket, string? challenge, string? salt)
            {
                _client = client;
                Socket = socket;
                Challenge = challenge;
                Salt = salt;
            }

            public WebSocket Socket { get; }

            public string? Challenge { get; }

            public string? Salt { get; }

            public Channel<Frame> Outgoing { get; } = Channel.CreateUnbounded<Frame>(new UnboundedChannelOptions { SingleReader = true });

            public long Number { get; set; }

            // Taken by whoever publishes Identified and by whoever queues a message for the connection (see Dispatch).
            public object Gate { get; } = new();

            public volatile bool Identified;

            public long Subscriptions = SimIntent.All;

            public bool IsOpen => Volatile.Read(ref _closing) == 0 && Socket.State is WebSocketState.Open or WebSocketState.CloseReceived;

            public void SendHello()
            {
                var members = new List<(string Name, JsonNode? Value)>
                {
                    ("obsStudioVersion", "33.0.0"),
                    ("obsWebSocketVersion", "5.7.6"),
                    ("rpcVersion", 1),
                };
                if (Challenge is not null)
                {
                    members.Add(("authentication", Obj(("challenge", Challenge), ("salt", Salt))));
                }

                Send(Serialize(Obj(("d", Obj(members.ToArray())), ("op", SimOp.Hello))));
            }

            public void Send(string text)
            {
                Outgoing.Writer.TryWrite(new Frame(text, null, null));
            }

            // Ends the connection with a close code of obs-websocket: after what is waiting to be sent, and no message is read any more.
            public void Close(int code, string reason)
            {
                if (Interlocked.Exchange(ref _closing, 1) == 0)
                {
                    Outgoing.Writer.TryWrite(new Frame(null, code, TruncateReason(reason)));
                }
            }

            // The reason of a close frame is at most 123 bytes of UTF-8 (a control frame holds 125 bytes, two of which are the code).
            private static string TruncateReason(string reason)
            {
                var bytes = Encoding.UTF8.GetByteCount(reason);
                while (bytes > 123)
                {
                    reason = reason[..^1];
                    bytes = Encoding.UTF8.GetByteCount(reason);
                }

                return reason;
            }

            // Lets go of the connection after a close handshake or the end of the peer: what was sent is delivered first.
            public void Release()
            {
                Interlocked.Exchange(ref _closing, 1);
                Outgoing.Writer.TryComplete();
                if (Interlocked.Exchange(ref _released, 1) != 0)
                {
                    return;
                }

                try
                {
                    Socket.Dispose();
                    _client.Dispose();
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
                {
                    // It is being torn down anyway.
                }
            }

            // Resets the connection: the peer gets no close handshake. A connection that was released is gone already.
            public void Abort()
            {
                Interlocked.Exchange(ref _closing, 1);
                Outgoing.Writer.TryComplete();
                if (Interlocked.Exchange(ref _released, 1) != 0)
                {
                    return;
                }

                try
                {
                    // The TcpClient gives no socket once it is disposed, which a release that is running at the same moment can do.
                    var tcp = _client.Client;
                    Socket.Abort();
                    if (tcp is not null)
                    {
                        tcp.LingerState = new LingerOption(true, 0);
                    }

                    _client.Close();
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
                {
                    // It is being torn down anyway.
                }
            }
        }
    }
}
