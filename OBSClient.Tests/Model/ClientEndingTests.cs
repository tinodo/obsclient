namespace OBSStudioClient.Tests.Model
{
    using System.Collections.Concurrent;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// How <see cref="ObsClient"/> ends a connection for <see cref="OBSStudioClient.Model.ObsModel"/>: by the number of the connection, so that a connection that was
    /// made in its place is not touched, and with the notification of the change of <see cref="ObsClient.AutoReconnect"/> left to the caller.
    /// </summary>
    public class ClientEndingTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static async Task<int> ConnectAsync(ObsClient client, WorldResponder responder, ConcurrentQueue<int> sessions)
        {
            Assert.True(await client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: responder.Port).WithTimeoutAsync(Timeout));
            await ModelRig.EventuallyAsync(() => !sessions.IsEmpty, "the start of the connection to be reported");
            return sessions.Last();
        }

        private static ConcurrentQueue<int> Watch(ObsClient client)
        {
            var sessions = new ConcurrentQueue<int>();
            Assert.True(client.TryAttachIngressTap(item =>
            {
                if (item.Kind == IngressKind.SessionStarted)
                {
                    sessions.Enqueue(item.SessionId);
                }
            }));
            return sessions;
        }

        [Fact]
        public async Task EndSession_EndsTheConnectionWithThatNumber_AndNoOther()
        {
            await using var responder = new WorldResponder();
            responder.Start();
            using var client = new ObsClient();
            var sessions = Watch(client);
            var session = await ConnectAsync(client, responder, sessions);

            Assert.False(client.EndSession(session + 1));
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            Assert.True(client.AutoReconnect);

            Assert.True(client.EndSession(session));
            await ModelRig.EventuallyAsync(() => client.ConnectionState == ConnectionState.Disconnected, "the connection to be closed");
            Assert.False(client.AutoReconnect);
        }

        [Fact]
        public async Task EndSession_ForAConnectionThatWasReplaced_LeavesTheNewOneAlone()
        {
            await using var responder = new WorldResponder();
            responder.Start();
            using var client = new ObsClient();
            var sessions = Watch(client);
            var first = await ConnectAsync(client, responder, sessions);
            client.Disconnect();
            await ModelRig.EventuallyAsync(() => client.ConnectionState == ConnectionState.Disconnected, "the first connection to be closed");
            var second = await ConnectAsync(client, responder, sessions);
            Assert.NotEqual(first, second);

            // The delayed end of the first connection must not end the connection that was made in its place, nor switch off what that one was made with.
            Assert.False(client.EndSession(first));

            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            Assert.True(client.AutoReconnect);
        }

        [Fact]
        public void EndSession_WithoutAConnection_DoesNothing()
        {
            using var client = new ObsClient();

            Assert.False(client.EndSession(1));
        }

        [Fact]
        public async Task DisconnectDeferred_EndsTheConnectionAtOnce_AndLeavesTheNotificationOfAutoReconnectToTheCaller()
        {
            await using var responder = new WorldResponder();
            responder.Start();
            using var client = new ObsClient();
            var sessions = Watch(client);
            await ConnectAsync(client, responder, sessions);
            var raised = new ConcurrentQueue<string>();
            client.PropertyChanged += (_, e) => raised.Enqueue(e.PropertyName!);

            var raise = client.DisconnectDeferred();

            // The setting is changed and the connection is ended now. A handler runs only when the caller raises the notification, on the thread of the caller.
            Assert.NotNull(raise);
            Assert.False(client.AutoReconnect);
            await ModelRig.EventuallyAsync(() => client.ConnectionState == ConnectionState.Disconnected, "the connection to be closed");
            Assert.DoesNotContain(nameof(ObsClient.AutoReconnect), raised);
            raise();
            Assert.Contains(nameof(ObsClient.AutoReconnect), raised);
        }
    }
}
