namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model of OBS Studio does when OBS Studio changes its scene collection: from <c>CurrentSceneCollectionChanging</c> it asks nothing and takes nothing from the
    /// events, what a read that is under way answers is not applied, and when <c>CurrentSceneCollectionChanged</c> says that the other collection is loaded the model of the
    /// old one is forgotten and the new one is read as a first one is. (Against the simulation of OBS Studio, with random switches: <see cref="ModelEngineSimulationTests"/>.)
    /// </summary>
    public class ModelEngineCollectionTests
    {
        private static EngineHarness Started(Action<FakeWorld>? setup = null)
        {
            var harness = new EngineHarness();
            if (setup is null)
            {
                harness.World.AddScene("Scene");
            }
            else
            {
                setup(harness.World);
            }

            harness.Start();
            harness.SettleWithTime();
            return harness;
        }

        // OBS Studio has another collection loaded: what the world holds is replaced.
        private static void Load(FakeWorld world, Action<FakeWorld> setup)
        {
            world.Scenes.Clear();
            world.Inputs.Clear();
            world.Items.Clear();
            world.Program = null;
            setup(world);
        }

        private static void Changing(EngineHarness h, string name = "Untitled")
        {
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = name }, 2);
        }

        private static void Changed(EngineHarness h, string name)
        {
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = name }, 2);
        }

        private static string[] SceneNames(EngineHarness h)
        {
            return h.Engine.Snapshot().Scenes.Select(scene => scene.Name).ToArray();
        }

        [Fact]
        public void WhileOBSStudioChangesItsSceneCollection_NothingIsRead_AndTheModelSaysThatItIsBeingRead()
        {
            var h = Started(world =>
            {
                var gaming = world.AddScene("Gaming");
                var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);

            // A read is wanted when the switch begins (the program scene changed), and is not taken.
            h.Event("CurrentProgramSceneChanged", new { sceneName = "Gaming", sceneUuid = h.World.Program }, 4);
            Changing(h);
            h.Advance(TimeSpan.FromSeconds(5));

            Assert.True(h.Engine.IsSwitching);
            Assert.Equal(ModelEngineState.Synchronizing, h.Engine.State);
            Assert.False(h.Engine.IsSettled);
            Assert.Null(h.Take());
            Assert.Null(h.Engine.NextWake);

            // What the model held is what an application reads meanwhile: the last that was known.
            Assert.Equal(["Gaming"], SceneNames(h));
            Assert.Single(h.Engine.Snapshot().Inputs);
        }

        [Fact]
        public void EventsThatComeWhileItChanges_AreNotApplied()
        {
            var h = Started(world => world.AddScene("Gaming"));
            var gaming = h.World.Scenes[0].Uuid;
            h.Engine.TakeChanges();

            Changing(h);
            h.Event("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "Renamed" }, 4);
            h.Event("SceneCreated", new { sceneName = "Created", sceneUuid = Guid.NewGuid(), isGroup = false }, 4);
            h.Event("SceneRemoved", new { sceneName = "Gaming", sceneUuid = gaming, isGroup = false }, 4);

            Assert.Equal(["Gaming"], SceneNames(h));
            Assert.Empty(h.Engine.TakeChanges());
        }

        [Fact]
        public void WhenTheOtherCollectionIsLoaded_TheModelOfTheOldOneIsForgotten_AndTheNewOneIsRead()
        {
            var h = Started(world =>
            {
                var gaming = world.AddScene("Gaming");
                var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });
            var epoch = h.Engine.Epoch;
            Changing(h);
            Load(h.World, world =>
            {
                var chatting = world.AddScene("Chatting");
                var camera = world.AddInput("Camera");
                world.AddItem(chatting, camera, "Camera");
                world.AddScene("Scene");
                world.Program = chatting;
            });

            Changed(h, "Chatting");

            // The old collection is gone at once, and the new one is not read yet.
            Assert.False(h.Engine.IsSwitching);
            Assert.Equal(epoch + 1, h.Engine.Epoch);
            Assert.Equal(ModelEngineState.Synchronizing, h.Engine.State);
            Assert.Empty(h.Engine.Snapshot().Scenes);
            Assert.Empty(h.Engine.Snapshot().Inputs);

            Assert.True(h.Settle() > 0);

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(ModelEngineState.Synchronized, snapshot.State);
            Assert.Equal(["Chatting", "Scene"], snapshot.Scenes.Select(scene => scene.Name).Order().ToArray());
            Assert.Equal(h.World.Program, snapshot.ProgramSceneUuid);
            Assert.Equal(["Camera"], snapshot.Inputs.Select(input => input.Name).ToArray());
            Assert.Single(snapshot.Scenes.Single(scene => scene.Name == "Chatting").Items);
            Assert.Empty(h.Engine.CheckConsistency());
            Assert.Equal(0, snapshot.Incomplete);
        }

        [Fact]
        public void TheEndOfASwitch_IsEnough_WhenTheModelDidNotSeeItsBeginning()
        {
            // The connection was made in the middle of it, or the event was lost.
            var h = Started(world => world.AddScene("Gaming"));
            Load(h.World, world => world.AddScene("Other"));

            Changed(h, "Other");
            h.Settle();

            Assert.Equal(["Other"], SceneNames(h));
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
        }

        [Fact]
        public void AReadThatWasUnderWayWhenTheSwitchBegan_IsNotApplied_WhenItAnswers_OrWhenTheNewCollectionIsRead()
        {
            var h = Started(world =>
            {
                world.AddScene("Gaming");
                world.AddScene("Second");
            });
            var gaming = h.World.Scenes.First(scene => scene.Name == "Gaming").Uuid;
            var second = h.World.Scenes.First(scene => scene.Name == "Second").Uuid;
            Assert.Equal(gaming, h.Engine.Snapshot().ProgramSceneUuid);

            // OBS Studio makes another scene the program scene, and the model reads it.
            h.World.Program = second;
            h.Event("CurrentProgramSceneChanged", new { sceneName = "Second", sceneUuid = second }, 4);
            var underWay = h.Take();
            Assert.NotNull(underWay);

            // It answers in the middle of the switch (with what the old collection had).
            Changing(h);
            h.Complete(underWay!);
            Assert.Equal(gaming, h.Engine.Snapshot().ProgramSceneUuid);
            Assert.False(h.Engine.IsOutdated(underWay!));
            Load(h.World, world => world.AddScene("Other"));
            var other = h.World.Scenes[0].Uuid;
            Changed(h, "Other");
            Assert.True(h.Engine.IsOutdated(underWay!));
            var fresh = h.Take();
            Assert.NotNull(fresh);

            // And it answers again after the new collection was loaded, before the new collection is read (a read that was slow): it is not about this collection, and
            // a model that took it would hold a program scene that is not there.
            h.Complete(underWay!, _ => (100, new { sceneName = "Second", sceneUuid = second }));
            Assert.Null(h.Engine.Snapshot().ProgramSceneUuid);

            h.Complete(fresh!);
            h.Settle();

            Assert.Equal(other, h.Engine.Snapshot().ProgramSceneUuid);
            Assert.Equal(["Other"], SceneNames(h));
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
            Assert.Empty(h.Engine.CheckConsistency());
        }

        [Fact]
        public void WhatWasRemovedInTheOldCollection_IsNotRemembered_WhenTheSameUuidIsInTheNewOne()
        {
            // A collection that is loaded again has the same UUIDs (they are saved with it): an input that was removed, and that the model guards against, is there again.
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                var gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });
            var gamingUuid = h.World.Scenes.First(scene => scene.Name == "Gaming").Uuid;
            h.Event("SceneItemRemoved", new { sceneName = "Gaming", sceneUuid = gamingUuid, sourceName = "Mic", sourceUuid = mic, sceneItemId = 1 }, 128);
            h.Event("InputRemoved", new { inputName = "Mic", inputUuid = mic }, 8);
            h.World.Inputs.Clear();
            h.World.Items[gamingUuid].Clear();
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.True(h.Engine.RememberedCount > 0);

            // The collection that has the input again (the same UUID) is loaded.
            Changing(h);
            Load(h.World, world =>
            {
                var scene = world.AddScene("Gaming");
                world.Inputs.Add((mic, "Mic", "wasapi_input_capture", 2, false, 1.0));
                world.AddItem(scene, mic, "Mic");
            });
            Changed(h, "Streaming");
            h.SettleWithTime();

            Assert.Equal(["Mic"], h.Engine.Snapshot().Inputs.Select(input => input.Name).ToArray());
            Assert.Equal(0, h.Engine.RememberedCount);
        }

        [Fact]
        public void AReadThatIsAnsweredNotReady_IsRepeatedAfterAWait_UntilTheServerIsReady()
        {
            // The switch is over for the model (it was told), and OBS Studio is not ready yet: it answers 207 to every request of a batch.
            var h = Started(world => world.AddScene("Gaming"));
            Load(h.World, world => world.AddScene("Other"));
            Changing(h);
            Changed(h, "Other");
            var notReady = 0;

            (int, object?) Answer(ReadRequest request)
            {
                notReady++;
                return (207, null);
            }

            Assert.True(h.Settle(Answer) > 0);
            Assert.True(notReady > 0);
            Assert.Equal(ModelEngineState.Synchronizing, h.Engine.State);
            Assert.NotNull(h.Engine.NextWake);

            h.SettleWithTime();

            Assert.Equal(["Other"], SceneNames(h));
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
        }

        [Fact]
        public void ANewConnection_EndsASwitchThatTheOldOneHadBegun()
        {
            var h = Started(world => world.AddScene("Gaming"));
            Changing(h);
            Assert.True(h.Engine.IsSwitching);

            h.End();
            h.Start();

            Assert.False(h.Engine.IsSwitching);
            var ticket = h.Take();
            Assert.NotNull(ticket);
            h.Complete(ticket!);
            h.Settle();
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
        }

        [Fact]
        public void AConnectionThatEndedInTheMiddleOfASwitch_TakesNothingOfTheOldCollectionIntoTheNextOne_EvenWhenTheSameUuidsAreLoadedAgain()
        {
            // OBS Studio went from this collection to another and back while nobody was connected: the scene has the UUID that it had, and it is a new scene all the same.
            var h = Started(world =>
            {
                var gaming = world.AddScene("Gaming");
                world.AddItem(gaming, world.AddInput("Mic", "wasapi_input_capture", 2), "Mic");
            });
            var epoch = h.Engine.Epoch;
            Changing(h);

            h.End();
            h.Start();

            // The model of the old collection is dropped at once (the application is told by the objects that it holds), and what is read is the collection that OBS Studio has.
            Assert.Equal(epoch + 1, h.Engine.Epoch);
            Assert.False(h.Engine.IsSwitching);
            Assert.Empty(h.Engine.Snapshot().Scenes);
            Assert.Empty(h.Engine.Snapshot().Inputs);
            h.Settle();
            var snapshot = h.Engine.Snapshot();
            Assert.Equal(ModelEngineState.Synchronized, snapshot.State);
            Assert.Equal(["Gaming"], snapshot.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(["Mic"], snapshot.Inputs.Select(input => input.Name).ToArray());
            Assert.Empty(h.Engine.CheckConsistency());
        }

        [Fact]
        public void AConnectionThatEndedWithoutASwitch_KeepsTheModel_AndMergesWhatItReadsByUuid()
        {
            var h = Started(world => world.AddScene("Gaming"));
            var epoch = h.Engine.Epoch;

            h.End();
            h.Start();

            // The model is the last that was known until the new connection has read, and the collection is the same one.
            Assert.Equal(epoch, h.Engine.Epoch);
            Assert.Equal(["Gaming"], SceneNames(h));
            h.Settle();
            Assert.Equal(["Gaming"], SceneNames(h));
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
        }

        [Fact]
        public void AReadThatNamesAnotherSceneCollection_IsASwitchThatTheEventsDidNotTellOf_AndTheModelIsOfTheNewOne()
        {
            var h = Started(world =>
            {
                world.AddScene("Gaming");
                world.SceneCollections.Add("Other");
            });
            var epoch = h.Engine.Epoch;

            // OBS Studio loaded the other collection and none of the events about it came: the model learns it from a read of the list, which the application asked for.
            Load(h.World, world => world.AddScene("Chatting"));
            h.World.CurrentSceneCollection = "Other";
            h.Engine.RequestRefresh(ReadKind.SceneCollections, Guid.Empty, h.Now);
            var ticket = h.Take()!;
            Assert.Equal([ReadKind.SceneCollections], ticket.Requests.Select(request => request.Kind).ToArray());
            h.Complete(ticket);

            // What the model held of the collection that was left is gone at once, and the model is not complete before it has read the new one.
            Assert.Equal(epoch + 1, h.Engine.Epoch);
            Assert.Equal(ModelEngineState.Synchronizing, h.Engine.State);
            Assert.Empty(h.Engine.Snapshot().Scenes);
            Assert.False(h.Engine.IsSettled);

            h.Settle();

            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
            Assert.Equal("Other", h.Engine.Snapshot().SceneCollections!.Current);
            Assert.Equal(["Chatting"], SceneNames(h));
            Assert.Empty(h.Engine.CheckConsistency());
        }

        [Fact]
        public void ARenamedSceneCollection_IsTheSameCollection_AndTheModelIsNotReadAgain()
        {
            var h = Started(world => world.AddScene("Gaming"));
            var epoch = h.Engine.Epoch;

            // The user renamed the collection that is loaded: OBS Studio lists it under the new name, and says nothing else.
            h.World.SceneCollections[0] = "Renamed";
            h.World.CurrentSceneCollection = "Renamed";
            h.Event("SceneCollectionListChanged", new { sceneCollections = Array.Empty<string>() }, 2);
            h.Settle();

            Assert.Equal("Renamed", h.Engine.Snapshot().SceneCollections!.Current);
            Assert.Equal(epoch, h.Engine.Epoch);
            Assert.Equal(["Gaming"], SceneNames(h));
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);

            // The collection that the model is of has the new name now: a read that says it again is no switch.
            h.Engine.RequestRefresh(ReadKind.SceneCollections, Guid.Empty, h.Now);
            h.Settle();
            Assert.Equal(epoch, h.Engine.Epoch);
            Assert.Equal(["Gaming"], SceneNames(h));
        }

        [Fact]
        public void ANewConnection_ThatFindsAnotherSceneCollectionLoaded_ReadsItAsAFirstOne()
        {
            var h = Started(world =>
            {
                world.AddScene("Gaming");
                world.SceneCollections.Add("Other");
            });
            var epoch = h.Engine.Epoch;

            // OBS Studio changed its collection while nobody was connected, and the model saw none of the events.
            h.End();
            Load(h.World, world => world.AddScene("Chatting"));
            h.World.CurrentSceneCollection = "Other";
            h.Start();
            h.Settle();

            Assert.Equal(epoch + 1, h.Engine.Epoch);
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
            Assert.Equal("Other", h.Engine.Snapshot().SceneCollections!.Current);
            Assert.Equal(["Chatting"], SceneNames(h));
            Assert.Empty(h.Engine.CheckConsistency());
        }

        [Fact]
        public void TheEventsOfASwitchThatAReadShowedAlready_AreNotASecondSwitch_WhenTheyComeLate()
        {
            var h = Started(world =>
            {
                world.AddScene("Gaming");
                world.SceneCollections.Add("Other");
            });
            Load(h.World, world => world.AddScene("Chatting"));
            h.World.CurrentSceneCollection = "Other";
            h.Engine.RequestRefresh(ReadKind.SceneCollections, Guid.Empty, h.Now);
            h.Settle();
            var epoch = h.Engine.Epoch;
            Assert.Equal(["Chatting"], SceneNames(h));

            // The events of that switch come after the read: the model is of the collection that OBS Studio loaded, and they are about the switch that it has seen.
            Changing(h, "Untitled");
            Assert.False(h.Engine.IsSwitching);
            Changed(h, "Other");

            Assert.Equal(epoch, h.Engine.Epoch);
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
            Assert.Equal(["Chatting"], SceneNames(h));
            Assert.True(h.Engine.IsSettled);

            // A switch after that is a switch, as its events say.
            Changing(h, "Other");
            Assert.True(h.Engine.IsSwitching);
            Load(h.World, world => world.AddScene("Third"));
            h.World.CurrentSceneCollection = "Untitled";
            Changed(h, "Untitled");
            h.Settle();

            Assert.Equal(epoch + 1, h.Engine.Epoch);
            Assert.Equal(["Third"], SceneNames(h));
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.State);
        }

        [Fact]
        public void TheEventsOfAnotherSwitchThanTheOneThatAReadShowed_AreASwitch_AlsoWhenTheEventsOfTheFirstWereLost()
        {
            var h = Started(world =>
            {
                world.AddScene("Gaming");
                world.SceneCollections.AddRange(["Other", "Third"]);
            });
            Load(h.World, world => world.AddScene("Chatting"));
            h.World.CurrentSceneCollection = "Other";
            h.Engine.RequestRefresh(ReadKind.SceneCollections, Guid.Empty, h.Now);
            h.Settle();
            var epoch = h.Engine.Epoch;

            // OBS Studio went on to a third collection, and the events of the first switch never came: the events that name other collections than the ones of the read are a switch.
            Changing(h, "Other");
            Assert.True(h.Engine.IsSwitching);
            Load(h.World, world => world.AddScene("Closing"));
            h.World.CurrentSceneCollection = "Third";
            Changed(h, "Third");
            h.Settle();

            Assert.Equal(epoch + 1, h.Engine.Epoch);
            Assert.Equal("Third", h.Engine.Snapshot().SceneCollections!.Current);
            Assert.Equal(["Closing"], SceneNames(h));

            // What the read showed is not waited for any more, the events above having ended it: an event that says that the first collection is loaded again is a switch like any other.
            Load(h.World, world => world.AddScene("Opening"));
            h.World.CurrentSceneCollection = "Other";
            Changed(h, "Other");
            h.Settle();

            Assert.Equal(epoch + 2, h.Engine.Epoch);
            Assert.Equal(["Opening"], SceneNames(h));
        }

        [Fact]
        public void TheEventsOfASwitch_AreIgnored_WhenThereIsNoConnection()
        {
            var h = new EngineHarness();

            Changing(h);
            Changed(h, "Other");

            Assert.False(h.Engine.IsSwitching);
            Assert.Equal(ModelEngineState.Disconnected, h.Engine.State);
            Assert.Equal(0, h.Engine.Epoch);
        }

        [Fact]
        public void ASecondBeginning_DoesNotChangeWhatTheFirstDid_AndTheEventsOfTheSwitchAreTheOnesThatTheEngineHandles()
        {
            var h = Started(world => world.AddScene("Gaming"));

            Changing(h);
            Changing(h, "Again");
            Load(h.World, world => world.AddScene("Other"));
            Changed(h, "Other");
            h.Settle();

            Assert.Equal(["Other"], SceneNames(h));
            Assert.True(ModelEngine.Handles(OBSStudioClient.Enums.EventType.CurrentSceneCollectionChanging));
            Assert.True(ModelEngine.Handles(OBSStudioClient.Enums.EventType.CurrentSceneCollectionChanged));
        }
    }
}
