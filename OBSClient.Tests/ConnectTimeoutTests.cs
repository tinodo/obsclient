namespace OBSStudioClient.Tests
{
    using System.Net;
    using System.Net.Sockets;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit;

    /// <summary>
    /// How long the client gives a connection to open. The tests of the assembly give it far more than the default (see <see cref="TestAssemblyInitializer"/>); these
    /// tests give one client little, and check what the attempt does when that time is up.
    /// </summary>
    public class ConnectTimeoutTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task ConnectAsync_ReturnsFalse_WhenTheServerAcceptsTheConnectionButNeverUpgradesIt()
        {
            // The operating system completes the connection of a listener that nobody accepts, so the client sends its request for the upgrade to a WebSocket, and
            // gets no answer: OBS Studio that is too busy to answer, or something that is not OBS Studio.
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var client = new ObsClient { ConnectTimeout = TimeSpan.FromMilliseconds(300) };

            var connected = await client.ConnectAsync(hostname: "127.0.0.1", port: ((IPEndPoint)listener.LocalEndpoint).Port).WithTimeoutAsync(Timeout);

            Assert.False(connected);
            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
        }

        [Fact]
        public void AClient_GivesTheConnectionTheTimeThatTheTestsAllow_ByDefault()
        {
            using var client = new ObsClient();

            Assert.Equal(ObsClient.DefaultConnectTimeout, client.ConnectTimeout);
            Assert.True(client.ConnectTimeout > TimeSpan.FromSeconds(2), "The tests allow a connection more than the two seconds of a client: a busy machine can need them.");
        }
    }
}
