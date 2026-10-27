namespace OBSStudioClient.Tests
{
    /// <summary>
    /// The arguments of <see cref="ObsClient.ConnectAsync"/> that are refused before any connection is tried.
    /// </summary>
    public class ConnectArgumentTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        public async Task ConnectAsync_WithAPortThatIsNotOne_IsRefused(int port)
        {
            using var client = new ObsClient();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ConnectAsync(hostname: "127.0.0.1", port: port));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(65535)]
        public async Task ConnectAsync_WithTheFirstOrTheLastPort_IsNotRefused(int port)
        {
            using var client = new ObsClient();

            // Nothing listens there, so the connection fails: but it is tried, which is what the port check allows.
            var connected = await client.ConnectAsync(hostname: "127.0.0.1", port: port);

            Assert.False(connected);
        }
    }
}
