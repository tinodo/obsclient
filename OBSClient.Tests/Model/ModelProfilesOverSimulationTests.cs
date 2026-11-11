namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The profiles and the scene collections, over the whole stack: an <see cref="ObsModel"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio
    /// (<see cref="SimObsServer"/>), with the lags and the order of what OBS Studio really does. What the model shows is what the simulation holds (<see cref="ModelMirror"/>), after what
    /// someone else did and after what the application asked for, and a call that switches or makes something returns when the model shows it.
    /// </summary>
    public class ModelProfilesOverSimulationTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        private static SimQuirks Quirks(string preset)
        {
            return preset switch
            {
                "Ideal" => ModelSimRig.DefaultQuirks with { CollectionSwitchTime = SimRange.Exactly(300) },
                "Real" => SimQuirks.Real,
                "Hostile" => SimQuirks.Hostile with { CollectionSwitchTime = new SimRange(300, 1200) },
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        private static async Task<ModelSimRig> StartAsync(SimQuirks? quirks = null, ObsModelOptions? options = null, int seed = 5)
        {
            var rig = new ModelSimRig(quirks, options, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                return rig;
            }
            catch
            {
                await rig.DisposeAsync();
                throw;
            }
        }

        private static List<string> Watch(System.ComponentModel.INotifyPropertyChanged source)
        {
            var raised = new List<string>();
            source.PropertyChanged += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e.PropertyName ?? string.Empty);
                }
            };
            return raised;
        }

        private static string[] Told(List<string> list)
        {
            lock (list)
            {
                return list.ToArray();
            }
        }

        [Fact]
        public async Task TheModel_ShowsTheProfilesAndTheSceneCollections_AsOBSStudioHoldsThem()
        {
            await using var rig = await StartAsync();

            Assert.Equal("Untitled", rig.Model.Profiles.Current);
            Assert.Equal(["Untitled"], rig.Model.Profiles.Names);
            Assert.Equal("Untitled", rig.Model.SceneCollections.Current);
            Assert.Equal(["Untitled"], rig.Model.SceneCollections.Names);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public void BeforeTheModelIsConnected_ThereAreNoProfilesAndNoSceneCollections_AndTheObjectsAreTheSameForever()
        {
            using var model = new ObsModel();

            Assert.Null(model.Profiles.Current);
            Assert.Empty(model.Profiles.Names);
            Assert.Null(model.SceneCollections.Current);
            Assert.Empty(model.SceneCollections.Names);
            Assert.Same(model.Profiles, model.Profiles);
            Assert.Same(model.SceneCollections, model.SceneCollections);
        }

        [Fact]
        public async Task TheFirstSynchronization_TellsTheApplicationThatTheProfilesAndTheSceneCollectionsAreKnown()
        {
            await using var rig = new ModelSimRig();
            var profiles = Watch(rig.Model.Profiles);
            var collections = Watch(rig.Model.SceneCollections);

            await rig.ConnectAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("the first read", Patience);

            // The objects are the same before and after, and the application can hold them from the start (bind to them): it is told what they were not.
            await ModelRig.EventuallyAsync(() => Told(profiles).Contains("Current") && Told(collections).Contains("Current"), "the notifications of the first synchronization");
            Assert.Equal(["Names", "Current"], Told(profiles));
            Assert.Equal(["Names", "Current"], Told(collections));
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheProfiles_FollowWhatSomeoneElseDoes_AndTheApplicationIsToldWhatChanged(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var profiles = rig.Model.Profiles;
            var raised = Watch(profiles);

            rig.World(world => world.CreateProfile("Gaming"));
            await rig.ConvergedAsync("a profile that the user made", Patience);
            Assert.Equal("Gaming", profiles.Current);
            Assert.Equal(["Gaming", "Untitled"], profiles.Names);

            // The list is told before the profile is the current one, so that the current one is among the names when it is told.
            await ModelRig.EventuallyAsync(() => Told(raised).Contains("Current"), "the notification of the current profile");
            Assert.Equal(["Names", "Current"], Told(raised).Take(2));
            var records = rig.Changes.Where(change => ReferenceEquals(change.Entity, profiles)).ToArray();
            Assert.Contains(records, change => change.PropertyName == nameof(ObsProfiles.Names) && change.NewValue is IReadOnlyList<string> names && names.SequenceEqual(["Gaming", "Untitled"]));
            Assert.Contains(records, change => change.PropertyName == nameof(ObsProfiles.Current) && (string?)change.OldValue == "Untitled" && (string?)change.NewValue == "Gaming");

            rig.World(world => world.SwitchProfile("Untitled"));
            await rig.ConvergedAsync("a profile that the user switched to", Patience);
            Assert.Equal("Untitled", profiles.Current);
            Assert.Equal(["Gaming", "Untitled"], profiles.Names);

            // The user removes the profile that is current, and OBS Studio does not say that the list changed: it says that the current profile did.
            rig.World(world => world.CreateProfile("Podcast"));
            await rig.ConvergedAsync("a second profile that the user made", Patience);
            Assert.Equal("Podcast", profiles.Current);
            rig.World(world => world.RemoveProfile("Podcast"));
            await rig.ConvergedAsync("the current profile that the user removed", Patience);
            Assert.Equal(["Gaming", "Untitled"], profiles.Names);
            Assert.Equal("Gaming", profiles.Current);

            rig.World(world => world.RemoveProfile("Untitled"));
            await rig.ConvergedAsync("a profile that the user removed", Patience);
            Assert.Equal(["Gaming"], profiles.Names);
            Assert.Null(rig.Compare());
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task EveryCallAboutTheProfiles_EndsWhenTheModelShowsIt(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var profiles = rig.Model.Profiles;

            await profiles.CreateAsync("Gaming").WithTimeoutAsync(Patience);
            Assert.Equal("Gaming", profiles.Current);
            Assert.Contains("Gaming", profiles.Names);

            await profiles.CreateAsync("Podcast").WithTimeoutAsync(Patience);
            Assert.Equal("Podcast", profiles.Current);
            Assert.Equal(["Gaming", "Podcast", "Untitled"], profiles.Names);

            await profiles.SetCurrentAsync("Untitled").WithTimeoutAsync(Patience);
            Assert.Equal("Untitled", profiles.Current);

            // OBS Studio does nothing, and says nothing, when the profile is the current one: the model shows it already.
            await profiles.SetCurrentAsync("Untitled").WithTimeoutAsync(Patience);
            Assert.Equal("Untitled", profiles.Current);

            await profiles.RemoveAsync("Gaming").WithTimeoutAsync(Patience);
            Assert.Equal(["Podcast", "Untitled"], profiles.Names);
            await rig.ConvergedAsync("the profiles that the application made and removed", Patience);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task ProfilesThatAreMadeOneRightAfterTheOther_AreMadeInThatOrder_AndEachCallEndsWhenTheModelShowsIt()
        {
            await using var rig = await StartAsync(Quirks("Real"));
            var profiles = rig.Model.Profiles;

            // Changes that are asked for one after the other are made in that order (the user interface of OBS Studio does its tasks in the order in which they were queued): each call ends
            // when the model shows its change, and the profile that was made last is the current one.
            var one = profiles.CreateAsync("One");
            var two = profiles.CreateAsync("Two");
            await Task.WhenAll(one, two).WithTimeoutAsync(Patience);

            Assert.Equal("Two", profiles.Current);
            Assert.Equal(["One", "Two", "Untitled"], profiles.Names);
            await rig.ConvergedAsync("two profiles that were made at once", Patience);
        }

        [Fact]
        public async Task TheCallsThatOBSStudioRefuses_SaySoInTheTermsOfTheModel_AndChangeNothing()
        {
            await using var rig = await StartAsync();
            var profiles = rig.Model.Profiles;
            await profiles.CreateAsync("Gaming").WithTimeoutAsync(Patience);

            var unknown = await Assert.ThrowsAsync<ObsNotFoundException>(() => profiles.SetCurrentAsync("Gamin").WithTimeoutAsync(Patience));
            Assert.Equal("profile", unknown.Kind);
            Assert.Equal("Gamin", unknown.RequestedName);
            Assert.Contains("Gaming", unknown.Suggestions);
            await Assert.ThrowsAsync<ObsNotFoundException>(() => profiles.RemoveAsync("Nope").WithTimeoutAsync(Patience));

            var taken = await Assert.ThrowsAsync<ObsNameInUseException>(() => profiles.CreateAsync("Untitled").WithTimeoutAsync(Patience));
            Assert.Equal("Untitled", taken.Name);
            Assert.Contains("'Untitled'", taken.Message);
            Assert.Equal("Gaming", profiles.Current);
            Assert.Equal(["Gaming", "Untitled"], profiles.Names);
        }

        [Fact]
        public async Task TheCurrentProfile_IsNotRemoved_AndNothingIsSent()
        {
            await using var rig = await StartAsync();
            var profiles = rig.Model.Profiles;
            await profiles.CreateAsync("Gaming").WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("the profile that was made", Patience);

            // OBS Studio says a little later that the profile is the current one, and the model reads the list once more: that is over before the requests are counted.
            await ModelRig.EventuallyAsync(() => rig.Server.Run(sim => sim.NextDeadline is null), "OBS Studio to have nothing left to say", Patience);
            await rig.Model.WaitUntilSettledAsync(Patience);
            await Task.Delay(200);
            var requests = rig.Server.RequestCount;

            // OBS Studio would ask the user in its own window, and answer that it removed the profile.
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => profiles.RemoveAsync("Gaming"));
            Assert.Contains("current profile", refused.Message);
            Assert.Contains("Nothing was sent", refused.Message);

            Assert.Equal(requests, rig.Server.RequestCount);
            Assert.Equal(["Gaming", "Untitled"], profiles.Names);
        }

        [Fact]
        public async Task TheOnlyProfile_IsNotRemoved_BecauseItIsTheCurrentOne()
        {
            await using var rig = await StartAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Model.Profiles.RemoveAsync("Untitled"));

            Assert.Equal(["Untitled"], rig.Model.Profiles.Names);
        }

        [Fact]
        public async Task TheCallsAboutTheProfiles_NeedAConnection_AndAName()
        {
            using var model = new ObsModel();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => model.Profiles.SetCurrentAsync("Gaming"));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => model.Profiles.CreateAsync("Gaming"));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => model.Profiles.RemoveAsync("Gaming"));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => model.SceneCollections.SetCurrentAsync("Stream"));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => model.SceneCollections.CreateAsync("Stream"));
            Assert.Throws<ArgumentException>(() => { _ = model.Profiles.SetCurrentAsync(string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = model.Profiles.CreateAsync(string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = model.Profiles.RemoveAsync(string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = model.SceneCollections.SetCurrentAsync(string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = model.SceneCollections.CreateAsync(string.Empty); });
            Assert.Throws<ArgumentNullException>(() => { _ = model.Profiles.SetCurrentAsync(null!); });
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheSceneCollections_FollowWhatSomeoneElseDoes_AndTheApplicationIsToldWhatChanged(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var collections = rig.Model.SceneCollections;
            var raised = Watch(collections);

            rig.World(world => world.CreateCollection("Podcast"));
            await ModelRig.EventuallyAsync(() => collections.Current == "Podcast" && rig.Model.State == ObsModelState.Synchronized, "the scene collection that the user made", Patience);
            await rig.ConvergedAsync("a scene collection that the user made", Patience);

            Assert.Equal("Podcast", collections.Current);
            Assert.Equal(["Podcast", "Untitled"], collections.Names);
            await ModelRig.EventuallyAsync(() => Told(raised).Contains("Current"), "the notification of the loaded collection");
            Assert.Equal(["Names", "Current"], Told(raised).Take(2));

            rig.World(world => world.SwitchCollection("Untitled"));
            await ModelRig.EventuallyAsync(() => collections.Current == "Untitled" && rig.Model.State == ObsModelState.Synchronized, "the scene collection that the user switched back to", Patience);
            await rig.ConvergedAsync("the scene collection that the user switched back to", Patience);
            Assert.Equal("Untitled", collections.Current);
            Assert.Equal(["Podcast", "Untitled"], collections.Names);
            Assert.Null(rig.Compare());
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task LoadingASceneCollection_ReturnsWhenTheModelHasReadIt_AndWhatTheApplicationHeldIsGone(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            rig.World(world => world.CreateInput(world.CreateScene("Gaming"), "Mic", "wasapi_input_capture"));
            await rig.ConvergedAsync("the scene and the input", Patience);
            var gaming = rig.Model.Scenes["Gaming"];
            var mic = rig.Model.Inputs["Mic"];

            await rig.Model.SceneCollections.CreateAsync("Podcast").WithTimeoutAsync(Patience);

            // The call returns when the model is complete again: the scenes are those of the new collection, and what the application held is of the old one.
            Assert.Equal(ObsModelState.Synchronized, rig.Model.State);
            Assert.Equal("Podcast", rig.Model.SceneCollections.Current);
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Empty(rig.Model.Inputs);
            Assert.Equal(ObsObjectState.Gone, gaming.State);
            Assert.Equal(ObsObjectState.Gone, mic.State);
            Assert.Null(rig.Compare());

            await rig.Model.SceneCollections.SetCurrentAsync("Untitled").WithTimeoutAsync(Patience);

            Assert.Equal(ObsModelState.Synchronized, rig.Model.State);
            Assert.Equal("Untitled", rig.Model.SceneCollections.Current);
            Assert.True(rig.Model.Scenes.Contains("Gaming"));
            Assert.NotSame(gaming, rig.Model.Scenes["Gaming"]);
            Assert.True(rig.Model.Inputs.Contains("Mic"));
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task ALargeSceneCollection_IsWaitedFor_TillTheModelHasReadAllOfIt_AlsoWhenTheEventsAreGivenLittleTime()
        {
            // The events are given 50 ms to show the change, and then the model is asked to read the list of collections again: that read is done long before the model has read the sixty
            // microphones of the collection that it was switched to, and the call must not end with it.
            await using var rig = await StartAsync(Quirks("Real"), new ObsModelOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(50) });
            rig.World(world => world.CreateCollection("Big"));
            await ModelRig.EventuallyAsync(() => rig.Model.SceneCollections.Current == "Big" && rig.Model.State == ObsModelState.Synchronized, "the collection that the user made", Patience);
            rig.World(world =>
            {
                var scene = world.CreateScene("Mics");
                for (var i = 0; i < 60; i++)
                {
                    world.CreateInput(scene, $"Mic {i}", "wasapi_input_capture");
                }
            });
            await rig.ConvergedAsync("the sixty microphones", Patience);
            rig.World(world => world.SwitchCollection("Untitled"));
            await ModelRig.EventuallyAsync(() => rig.Model.SceneCollections.Current == "Untitled" && rig.Model.State == ObsModelState.Synchronized, "the first collection again", Patience);

            await rig.Model.SceneCollections.SetCurrentAsync("Big").WithTimeoutAsync(Patience);

            Assert.Equal(ObsModelState.Synchronized, rig.Model.State);
            Assert.Equal("Big", rig.Model.SceneCollections.Current);
            Assert.Equal(60, rig.Model.Inputs.Count);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task LoadingTheSceneCollectionThatIsLoaded_ReturnsAtOnce()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            var requests = rig.Server.RequestCount;

            await rig.Model.SceneCollections.SetCurrentAsync("Untitled").WithTimeoutAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("Untitled", rig.Model.SceneCollections.Current);
            Assert.Equal(ObsModelState.Synchronized, rig.Model.State);
            Assert.True(rig.Server.RequestCount >= requests);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task ASceneCollectionThatTheApplicationLoadsWhileAnotherIsLoading_IsRefusedBeforeItIsSent()
        {
            await using var rig = await StartAsync(Quirks("Ideal") with { CollectionSwitchTime = SimRange.Exactly(1500) });
            rig.World(world => world.CreateCollection("Podcast"));
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Synchronizing, "the model to know that OBS Studio is switching");

            await Assert.ThrowsAsync<ObsNotReadyException>(() => rig.Model.SceneCollections.SetCurrentAsync("Untitled"));
            await Assert.ThrowsAsync<ObsNotReadyException>(() => rig.Model.Profiles.SetCurrentAsync("Untitled"));

            await rig.ConvergedAsync("the scene collection that the user made", Patience);
            Assert.Equal("Podcast", rig.Model.SceneCollections.Current);
        }

        [Fact]
        public async Task TheSceneCollectionCalls_SayWhyOBSStudioRefusesThem()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            var collections = rig.Model.SceneCollections;

            var unknown = await Assert.ThrowsAsync<ObsNotFoundException>(() => collections.SetCurrentAsync("Untitle").WithTimeoutAsync(Patience));
            Assert.Equal("scene collection", unknown.Kind);
            Assert.Contains("Untitled", unknown.Suggestions);

            var taken = await Assert.ThrowsAsync<ObsNameInUseException>(() => collections.CreateAsync("Untitled").WithTimeoutAsync(Patience));
            Assert.Equal("Untitled", taken.Name);
            Assert.Equal("Untitled", collections.Current);
            Assert.Equal(["Untitled"], collections.Names);
        }

        [Fact]
        public async Task TheProfilesSurviveTheSwitchOfTheSceneCollection_AndTheApplicationIsToldNothingAboutThem()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            var profiles = rig.Model.Profiles;
            var raised = Watch(profiles);
            await profiles.CreateAsync("Gaming").WithTimeoutAsync(Patience);
            await ModelRig.EventuallyAsync(() => Told(raised).Contains("Current"), "the notification of the profile that was made");
            await Task.Delay(100);
            var told = Told(raised).Length;

            await rig.Model.SceneCollections.CreateAsync("Podcast").WithTimeoutAsync(Patience);
            await Task.Delay(300);

            // The model read the lists again with the rest of the new collection, and they are what they were: nothing is told.
            Assert.Equal("Gaming", profiles.Current);
            Assert.Equal(["Gaming", "Untitled"], profiles.Names);
            Assert.Equal(told, Told(raised).Length);
            Assert.Null(rig.Compare());
        }
    }
}
