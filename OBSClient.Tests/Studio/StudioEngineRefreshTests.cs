namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// What the engine does when the application asks for a part of the model to be read again (a change that it made was not shown by the events in time).
    /// </summary>
    public class StudioEngineRefreshTests
    {
        private static EngineHarness Started(Action<FakeWorld> setup)
        {
            var harness = new EngineHarness();
            setup(harness.World);
            harness.Start();
            harness.SettleWithTime();
            return harness;
        }

        [Fact]
        public void TheSceneItemsOfAScene_AreReadAgain_WhenTheyAreAskedFor_AndNothingElseIs()
        {
            var gaming = Guid.Empty;
            var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                world.AddScene("Chatting");
                world.AddItem(gaming, world.AddInput("Camera"), "Camera");
            });
            Assert.Null(h.Take());

            h.Engine.RequestRefresh(ReadKind.Items, gaming, h.Now);
            var ticket = h.Take();

            Assert.NotNull(ticket);
            var request = Assert.Single(ticket!.Requests);
            Assert.Equal(ReadKind.Items, request.Kind);
            Assert.Equal(gaming, request.Target);
            h.Complete(ticket);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void TheAudioOfAnInput_TheListsAndTheProgramScene_CanBeAskedFor()
        {
            var mic = Guid.Empty;
            var h = Started(world =>
            {
                world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
            });

            h.Engine.RequestRefresh(ReadKind.AudioMute, mic, h.Now);
            h.Engine.RequestRefresh(ReadKind.Program, Guid.Empty, h.Now);
            h.Engine.RequestRefresh(ReadKind.Scenes, Guid.Empty, h.Now);
            var kinds = new List<ReadKind>();
            while (h.Take() is { } ticket)
            {
                kinds.AddRange(ticket.Requests.Select(request => request.Kind));
                h.Complete(ticket);
            }

            Assert.Contains(ReadKind.AudioMute, kinds);
            Assert.Contains(ReadKind.Program, kinds);
            Assert.Contains(ReadKind.Scenes, kinds);
            Assert.DoesNotContain(ReadKind.Inputs, kinds);
        }

        [Fact]
        public void APartThatIsNotInTheModel_IsNotRead()
        {
            var h = Started(world => world.AddScene("Gaming"));

            h.Engine.RequestRefresh(ReadKind.Items, Guid.NewGuid(), h.Now);
            h.Engine.RequestRefresh(ReadKind.AudioMute, Guid.NewGuid(), h.Now);

            Assert.Null(h.Take());
        }

        [Fact]
        public void NothingIsRead_WithoutAConnection_OrWhileOBSStudioChangesItsSceneCollection()
        {
            var gaming = Guid.Empty;
            var h = Started(world => gaming = world.AddScene("Gaming"));

            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            h.Engine.RequestRefresh(ReadKind.Items, gaming, h.Now);
            Assert.Null(h.Take());

            h.End();
            h.Engine.RequestRefresh(ReadKind.Items, gaming, h.Now);
            h.Start();
            var read = new List<ReadKind>();
            while (h.Take() is { } ticket)
            {
                read.AddRange(ticket.Requests.Select(request => request.Kind));
                h.Complete(ticket);
            }

            // The new connection reads everything as it does always; the request that came while nobody was connected did not leave a mark of its own.
            Assert.Contains(ReadKind.Scenes, read);
        }

        [Fact]
        public void APartThatWaitsForAnotherTry_IsReadAtOnce_WhenItIsAskedFor()
        {
            var gaming = Guid.Empty;
            var h = Started(world => gaming = world.AddScene("Gaming"));

            // The read of the scene items fails: the engine waits before it tries again.
            h.Engine.RequestRefresh(ReadKind.Items, gaming, h.Now);
            var first = h.Take();
            Assert.NotNull(first);
            h.Complete(first!, request => request.Kind == ReadKind.Items ? (207, null) : h.World.Answer(request));
            Assert.Null(h.Take());

            h.Engine.RequestRefresh(ReadKind.Items, gaming, h.Now);

            Assert.NotNull(h.Take());
        }
    }
}
