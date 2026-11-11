namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds of whether an input is on the air and whether it is shown, when it is asked to follow the activity (the events come with <see cref="ObsHighVolume.Activity"/>): that it is not kept, not read
    /// and its events not even acted on, when it is not asked; how a read and the events decide which of them is newer; and what happens to the activity of an input that is new, refused, gone or read again.
    /// </summary>
    public class ModelEngineActivityTests
    {
        private const int ActiveEvents = 1 << 17;
        private const int ShowEvents = 1 << 18;
        private const uint MediaCaps = 8327;

        // A world with a scene "Scene" (the program scene) that shows nothing, and a scene "Gaming" that shows the input "Camera"; the inputs "Mic" and "Logo" are in no scene.
        private static EngineHarness Started(bool follow, Action<FakeWorld>? setup = null, List<ReadKind>? reads = null)
        {
            var h = new EngineHarness(new ModelEngineOptions { HighVolume = follow ? ObsHighVolume.Activity : ObsHighVolume.None });
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.AddInput("Logo", "image_source", 8);
            h.World.AddItem(gaming, camera, "Camera");
            setup?.Invoke(h.World);
            h.Start();
            h.SettleWithTime(request =>
            {
                reads?.Add(request.Kind);
                return h.World.Answer(request);
            });
            h.Engine.TakeChanges();
            return h;
        }

        private static Guid Uuid(EngineHarness h, string name)
        {
            return Uuid(h.World, name);
        }

        private static Guid Uuid(FakeWorld world, string name)
        {
            return world.Inputs.Single(input => input.Name == name).Uuid;
        }

        private static (bool? Active, bool? Showing) State(EngineHarness h, Guid uuid)
        {
            var input = h.Engine.Snapshot().Inputs.Single(entry => entry.Uuid == uuid);
            return (input.Active, input.Showing);
        }

        private static EngineInput Input(EngineHarness h, Guid uuid)
        {
            return h.Engine.Snapshot().Inputs.Single(input => input.Uuid == uuid);
        }

        private static ModelChange[] Changes(EngineHarness h, params ModelChangeKind[] kinds)
        {
            return h.Engine.TakeChanges().Where(change => kinds.Contains(change.Kind)).ToArray();
        }

        private static ReadRequest[] NextRead(EngineHarness h)
        {
            return h.Take() is { } ticket ? ticket.Requests.ToArray() : [];
        }

        private static void Active(EngineHarness h, Guid input, bool active, string name = "Camera")
        {
            h.Event("InputActiveStateChanged", new { inputName = name, inputUuid = input, videoActive = active }, ActiveEvents);
        }

        private static void Showing(EngineHarness h, Guid input, bool showing, string name = "Camera")
        {
            h.Event("InputShowStateChanged", new { inputName = name, inputUuid = input, videoShowing = showing }, ShowEvents);
        }

        [Fact]
        public void WhenTheActivityIsNotFollowed_AnInputHasNone_ItIsNeverRead_AndItsEventsAreNotActedOn()
        {
            var reads = new List<ReadKind>();
            var h = Started(follow: false, reads: reads);
            var camera = Uuid(h, "Camera");

            Assert.All(h.Engine.Snapshot().Inputs, input =>
            {
                Assert.Null(input.Active);
                Assert.Null(input.Showing);
            });
            Assert.DoesNotContain(ReadKind.Activity, reads);
            Assert.False(h.Engine.Wants(EventType.InputActiveStateChanged));
            Assert.False(h.Engine.Wants(EventType.InputShowStateChanged));
            Assert.True(ModelEngine.Handles(EventType.InputActiveStateChanged));
            Assert.True(ModelEngine.Handles(EventType.InputShowStateChanged));

            Showing(h, camera, true);
            Active(h, camera, true);

            Assert.Null(Input(h, camera).Active);
            Assert.Null(Input(h, camera).Showing);
            Assert.Empty(h.Engine.TakeChanges());
            Assert.Null(h.Take());
        }

        [Fact]
        public void WhenTheActivityIsFollowed_TheEngineWantsItsEvents_AndTheyAreTheOnlyOnesThatAreCalledActivity()
        {
            var h = Started(follow: true);

            Assert.True(h.Engine.Wants(EventType.InputActiveStateChanged));
            Assert.True(h.Engine.Wants(EventType.InputShowStateChanged));
            Assert.False(h.Engine.Wants(EventType.SceneItemTransformChanged));
            Assert.Equal(ObsHighVolume.Activity, ModelEngine.HighVolumeOf(EventType.InputActiveStateChanged));
            Assert.Equal(ObsHighVolume.Activity, ModelEngine.HighVolumeOf(EventType.InputShowStateChanged));
            Assert.Equal(
                [EventType.InputActiveStateChanged, EventType.InputShowStateChanged],
                Enum.GetValues<EventType>().Where(type => ModelEngine.HighVolumeOf(type) == ObsHighVolume.Activity).Order().ToArray());
        }

        [Fact]
        public void TheActivityIsReadOnceForEachInput_AndIsInTheModelAsOBSStudioSaidIt()
        {
            var reads = new List<ReadKind>();
            var h = Started(
                follow: true,
                world =>
                {
                    world.Activity[Uuid(world, "Camera")] = (true, true);
                    world.Activity[Uuid(world, "Logo")] = (false, true);
                },
                reads);

            Assert.Equal(3, reads.Count(kind => kind == ReadKind.Activity));
            Assert.Equal((true, true), State(h, Uuid(h, "Camera")));
            Assert.Equal((false, true), State(h, Uuid(h, "Logo")));
            Assert.Equal((false, false), State(h, Uuid(h, "Mic")));
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void WhatTheFirstReadSaysOfTheActivity_IsNoChange()
        {
            var h = new EngineHarness(new ModelEngineOptions { HighVolume = ObsHighVolume.Activity });
            h.World.AddScene("Scene");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.World.Activity[camera] = (true, true);
            h.Start();
            h.SettleWithTime();

            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind is ModelChangeKind.InputActiveChanged or ModelChangeKind.InputShowingChanged);
        }

        [Fact]
        public void AnEventOfTheActivity_ChangesItAtOnce_WithoutARead_AndTheChangeSaysFromWhatToWhat()
        {
            var h = Started(follow: true);
            var camera = Uuid(h, "Camera");

            // OBS Studio says first that the input is shown, and then that it is on the air.
            Showing(h, camera, true);
            Active(h, camera, true);

            Assert.True(Input(h, camera).Active);
            Assert.True(Input(h, camera).Showing);
            var changes = Changes(h, ModelChangeKind.InputShowingChanged, ModelChangeKind.InputActiveChanged);
            Assert.Equal([ModelChangeKind.InputShowingChanged, ModelChangeKind.InputActiveChanged], changes.Select(change => change.Kind).ToArray());
            Assert.All(changes, change =>
            {
                Assert.Equal(camera, change.Subject);
                Assert.Equal("Camera", change.Text);
                Assert.Equal(false, change.OldValue);
                Assert.Equal(true, change.NewValue);
            });
            Assert.Null(h.Take());
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatGoesOffTheAir_IsToldAsTwoChanges_InTheOrderOfTheEvents()
        {
            var h = Started(follow: true, world => world.Activity[Uuid(world, "Camera")] = (true, true));
            var camera = Uuid(h, "Camera");

            Showing(h, camera, false);
            Active(h, camera, false);

            Assert.Equal(
                [ModelChangeKind.InputShowingChanged, ModelChangeKind.InputActiveChanged],
                Changes(h, ModelChangeKind.InputShowingChanged, ModelChangeKind.InputActiveChanged).Select(change => change.Kind).ToArray());
            Assert.False(Input(h, camera).Active);
            Assert.False(Input(h, camera).Showing);
        }

        [Fact]
        public void AnEventWithTheStateThatTheModelHolds_IsNoChange()
        {
            var h = Started(follow: true, world => world.Activity[Uuid(world, "Camera")] = (true, false));
            var camera = Uuid(h, "Camera");

            Active(h, camera, true);
            Showing(h, camera, false);

            Assert.Empty(h.Engine.TakeChanges());
        }

        [Fact]
        public void AnInputCanBeShownWithoutBeingOnTheAir_AndTheTwoDoNotDependOnEachOther()
        {
            var h = Started(follow: true);
            var logo = Uuid(h, "Logo");

            // The preview of studio mode shows an input that is not on the air.
            Showing(h, logo, true, "Logo");

            Assert.False(Input(h, logo).Active);
            Assert.True(Input(h, logo).Showing);
            Assert.Equal(ModelChangeKind.InputShowingChanged, Assert.Single(h.Engine.TakeChanges()).Kind);
        }

        [Fact]
        public void AnEventOfAnInputThatTheModelDoesNotKnow_MakesTheListOfInputsBeRead()
        {
            var h = Started(follow: true);

            Active(h, Guid.NewGuid(), true);
            Showing(h, Guid.NewGuid(), true);

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Contains(ReadKind.Inputs, NextRead(h).Select(request => request.Kind));
        }

        [Fact]
        public void AnEventOfAnInputThatWasRemoved_IsIgnored()
        {
            var h = Started(follow: true);
            var camera = Uuid(h, "Camera");
            var gaming = h.World.Scenes.Single(scene => scene.Name == "Gaming").Uuid;
            h.World.Inputs.RemoveAll(input => input.Uuid == camera);
            h.World.Items[gaming].Clear();
            EngineEvents.InputRemoved(h, camera, "Camera");
            h.SettleWithTime();
            h.Engine.TakeChanges();

            // OBS Studio says nothing of an input that it destroys, but one that was taken off the air before could still have an event on its way.
            Active(h, camera, false);

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Null(h.Take());
        }

        [Fact]
        public void AReadThatWasSentBeforeAnEvent_DoesNotUndoIt()
        {
            var h = Started(follow: true);
            var camera = Uuid(h, "Camera");
            h.Engine.RequestRefresh(ReadKind.Activity, camera, h.Now);
            var read = h.Take()!;
            Assert.Equal(camera, read.Requests.Single().Target);

            // The event arrives while the read is under way: it is newer than the read, whatever the read says.
            Showing(h, camera, true);
            Active(h, camera, true);
            h.Complete(read);

            Assert.True(Input(h, camera).Active);
            Assert.True(Input(h, camera).Showing);
        }

        [Fact]
        public void AReadThatWasSentAfterAnEvent_ShowsWhatCameAfterIt()
        {
            var h = Started(follow: true);
            var camera = Uuid(h, "Camera");
            Showing(h, camera, true);
            Active(h, camera, true);

            // OBS Studio took the input off the air again, and the event for that has not reached the model.
            h.World.Activity[camera] = (false, true);
            h.Engine.RequestRefresh(ReadKind.Activity, camera, h.Now);
            h.Settle();

            Assert.False(Input(h, camera).Active);
            Assert.True(Input(h, camera).Showing);
            Assert.Equal(
                [true, false],
                Changes(h, ModelChangeKind.InputActiveChanged).Select(change => (bool)change.NewValue!).ToArray());
        }

        [Fact]
        public void ARefreshOfTheActivity_ReadsItAgain_AndIsDoneWhenTheReadHasBeenApplied()
        {
            var h = Started(follow: true);
            var camera = Uuid(h, "Camera");

            h.Engine.RequestRefresh(ReadKind.Activity, camera, h.Now);

            Assert.False(h.Engine.IsRefreshed(ReadKind.Activity, camera));
            var read = Assert.Single(NextRead(h));
            Assert.Equal((ReadKind.Activity, camera), (read.Kind, read.Target));
            Assert.Equal(RequestType.GetSourceActive, read.Type);
        }

        [Fact]
        public void ARefreshOfTheActivity_WhenItIsNotFollowed_HasNothingToRead()
        {
            var h = Started(follow: false);
            var camera = Uuid(h, "Camera");

            h.Engine.RequestRefresh(ReadKind.Activity, camera, h.Now);

            Assert.True(h.Engine.IsRefreshed(ReadKind.Activity, camera));
            Assert.Null(h.Take());
        }

        [Fact]
        public void AnInputThatAnEventMade_IsAnnouncedWhenItsActivityIsKnown_AndNotBefore()
        {
            var h = Started(follow: true);
            var created = Guid.NewGuid();
            h.World.Inputs.Add((created, "Intro", "ffmpeg_source", MediaCaps, false, 1.0));
            h.World.Activity[created] = (true, true);

            EngineEvents.InputCreated(h, created, "Intro", MediaCaps);
            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == created);
            h.SettleWithTime();

            Assert.True(Input(h, created).Active);
            Assert.True(Input(h, created).Showing);
            var changes = h.Engine.TakeChanges();
            Assert.Single(changes, change => change.Kind == ModelChangeKind.InputAdded && change.Subject == created);

            // What a read says of an input that is not announced yet is not a change of it.
            Assert.DoesNotContain(changes, change => change.Kind is ModelChangeKind.InputActiveChanged or ModelChangeKind.InputShowingChanged);
        }

        [Fact]
        public void AnEventThatComesBeforeTheInputIsAnnounced_IsPartOfWhatIsAnnounced_WhateverTheReadSays()
        {
            var h = Started(follow: true);
            var created = Guid.NewGuid();
            h.World.Inputs.Add((created, "Intro", "ffmpeg_source", MediaCaps, false, 1.0));
            EngineEvents.InputCreated(h, created, "Intro", MediaCaps);

            // The read of the activity goes out with the other reads of the new input; the events come while it is under way (they are newer than the read, which says what OBS Studio said before).
            var ticket = h.Take()!;
            Assert.Contains(ReadKind.Activity, ticket.Requests.Select(request => request.Kind));
            Showing(h, created, true, "Intro");
            Active(h, created, true, "Intro");
            h.Complete(ticket);
            h.SettleWithTime();

            Assert.Equal((true, true), State(h, created));
        }

        [Fact]
        public void AnInputThatIsNew_WhenTheActivityIsNotFollowed_IsAnnouncedWithoutAReadOfTheActivity()
        {
            var reads = new List<ReadKind>();
            var h = Started(follow: false, reads: reads);
            var created = Guid.NewGuid();
            h.World.Inputs.Add((created, "Color", "color_source_v3", 32777, false, 1.0));

            EngineEvents.InputCreated(h, created, "Color");
            while (h.Take() is { } ticket)
            {
                reads.AddRange(ticket.Requests.Select(request => request.Kind));
                h.Complete(ticket);
            }

            Assert.DoesNotContain(ReadKind.Activity, reads);
            Assert.Null(Input(h, created).Active);
            Assert.Null(Input(h, created).Showing);
        }

        [Fact]
        public void AnActivityThatOBSStudioRefusesForGood_LeavesItUnknown_AndTheInputIsAnnounced()
        {
            var h = new EngineHarness(new ModelEngineOptions { HighVolume = ObsHighVolume.Activity });
            h.World.AddScene("Scene");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.Start();

            // A simulation that does not know the request answers 204: there is nothing to wait for.
            h.Settle(request => request.Kind == ReadKind.Activity ? (204, null) : h.World.Answer(request));

            Assert.True(h.Engine.IsSettled);
            Assert.Null(Input(h, camera).Active);
            Assert.Null(Input(h, camera).Showing);
        }

        [Fact]
        public void TheActivityOfAnInputThatOBSStudioDoesNotHave_IsReadAgain_AndTheListOfInputsDecides()
        {
            var h = new EngineHarness(new ModelEngineOptions { HighVolume = ObsHighVolume.Activity });
            h.World.AddScene("Scene");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.Start();
            h.Complete(h.Take()!);

            // The read of the activity says that the input is not there: the list is read again, and says that it is.
            var activity = h.Take()!;
            h.Complete(activity, request => request.Kind == ReadKind.Activity ? (600, null) : h.World.Answer(request));
            Assert.Empty(h.Engine.Snapshot().Inputs);

            h.SettleWithTime();

            Assert.Equal(camera, Assert.Single(h.Engine.Snapshot().Inputs).Uuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnActivityThatCouldNotBeRead_IsReadAgainAfterAWait()
        {
            var h = new EngineHarness(new ModelEngineOptions { HighVolume = ObsHighVolume.Activity });
            h.World.AddScene("Scene");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.World.Activity[camera] = (true, true);
            h.Start();
            h.Complete(h.Take()!);

            // OBS Studio is not ready.
            h.Complete(h.Take()!, request => request.Kind == ReadKind.Activity ? (207, null) : h.World.Answer(request));
            Assert.Empty(h.Engine.Snapshot().Inputs);

            h.SettleWithTime();

            Assert.True(Input(h, camera).Active);
            Assert.True(Input(h, camera).Showing);
        }

        [Fact]
        public void WhenTheConnectionIsMadeAgain_TheActivityIsReadAgain_AndWhatChangedIsTold()
        {
            var h = Started(follow: true, world => world.Activity[Uuid(world, "Camera")] = (true, true));
            var camera = Uuid(h, "Camera");
            h.End();

            // The program scene changed while the connection was down.
            h.World.Activity[camera] = (false, false);
            h.Start();
            h.SettleWithTime();

            Assert.False(Input(h, camera).Active);
            Assert.False(Input(h, camera).Showing);
            var changes = Changes(h, ModelChangeKind.InputActiveChanged, ModelChangeKind.InputShowingChanged);
            Assert.Equal(2, changes.Length);
            Assert.All(changes, change => Assert.Equal((camera, true, false), (change.Subject, (bool)change.OldValue!, (bool)change.NewValue!)));
        }

        [Fact]
        public void TheEventsOfTheActivity_AreNotActedOnWhileOBSStudioChangesItsSceneCollection()
        {
            var h = Started(follow: true);
            var camera = Uuid(h, "Camera");
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Other" }, 2);
            h.Engine.TakeChanges();

            Showing(h, camera, true);
            Active(h, camera, true);

            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind is ModelChangeKind.InputActiveChanged or ModelChangeKind.InputShowingChanged);
        }

        [Fact]
        public void ARenamedInput_HasItsNameInTheChangeOfItsActivity()
        {
            var h = Started(follow: true);
            var camera = Uuid(h, "Camera");
            h.Event("InputNameChanged", new { inputUuid = camera, oldInputName = "Camera", inputName = "Webcam" }, 8);
            h.Engine.TakeChanges();

            Showing(h, camera, true, "Webcam");

            Assert.Equal("Webcam", Assert.Single(h.Engine.TakeChanges()).Text);
        }
    }
}
