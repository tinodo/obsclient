namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// Whether an input is on the air and whether it is shown, as the model shows it when it is asked to follow the activity (<see cref="ObsHighVolume.Activity"/>), and what it does when it is not: what the
    /// objects say, what the model reads and what it tells.
    /// </summary>
    public class ModelActivityTests
    {
        private const int ActiveEvents = 1 << 17;
        private const int ShowEvents = 1 << 18;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsModelOptions Following()
        {
            var options = CallFixture.DefaultOptions();
            options.HighVolume = ObsHighVolume.Activity;
            return options;
        }

        private static Task<CallFixture> StartAsync(bool follow, Action<FakeWorld>? setup = null)
        {
            return CallFixture.StartAsync(follow ? Following() : null, setup);
        }

        private static Guid Uuid(FakeWorld world, string name)
        {
            return world.Inputs.Single(input => input.Name == name).Uuid;
        }

        private static Task SendShowingAsync(CallFixture test, ObsInput input, bool showing)
        {
            return test.Rig.Responder.SendEventAsync("InputShowStateChanged", new { inputName = input.Name, inputUuid = input.Uuid, videoShowing = showing }, ShowEvents);
        }

        private static Task SendActiveAsync(CallFixture test, ObsInput input, bool active)
        {
            return test.Rig.Responder.SendEventAsync("InputActiveStateChanged", new { inputName = input.Name, inputUuid = input.Uuid, videoActive = active }, ActiveEvents);
        }

        // How many times the model told that a property of the inputs changed.
        private static int Told(CallFixture test, string property)
        {
            return test.Rig.Changes.Count(change => change.PropertyName == property);
        }

        [Fact]
        public async Task TheActivity_IsNull_WhenTheModelWasNotAskedToFollowIt_ItIsNeverRead_AndItsEventsAreNotActedOn()
        {
            await using var test = await StartAsync(follow: false);

            await SendShowingAsync(test, test.Camera, true);
            await SendActiveAsync(test, test.Camera, true);
            await Task.Delay(100);
            await test.Model.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);

            Assert.Null(test.Camera.IsActive);
            Assert.Null(test.Camera.IsShowing);
            Assert.Null(test.Mic.IsActive);
            Assert.Equal(0, test.Rig.Responder.RequestLog.Count(type => type == "GetSourceActive"));
            Assert.Equal(0, Told(test, nameof(ObsInput.IsActive)) + Told(test, nameof(ObsInput.IsShowing)));
        }

        [Fact]
        public async Task TheActivity_IsWhatOBSStudioSays_AndIsReadOnceForEachInput_WhenTheModelFollowsIt()
        {
            await using var test = await StartAsync(follow: true, world =>
            {
                world.Activity[Uuid(world, "Camera")] = (true, true);
                world.Activity[Uuid(world, "Spare")] = (false, true);
            });

            Assert.True(test.Camera.IsActive);
            Assert.True(test.Camera.IsShowing);
            Assert.False(test.Model.Inputs["Spare"].IsActive);
            Assert.True(test.Model.Inputs["Spare"].IsShowing);
            Assert.False(test.Mic.IsActive);
            Assert.False(test.Mic.IsShowing);
            Assert.Equal(3, test.Rig.Responder.RequestLog.Count(type => type == "GetSourceActive"));
            Assert.Equal(0, Told(test, nameof(ObsInput.IsActive)) + Told(test, nameof(ObsInput.IsShowing)));
        }

        [Fact]
        public async Task AnActivityThatChanges_IsTold_WithTheValueBeforeAndAfter_InTheOrderOfTheEvents()
        {
            await using var test = await StartAsync(follow: true);
            var camera = test.Camera;

            await SendShowingAsync(test, camera, true);
            await SendActiveAsync(test, camera, true);
            await ModelRig.EventuallyAsync(() => Told(test, nameof(ObsInput.IsActive)) == 1 && Told(test, nameof(ObsInput.IsShowing)) == 1, "the changes of the activity to be told");

            Assert.True(camera.IsActive);
            Assert.True(camera.IsShowing);
            var changes = test.Rig.Changes.Where(change => change.PropertyName is nameof(ObsInput.IsActive) or nameof(ObsInput.IsShowing)).ToArray();
            Assert.Equal([nameof(ObsInput.IsShowing), nameof(ObsInput.IsActive)], changes.Select(change => change.PropertyName!).ToArray());
            Assert.All(changes, change =>
            {
                Assert.Equal(ObsChangeKind.PropertyChanged, change.Kind);
                Assert.Same(camera, change.Entity);
                Assert.Equal(false, change.OldValue);
                Assert.Equal(true, change.NewValue);
            });
        }

        [Fact]
        public async Task AnInputThatGoesOffTheAir_IsTold_AndTheModelSaysSoAfterwards()
        {
            await using var test = await StartAsync(follow: true, world => world.Activity[Uuid(world, "Camera")] = (true, true));
            var camera = test.Camera;

            await SendShowingAsync(test, camera, false);
            await SendActiveAsync(test, camera, false);
            await ModelRig.EventuallyAsync(() => Told(test, nameof(ObsInput.IsActive)) == 1 && Told(test, nameof(ObsInput.IsShowing)) == 1, "the changes of the activity to be told");

            Assert.False(camera.IsActive);
            Assert.False(camera.IsShowing);
            var change = Assert.Single(test.Rig.Changes, entry => entry.PropertyName == nameof(ObsInput.IsActive));
            Assert.Equal(true, change.OldValue);
            Assert.Equal(false, change.NewValue);
        }

        [Fact]
        public async Task AnEvent_ThatSaysWhatTheModelHolds_IsNotTold()
        {
            await using var test = await StartAsync(follow: true, world => world.Activity[Uuid(world, "Camera")] = (true, false));
            var camera = test.Camera;

            await SendActiveAsync(test, camera, true);
            await SendShowingAsync(test, camera, false);
            await Task.Delay(100);
            await test.Model.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(0, Told(test, nameof(ObsInput.IsActive)) + Told(test, nameof(ObsInput.IsShowing)));
        }

        [Fact]
        public async Task AnInputThatIsShownButNotOnTheAir_IsPossible_AndTheTwoAreToldApart()
        {
            await using var test = await StartAsync(follow: true);
            var spare = test.Model.Inputs["Spare"];

            await SendShowingAsync(test, spare, true);
            await ModelRig.EventuallyAsync(() => Told(test, nameof(ObsInput.IsShowing)) == 1, "the change to be told");

            Assert.False(spare.IsActive);
            Assert.True(spare.IsShowing);
            Assert.Equal(0, Told(test, nameof(ObsInput.IsActive)));
        }

        [Fact]
        public async Task TheActivity_OfAnInputThatWasMadeAfterTheConnection_IsThere_WhenTheInputIs()
        {
            await using var test = await StartAsync(follow: true);
            var created = Guid.Empty;
            test.Rig.Responder.Change(world =>
            {
                created = world.AddInput("Logo");
                world.Activity[created] = (true, true);
            });

            await test.Rig.Responder.SendEventAsync("InputCreated", new { inputName = "Logo", inputUuid = created, inputKind = "color_source_v3", unversionedInputKind = "color_source", inputKindCaps = 32777, inputSettings = new { }, defaultInputSettings = new { } }, 8);
            await ModelRig.EventuallyAsync(() => test.Model.Inputs.Contains("Logo"), "the new input to be shown");

            Assert.True(test.Model.Inputs["Logo"].IsActive);
            Assert.True(test.Model.Inputs["Logo"].IsShowing);
            Assert.Equal(0, Told(test, nameof(ObsInput.IsActive)) + Told(test, nameof(ObsInput.IsShowing)));
        }

        [Fact]
        public async Task TheActivity_IsReadAgain_AfterTheConnectionIsMadeAgain_AndEachPropertyThatChangedIsToldOnce()
        {
            var options = Following();
            options.AutoReconnect = true;
            var resynchronized = 0;
            await using var test = await CallFixture.StartAsync(
                options,
                world => world.Activity[Uuid(world, "Camera")] = (true, true),
                model => model.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized));
            var camera = test.Camera;
            Assert.True(camera.IsActive);
            var properties = new List<string>();
            camera.PropertyChanged += (_, e) =>
            {
                lock (properties)
                {
                    properties.Add(e.PropertyName!);
                }
            };

            // The first read is announced after the connection is made, and it is not the read that this test is about.
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "the first read to be announced", Timeout);

            // The scene that shows the camera went off the air while the connection was down, and the model reconnects by itself.
            test.Rig.Responder.Change(world => world.Activity[camera.Uuid] = (false, false));
            test.Rig.Responder.AbortConnection();

            // While the model is read again nobody is told one by one: each property is told once when it is complete, and Resynchronized comes after them.
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 2, "the model to be read again", Timeout);
            Assert.False(camera.IsActive);
            Assert.False(camera.IsShowing);
            lock (properties)
            {
                Assert.Equal(1, properties.Count(name => name == nameof(ObsInput.IsActive)));
                Assert.Equal(1, properties.Count(name => name == nameof(ObsInput.IsShowing)));
            }

            Assert.Equal(0, Told(test, nameof(ObsInput.IsActive)) + Told(test, nameof(ObsInput.IsShowing)));
        }
    }
}
