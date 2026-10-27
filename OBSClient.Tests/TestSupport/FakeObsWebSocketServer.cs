namespace OBSStudioClient.Tests.TestSupport
{
    using System.Net;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// A minimal, single-connection obs-websocket server used to exercise <see cref="ObsClient"/> end-to-end
    /// without a real OBS Studio instance. It speaks just enough of the protocol (Hello/Identify/Identified,
    /// Request/RequestResponse, Event) to drive the scenarios under test.
    /// </summary>
    /// <remarks>
    /// Built directly on <see cref="TcpListener"/> plus a hand-rolled RFC 6455 handshake (rather than
    /// <see cref="System.Net.HttpListener"/>) specifically so <see cref="AbortConnection"/> can reach the raw
    /// socket: an <see cref="HttpListener"/>-backed <see cref="WebSocket.Abort"/> does not reliably sever the
    /// underlying TCP connection on Linux, which made the abrupt-disconnect regression tests
    /// (https://github.com/tinodo/obsclient/issues/17) flaky in Linux CI. Setting <see cref="LingerOption"/> to
    /// an abortive close and closing the socket directly sends a TCP RST on every platform.
    /// </remarks>
    internal sealed class FakeObsWebSocketServer : IAsyncDisposable
    {
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly TcpListener _listener;
        private readonly SemaphoreSlim _connectionSignal = new(0);
        private Task? _acceptLoop;
        private TcpClient? _tcpClient;
        private WebSocket? _socket;
        private bool _stopped;

        /// <summary>Initializes a new instance of the <see cref="FakeObsWebSocketServer"/> class.</summary>
        /// <param name="receiveBufferSize">
        /// The receive buffer of the connections that the server accepts, in bytes, or null for the default. A small one makes the writes
        /// of a client block soon when nothing reads from this server, which is what the tests of a blocked write need.
        /// </param>
        public FakeObsWebSocketServer(int? receiveBufferSize = null)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            if (receiveBufferSize is { } size)
            {
                // Before the listener starts: the connections that it accepts get the same buffer, and so the same small window.
                _listener.Server.ReceiveBufferSize = size;
            }

            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public int Port { get; }

        public void Start()
        {
            _acceptLoop = AcceptLoopAsync();
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_stopped)
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    client.NoDelay = true;
                    var stream = client.GetStream();
                    await PerformHandshakeAsync(stream).ConfigureAwait(false);

                    _tcpClient = client;
                    _socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true });
                    _connectionSignal.Release();
                }
            }
            catch (Exception) when (_stopped)
            {
                // Listener was stopped; exit quietly.
            }
        }

        private static async Task PerformHandshakeAsync(NetworkStream stream)
        {
            var headers = await ReadHttpHeadersAsync(stream).ConfigureAwait(false);
            var key = ExtractHeaderValue(headers, "Sec-WebSocket-Key")
                ?? throw new InvalidOperationException("Handshake request was missing Sec-WebSocket-Key.");
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));

            var response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
            var responseBytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(responseBytes).ConfigureAwait(false);
        }

        private static async Task<string> ReadHttpHeadersAsync(NetworkStream stream)
        {
            var builder = new StringBuilder();
            var buffer = new byte[1];
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, 1)).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("Connection closed before the WebSocket handshake completed.");
                }

                builder.Append((char)buffer[0]);
                if (builder.Length >= 4 &&
                    builder[^4] == '\r' && builder[^3] == '\n' && builder[^2] == '\r' && builder[^1] == '\n')
                {
                    return builder.ToString();
                }
            }
        }

        private static string? ExtractHeaderValue(string headers, string name)
        {
            foreach (var line in headers.Split("\r\n"))
            {
                var separatorIndex = line.IndexOf(':');
                if (separatorIndex > 0 && string.Equals(line[..separatorIndex].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return line[(separatorIndex + 1)..].Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Waits for a client to connect and returns once the WebSocket upgrade has completed.
        /// </summary>
        public async Task WaitForConnectionAsync(TimeSpan timeout)
        {
            if (!await _connectionSignal.WaitAsync(timeout).ConfigureAwait(false))
            {
                throw new TimeoutException("No client connected to the fake OBS server in time.");
            }
        }

        /// <summary>
        /// Performs the Hello/Identify/Identified handshake with the already-connected client.
        /// </summary>
        /// <param name="password">When set, requires authentication and verifies the client computed the correct hash.</param>
        /// <param name="rpcVersion">The RPC version to advertise and negotiate.</param>
        public async Task<JsonDocument> HandshakeAsync(string? password = null, int rpcVersion = 1, TimeSpan? timeout = null)
        {
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
            await WaitForConnectionAsync(effectiveTimeout).ConfigureAwait(false);

            const string salt = "testSaltValue1234567890==";
            const string challenge = "testChallengeValue1234567890==";

            object hello = password is null
                ? new { op = 0, d = new { obsStudioVersion = "30.2.2", obsWebSocketVersion = "5.7.6", rpcVersion } }
                : new
                {
                    op = 0,
                    d = new
                    {
                        obsStudioVersion = "30.2.2",
                        obsWebSocketVersion = "5.7.6",
                        rpcVersion,
                        authentication = new { challenge, salt },
                    },
                };

            await SendJsonAsync(hello).ConfigureAwait(false);
            var identify = await ReceiveJsonAsync(effectiveTimeout).ConfigureAwait(false);

            if (password != null)
            {
                var expected = ComputeExpectedAuthentication(password, salt, challenge);
                var actual = identify.RootElement.GetProperty("d").GetProperty("authentication").GetString();
                if (actual != expected)
                {
                    await SendCloseAsync(4009, "AuthenticationFailed").ConfigureAwait(false);
                    throw new InvalidOperationException($"Client authentication string did not match. Expected '{expected}', got '{actual}'.");
                }
            }

            await SendJsonAsync(new { op = 2, d = new { negotiatedRpcVersion = rpcVersion } }).ConfigureAwait(false);
            return identify;
        }

        /// <summary>
        /// Computes the obs-websocket authentication string the same way a real server would, so the handshake
        /// helper can assert the client implements https://github.com/obsproject/obs-websocket's documented algorithm.
        /// </summary>
        private static string ComputeExpectedAuthentication(string password, string salt, string challenge)
        {
            var base64Secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(base64Secret + challenge)));
        }

        /// <summary>
        /// Waits for and returns the next message sent by the client (e.g. a Request op-code message).
        /// </summary>
        public async Task<JsonDocument> ReceiveJsonAsync(TimeSpan timeout)
        {
            if (_socket is null)
            {
                await WaitForConnectionAsync(timeout).ConfigureAwait(false);
            }

            using var cts = new CancellationTokenSource(timeout);
            var buffer = new byte[8192];
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket!.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException("Client closed the connection while a message was expected.");
                }

                stream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            stream.Position = 0;
            return await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for the next client Request message (op 6) and returns its requestType, requestId and requestData.
        /// </summary>
        public async Task<(string RequestType, string RequestId, JsonElement? RequestData)> ReceiveRequestAsync(TimeSpan timeout)
        {
            using var doc = await ReceiveJsonAsync(timeout).ConfigureAwait(false);
            var d = doc.RootElement.GetProperty("d");
            var requestType = d.GetProperty("requestType").GetString()!;
            var requestId = d.GetProperty("requestId").GetString()!;
            JsonElement? requestData = d.TryGetProperty("requestData", out var rd) ? rd.Clone() : null;
            return (requestType, requestId, requestData);
        }

        /// <summary>
        /// Sends a successful RequestResponse (op 7) for the given request.
        /// </summary>
        public Task SendRequestSuccessAsync(string requestType, string requestId, object? responseData = null)
        {
            return SendJsonAsync(new
            {
                op = 7,
                d = new
                {
                    requestType,
                    requestId,
                    requestStatus = new { result = true, code = 100 },
                    responseData,
                },
            });
        }

        /// <summary>
        /// Sends a failed RequestResponse (op 7) for the given request.
        /// </summary>
        public Task SendRequestErrorAsync(string requestType, string requestId, int code, string? comment = null)
        {
            return SendJsonAsync(new
            {
                op = 7,
                d = new
                {
                    requestType,
                    requestId,
                    requestStatus = new { result = false, code, comment },
                },
            });
        }

        /// <summary>
        /// Sends an Event message (op 5) with an arbitrary, possibly client-unrecognized, event type.
        /// </summary>
        public Task SendEventAsync(string eventType, object? eventData = null, int eventIntent = 1)
        {
            return SendJsonAsync(new { op = 5, d = new { eventType, eventIntent, eventData } });
        }

        /// <summary>
        /// Sends a raw, already-serialized text frame, for tests that need to send intentionally malformed payloads.
        /// </summary>
        public Task SendRawAsync(string json)
        {
            return SendBytesAsync(Encoding.UTF8.GetBytes(json));
        }

        private async Task SendJsonAsync(object payload)
        {
            await SendBytesAsync(JsonSerializer.SerializeToUtf8Bytes(payload)).ConfigureAwait(false);
        }

        private async Task SendBytesAsync(byte[] bytes)
        {
            if (_socket is null)
            {
                await WaitForConnectionAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }

            await _socket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }

        private Task SendCloseAsync(int closeStatus, string description)
        {
            return _socket!.CloseAsync((WebSocketCloseStatus)closeStatus, description, CancellationToken.None);
        }

        /// <summary>
        /// Ends the connection with a WebSocket close frame that carries one of obs-websocket's own close codes, the way
        /// OBS Studio does when it ends a session: 4009 when the password is wrong, 4011 for the Kick button of its session list.
        /// </summary>
        public Task CloseWithCodeAsync(int closeStatus, string description)
        {
            return SendCloseAsync(closeStatus, description);
        }

        /// <summary>
        /// Kills the connection without performing a WebSocket close handshake, simulating a crashed OBS process
        /// or an interrupted network link. See https://github.com/tinodo/obsclient/issues/17.
        /// </summary>
        /// <remarks>
        /// Forces an abortive close (TCP RST) on the raw socket, in addition to <see cref="WebSocket.Abort"/>,
        /// so the peer's pending read reliably fails on every platform instead of merely hanging.
        /// </remarks>
        public void AbortConnection()
        {
            // Take the connection that is to be killed before anything is done to it. Its client may reconnect at once (a
            // test of a handler that does so), and the accept loop then replaces these fields while this method is still
            // running: the connection that was made for the reconnect must not be the one that is closed below.
            var socket = _socket;
            var tcpClient = _tcpClient;

            socket?.Abort();
            try
            {
                if (tcpClient != null)
                {
                    tcpClient.Client.LingerState = new LingerOption(true, 0);
                    tcpClient.Close();
                }
            }
            catch
            {
                // Best-effort: the connection is being torn down anyway.
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stopped = true;
            try
            {
                _socket?.Abort();
                _tcpClient?.Close();
                _listener.Stop();
                if (_acceptLoop != null)
                {
                    await Task.WhenAny(_acceptLoop, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
            finally
            {
                _connectionSignal.Dispose();
            }
        }
    }
}
