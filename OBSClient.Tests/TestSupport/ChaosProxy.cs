namespace OBSStudioClient.Tests.TestSupport
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// A TCP proxy between a client and a server (a real OBS Studio, or the fake one) that makes the connection go wrong the way a network
    /// and a busy machine do: it <b>cuts</b> the connection with a reset, as when the process at the other end dies, it <b>stalls</b> it, so
    /// that nothing arrives for a while and then everything at once, and it <b>refuses</b> it, as a server that is still starting does.
    /// Nothing that is sent is changed: both sides see the same bytes, only not always, and not always in time.
    /// </summary>
    /// <remarks>
    /// Nothing of OBS Studio has to be restarted for any of it, which is what makes it fit for a test of a real OBS Studio: the server
    /// keeps running, and the proxy is what comes and goes.
    /// </remarks>
    internal sealed class ChaosProxy : IAsyncDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly ConcurrentDictionary<Link, byte> _links = new();
        private readonly CancellationTokenSource _stopped = new();
        private readonly object _stall = new();
        private Task _flowing = Task.CompletedTask;
        private volatile bool _refusing;
        private Task? _accepting;
        private int _accepted;
        private int _cut;

        /// <summary>Initializes a new instance of the <see cref="ChaosProxy"/> class.</summary>
        /// <param name="host">The host of the server that the connections are passed on to.</param>
        /// <param name="port">The port of the server.</param>
        public ChaosProxy(string host, int port)
        {
            this._host = host;
            this._port = port;
            this._listener.Start();
            this.Port = ((IPEndPoint)this._listener.LocalEndpoint).Port;
        }

        /// <summary>Gets the port of the proxy, on the loopback address: where the client connects to.</summary>
        public int Port { get; }

        /// <summary>Gets the number of connections that the proxy has accepted, those that it refused included.</summary>
        public int Accepted => Volatile.Read(ref this._accepted);

        /// <summary>Gets the number of connections that were cut by <see cref="Cut"/>.</summary>
        public int ConnectionsCut => Volatile.Read(ref this._cut);

        /// <summary>Gets the number of connections that are open now.</summary>
        public int Open => this._links.Count;

        /// <summary>Starts accepting connections.</summary>
        public void Start() => this._accepting = this.AcceptAsync();

        /// <summary>Cuts every connection that is open with a reset (nothing is sent to say goodbye), as when the process at the other end dies.</summary>
        /// <returns>The number of connections that were cut.</returns>
        public int Cut()
        {
            var cut = 0;
            foreach (var link in this._links.Keys)
            {
                if (link.Abort())
                {
                    cut++;
                }
            }

            Interlocked.Add(ref this._cut, cut);
            return cut;
        }

        /// <summary>Holds back what is sent, in both directions, until the stall is over: it all arrives then.</summary>
        /// <param name="duration">How long. A stall that is under way is not extended or restarted.</param>
        public void Stall(TimeSpan duration)
        {
            lock (this._stall)
            {
                if (!this._flowing.IsCompleted)
                {
                    return;
                }

                var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                this._flowing = released.Task;
                _ = Task.Delay(duration).ContinueWith(_ => released.TrySetResult(), TaskScheduler.Default);
            }
        }

        /// <summary>Makes the proxy accept connections and reset them at once, as a server that is starting up does, or lets it pass them on again.</summary>
        /// <param name="refuse">Whether to refuse.</param>
        public void Refuse(bool refuse) => this._refusing = refuse;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            this._stopped.Cancel();
            this._listener.Stop();
            this.Cut();
            if (this._accepting is not null)
            {
                await Task.WhenAny(this._accepting, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
            }

            this._stopped.Dispose();
        }

        private Task Flowing
        {
            get
            {
                lock (this._stall)
                {
                    return this._flowing;
                }
            }
        }

        private static void Reset(TcpClient client)
        {
            try
            {
                client.Client.LingerState = new LingerOption(true, 0);
            }
            catch (Exception)
            {
                // Already closed.
            }

            client.Dispose();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!this._stopped.IsCancellationRequested)
                {
                    var client = await this._listener.AcceptTcpClientAsync(this._stopped.Token).ConfigureAwait(false);
                    Interlocked.Increment(ref this._accepted);
                    _ = this.ServeAsync(client);
                }
            }
            catch (Exception) when (this._stopped.IsCancellationRequested)
            {
                // Stopped.
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            if (this._refusing)
            {
                Reset(client);
                return;
            }

            TcpClient? server = null;
            try
            {
                client.NoDelay = true;
                server = new TcpClient { NoDelay = true };
                await server.ConnectAsync(this._host, this._port, this._stopped.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The server cannot be reached, or the proxy was stopped: the client sees its connection end.
                server?.Dispose();
                Reset(client);
                return;
            }

            var link = new Link(client, server);
            this._links.TryAdd(link, 0);
            try
            {
                await Task.WhenAll(this.PumpAsync(link, client, server), this.PumpAsync(link, server, client)).ConfigureAwait(false);
            }
            finally
            {
                this._links.TryRemove(link, out _);
                link.Dispose();
            }
        }

        private async Task PumpAsync(Link link, TcpClient from, TcpClient to)
        {
            var buffer = new byte[16 * 1024];
            try
            {
                var source = from.GetStream();
                var target = to.GetStream();
                while (true)
                {
                    var read = await source.ReadAsync(buffer, this._stopped.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        // This side ended the connection in an orderly way: the other side is told, and what it still has to say goes on.
                        link.PassOnTheEnd(to);
                        return;
                    }

                    await this.Flowing.ConfigureAwait(false);
                    await target.WriteAsync(buffer.AsMemory(0, read), this._stopped.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // Cut, or one of the two ended badly: the connection is over for both.
                link.Abort();
            }
        }

        /// <summary>The two sockets of one connection: the client's side of the proxy, and the proxy's connection to the server.</summary>
        private sealed class Link(TcpClient client, TcpClient server) : IDisposable
        {
            private int _aborted;

            /// <summary>Resets both sockets.</summary>
            /// <returns><see langword="true"/> when this call did it; <see langword="false"/> when it was done already.</returns>
            public bool Abort()
            {
                if (Interlocked.Exchange(ref this._aborted, 1) != 0)
                {
                    return false;
                }

                Reset(client);
                Reset(server);
                return true;
            }

            /// <summary>Tells the side that a socket belongs to that nothing more comes.</summary>
            /// <param name="side">The socket whose side to tell.</param>
            public void PassOnTheEnd(TcpClient side)
            {
                try
                {
                    side.Client.Shutdown(SocketShutdown.Send);
                }
                catch (Exception)
                {
                    // Gone already.
                }
            }

            public void Dispose()
            {
                client.Dispose();
                server.Dispose();
            }
        }
    }
}
