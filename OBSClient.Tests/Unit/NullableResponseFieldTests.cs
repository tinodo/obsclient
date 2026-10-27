namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// Where obs-websocket documents that a field is null (a scene without a transition override, a record that has not stopped
    /// yet), the field is read as null. A real OBS Studio answers GetSceneSceneTransitionOverride with two nulls for every scene
    /// that has no override, which the client could not read when the duration was a number that cannot be null (found when the
    /// responses of a real OBS Studio were read, see ObsStudioCaptureTests).
    /// </summary>
    public class NullableResponseFieldTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task ASceneWithoutATransitionOverride_IsAnsweredWithNulls_WhichTheClientReads()
        {
            var (sent, response) = await ObsClientHarness.QueryAsync(
                client => client.GetSceneSceneTransitionOverride("Game"),
                new { transitionName = (string?)null, transitionDuration = (long?)null });

            Assert.Equal("GetSceneSceneTransitionOverride", sent.RequestType);
            Assert.Null(response.TransitionName);
            Assert.Null(response.TransitionDuration);
        }

        [Fact]
        public async Task ASceneWithATransitionOverride_IsAnsweredWithTheNameAndTheDuration()
        {
            var (_, response) = await ObsClientHarness.QueryAsync(
                client => client.GetSceneSceneTransitionOverride("Game"),
                new { transitionName = "Fade", transitionDuration = 300L });

            Assert.Equal("Fade", response.TransitionName);
            Assert.Equal(300L, response.TransitionDuration);
        }

        [Fact]
        public async Task TheListOfTransitions_MayHaveNoCurrentTransition()
        {
            var (_, response) = await ObsClientHarness.QueryAsync(
                client => client.GetSceneTransitionList(),
                new
                {
                    currentSceneTransitionName = (string?)null,
                    currentSceneTransitionUuid = (Guid?)null,
                    currentSceneTransitionKind = (string?)null,
                    transitions = Array.Empty<object>(),
                });

            Assert.Null(response.CurrentSceneTransitionName);
            Assert.Null(response.CurrentSceneTransitionUuid);
            Assert.Null(response.CurrentSceneTransitionKind);
            Assert.Empty(response.Transitions);
        }

        [Fact]
        public async Task TheSpecialInputs_AreNullForTheOnesThatAreNotConfigured()
        {
            var (sent, response) = await ObsClientHarness.QueryAsync(
                client => client.GetSpecialInputs(),
                new
                {
                    desktop1 = "Desktop Audio",
                    desktop2 = (string?)null,
                    mic1 = "Mic/Aux",
                    mic2 = (string?)null,
                    mic3 = (string?)null,
                    mic4 = (string?)null,
                });

            Assert.Equal("GetSpecialInputs", sent.RequestType);
            Assert.Equal("Desktop Audio", response.Desktop1);
            Assert.Null(response.Desktop2);
            Assert.Equal("Mic/Aux", response.Mic1);
            Assert.Null(response.Mic2);
            Assert.Null(response.Mic3);
            Assert.Null(response.Mic4);
        }

        [Fact]
        public async Task ARecordThatStarted_HasNoOutputPath()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            var awaiter = new EventAwaiter<RecordStateChangedEventArgs>();
            client.RecordStateChanged += awaiter.Handler;

            await server.SendEventAsync("RecordStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED", outputPath = (string?)null });

            var args = await awaiter.Task.WithTimeoutAsync(Timeout, "RecordStateChanged handler was not invoked.");
            Assert.True(args.OutputActive);
            Assert.Equal(ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED, args.OutputState);
            Assert.Null(args.OutputPath);
        }
    }
}
