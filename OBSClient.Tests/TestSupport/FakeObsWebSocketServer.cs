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
    /// Request/RequestResponse, Event) to drive the scenarios under test, and lets a test script arbitrary raw
    /// frames and abrupt disconnects.
    /// </summary>
    internal sealed class FakeObsWebSocketServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly SemaphoreSlim _connectionSignal = new(0);
        private Task? _acceptLoop;
        private WebSocket? _socket;

        public FakeObsWebSocketServer()
        {
            Port = GetFreeTcpPort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }

        public int Port { get; }

        public void Start()
        {
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    var wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                    _socket = wsContext.WebSocket;
                    _connectionSignal.Release();
                }
            }
            catch (Exception) when (!_listener.IsListening)
            {
                // Listener was stopped; exit quietly.
            }
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
            var base64Secret = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(password + salt)));
            return Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(base64Secret + challenge)));
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
        /// Kills the connection without performing a WebSocket close handshake, simulating a crashed OBS process
        /// or an interrupted network link. See https://github.com/tinodo/obsclient/issues/17.
        /// </summary>
        public void AbortConnection()
        {
            _socket?.Abort();
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _socket?.Abort();
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
                _listener.Close();
                _connectionSignal.Dispose();
            }
        }
    }
}
