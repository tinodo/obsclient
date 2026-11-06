namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// What the model does when OBS Studio answers a read with something else than what it asked for: a failure, a timeout, less than was asked, a
    /// scene or input that is not there, an object that the model does not know.
    /// </summary>
    public class StudioEngineReadTests
    {
        private static EngineHarness WithWorld(StudioEngineOptions? options = null)
        {
            var harness = new EngineHarness(options);
            harness.World.AddScene("Scene");
            var gaming = harness.World.AddScene("Gaming");
            var mic = harness.World.AddInput("Mic", "wasapi_input_capture", 2);
            harness.World.AddItem(gaming, mic, "Mic");
            return harness;
        }

        // Answers what the world says, except that the requests of one kind fail the first times that they are made.
        private static Func<ReadRequest, (int Code, object? Data)> Failing(EngineHarness harness, ReadKind kind, int times, int code = 500)
        {
            var left = times;
            return request =>
            {
                if (request.Kind == kind && left > 0)
                {
                    left--;
                    return (code, null);
                }

                return harness.World.Answer(request);
            };
        }

        private static void AssertWholeModel(EngineHarness harness)
        {
            var snapshot = harness.Engine.Snapshot();
            Assert.Equal(StudioEngineState.Synchronized, snapshot.State);
            Assert.True(harness.Engine.IsSettled);
            Assert.Equal(new[] { "Gaming", "Scene" }, snapshot.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Single(snapshot.Scenes.Single(scene => scene.Name == "Gaming").Items);
            Assert.Equal("Mic", Assert.Single(snapshot.Inputs).Name);
        }

        [Theory]
        [InlineData("Scenes")]
        [InlineData("Inputs")]
        [InlineData("Program")]
        [InlineData("StudioMode")]
        [InlineData("Items")]
        [InlineData("AudioMute")]
        [InlineData("AudioVolume")]
        public void ARead_ThatFails_IsRepeatedAfterAWait_UntilItWorks(string kindName)
        {
            var kind = Enum.Parse<ReadKind>(kindName);
            var h = WithWorld();
            h.Start();
            var start = h.Now;

            h.SettleWithTime(Failing(h, kind, 3));

            AssertWholeModel(h);
            Assert.True(h.Now - start >= TimeSpan.FromMilliseconds(750), "The read was repeated without waiting.");
        }

        [Fact]
        public void TheWaitBetweenRepeats_Doubles_UpToTheMaximum()
        {
            var h = WithWorld();
            h.Start();
            var start = h.Now;

            h.SettleWithTime(Failing(h, ReadKind.Scenes, 6));

            // 0.25 + 0.5 + 1 + 2 + 2 + 2 seconds.
            Assert.InRange(h.Now - start, TimeSpan.FromSeconds(7.75), TimeSpan.FromSeconds(8.5));
            AssertWholeModel(h);
        }

        [Fact]
        public void AReadThatTimesOut_IsRepeated()
        {
            var h = WithWorld();
            h.Start();

            h.Engine.OnReadFailed(h.Take()!, h.Now);
            h.SettleWithTime();

            AssertWholeModel(h);
        }

        [Fact]
        public void AReadThatGetsLessAnswersThanRequests_IsRepeatedForWhatWasNotAnswered()
        {
            var h = WithWorld();
            h.Start();

            // The batch ended after its first request.
            h.Complete(h.Take()!, only: 1);
            h.SettleWithTime();

            AssertWholeModel(h);
        }

        [Fact]
        public void ASceneThatOBSDoesNotHaveAnyMore_ThatItsItemsRead_SaysSo_IsLeftToTheListOfScenes()
        {
            Guid gaming = Guid.Empty;
            var h = WithWorld();
            h.Start();
            h.SettleWithTime();
            gaming = h.Engine.Snapshot().Scenes.Single(scene => scene.Name == "Gaming").Uuid;

            // OBS removed the scene, and the model has not heard of it: an item event makes it read the scene items, and OBS says that there is no such scene.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == gaming);
            h.World.Items.Remove(gaming);
            EngineEvents.ItemCreated(h, gaming, "Gaming", 9, 0, Guid.NewGuid(), "X");
            h.SettleWithTime();

            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Name == "Gaming");
            Assert.Contains(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.Repaired && change.Text == "Gaming");
        }

        [Fact]
        public void AnInputThatOBSDoesNotHaveAnyMore_ThatItsAudioRead_SaysSo_IsLeftToTheListOfInputs()
        {
            var h = WithWorld();
            h.Start();
            var reads = 0;
            var failing = Failing(h, ReadKind.AudioMute, 1, 600);

            (int, object?) CountInputs(ReadRequest request)
            {
                if (request.Kind == ReadKind.Inputs)
                {
                    reads++;
                }

                return failing(request);
            }

            h.SettleWithTime(CountInputs);

            // The list of inputs was read again to decide (it still has the input), and the audio state was read again after it.
            Assert.True(reads >= 2, $"The list of inputs was read {reads} time(s).");
            AssertWholeModel(h);
        }

        [Fact]
        public void AnInputWithoutAudio_ThatItsCapabilitiesSaidHasIt_IsAnnouncedWithoutAudioState()
        {
            var h = WithWorld();
            h.Start();

            h.SettleWithTime(Failing(h, ReadKind.AudioMute, 99, 604));

            var input = Assert.Single(h.Engine.Snapshot().Inputs);
            Assert.Equal(("Mic", false, 1f), (input.Name, input.Muted, input.VolumeMul));
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AProgramSceneThatTheModelDoesNotKnow_MakesTheListOfScenesBeRead()
        {
            var h = WithWorld();
            h.Start();
            h.SettleWithTime();

            // OBS has a new scene, and the program scene is already it: the model hears of the program scene first.
            var created = h.World.AddScene("New");
            h.World.Program = created;
            h.Event("CurrentProgramSceneChanged", new { sceneName = "New", sceneUuid = created }, 4);
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(created, snapshot.ProgramSceneUuid);
            Assert.Contains(snapshot.Scenes, scene => scene.Name == "New");
        }

        [Fact]
        public void TheSceneItemsOfAGroup_AreReadWithTheRequestForGroups()
        {
            var h = WithWorld();
            h.Start();
            h.SettleWithTime();
            var group = Guid.NewGuid();
            h.World.Items[group] = [];

            h.Event("SceneCreated", new { sceneName = "Group", sceneUuid = group, isGroup = true }, 4);

            var read = h.Take()!;
            Assert.Contains(read.Requests, request => request.Kind == ReadKind.Items && request.Target == group && request.Type == RequestType.GetGroupSceneItemList);
            h.Complete(read);
            h.SettleWithTime();
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AGroupThatWasDeletedWhileDisconnected_IsDroppedAfterTheModelIsReadAgain()
        {
            var h = WithWorld();
            h.Start();
            h.SettleWithTime();
            var group = Guid.NewGuid();
            h.World.Items[group] = [];
            h.Event("SceneCreated", new { sceneName = "Group", sceneUuid = group, isGroup = true }, 4);
            h.SettleWithTime();
            Assert.True(h.Engine.IsSettled);

            // The connection ends, and the group is deleted: no list of scenes has it, so only the 600 of its items can tell.
            h.End();
            h.World.Items.Remove(group);
            h.Start();
            h.SettleWithTime();

            Assert.True(h.Engine.IsSettled);
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void ASceneThatTheListDoesNotHaveAndOnlyAnItemKnows_IsReadFromItsItems_AndAGroupIsJustThat()
        {
            Guid scene = Guid.Empty;
            var h = WithWorld();
            h.Start();
            h.SettleWithTime();
            scene = h.Engine.Snapshot().Scenes.Single(s => s.Name == "Gaming").Uuid;

            // A group is the source of a scene item and is in no list of OBS.
            var group = Guid.NewGuid();
            h.World.Items[group] = [];
            var id = h.World.AddItem(scene, group, "Group", isScene: true);
            EngineEvents.ItemCreated(h, scene, "Gaming", id, 1, group, "Group");
            h.SettleWithTime();

            Assert.True(h.Engine.IsSettled);
            Assert.Equal(2, h.Engine.Snapshot().Scenes.Single(s => s.Name == "Gaming").Items.Count);
        }
    }
}
