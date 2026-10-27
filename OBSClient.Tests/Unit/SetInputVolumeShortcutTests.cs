namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// <c>SetInputVolumeMul</c> and <c>SetInputVolumeDb</c> are shortcuts for <c>SetInputVolume</c>: each sends the one field it is
    /// named after, which is what obs-websocket wants (it refuses a request with both).
    /// </summary>
    public class SetInputVolumeShortcutTests
    {
        [Fact]
        public async Task SetInputVolumeMul_SendsOnlyTheMul()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.SetInputVolumeMul("Microphone", 0.5f));

            Assert.Equal("SetInputVolume", sent.RequestType);
            Assert.Equal(new[] { "inputName", "inputVolumeMul" }, sent.Fields);
            Assert.Equal(0.5f, sent["inputVolumeMul"].GetSingle());
        }

        [Fact]
        public async Task SetInputVolumeDb_SendsOnlyTheDb()
        {
            var uuid = Guid.NewGuid();

            var sent = await ObsClientHarness.SendAsync(client => client.SetInputVolumeDb(uuid, -6f));

            Assert.Equal("SetInputVolume", sent.RequestType);
            Assert.Equal(new[] { "inputUuid", "inputVolumeDb" }, sent.Fields);
            Assert.Equal(-6f, sent["inputVolumeDb"].GetSingle());
        }
    }
}
