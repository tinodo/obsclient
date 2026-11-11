namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The calls about the profiles and the scene collections, against a stand-in for OBS Studio that answers as the test says: the requests that they send, how long they wait for the
    /// answer, and how they end when OBS Studio says that it did something and the model does not show it.
    /// </summary>
    public class ModelProfilesCallTests
    {
        private const int Config = 2;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsModelOptions Options()
        {
            return new ObsModelOptions
            {
                ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromMilliseconds(300),
                SynchronizationTimeout = TimeSpan.FromSeconds(5),
            };
        }

        private static Task<CallFixture> StartAsync()
        {
            return CallFixture.StartAsync(Options(), world =>
            {
                world.Profiles.AddRange(["Gaming", "Podcast"]);
                world.CurrentProfile = "Gaming";
                world.SceneCollections.Add("Other");
            });
        }

        private static async Task SendAsync(CallFixture test, string type, object data)
        {
            await test.Rig.Responder.SendEventAsync(type, data, Config);
        }

        [Fact]
        public async Task SettingTheCurrentProfile_SendsTheNameOfTheProfile_AndEndsWhenTheModelShowsIt()
        {
            await using var test = await StartAsync();
            Assert.Equal("Gaming", test.Model.Profiles.Current);
            test.Answer("SetCurrentProfile", data =>
            {
                var name = data!.Value.GetProperty("profileName").GetString()!;
                var old = test.Rig.Responder.World.CurrentProfile;
                test.Rig.Responder.Change(world => world.CurrentProfile = name);
                _ = Task.Run(async () =>
                {
                    await SendAsync(test, "CurrentProfileChanging", new { profileName = old });
                    await SendAsync(test, "CurrentProfileChanged", new { profileName = name });
                });
                return (100, null);
            });

            await test.Model.Profiles.SetCurrentAsync("Podcast").WithTimeoutAsync(Timeout);

            Assert.Equal("Podcast", test.Model.Profiles.Current);
            var sent = Assert.Single(test.AllSent("SetCurrentProfile"));
            Assert.Equal("Podcast", sent.GetProperty("profileName").GetString());
        }

        [Fact]
        public async Task MakingAProfile_SendsTheNameOfTheProfile_AndEndsWhenTheModelListsItAndOBSStudioHasSwitchedToIt()
        {
            await using var test = await StartAsync();
            test.Answer("CreateProfile", data =>
            {
                var name = data!.Value.GetProperty("profileName").GetString()!;

                // OBS Studio answers first, lists the profile a little later, and says a little after that that it switched to it.
                _ = Task.Run(async () =>
                {
                    await Task.Delay(40);
                    test.Rig.Responder.Change(world =>
                    {
                        world.Profiles.Add(name);
                        world.CurrentProfile = "Gaming";
                    });
                    await SendAsync(test, "ProfileListChanged", new { profiles = new[] { "Untitled", "Gaming", "Podcast", name } });
                    await Task.Delay(60);
                    test.Rig.Responder.Change(world => world.CurrentProfile = name);
                    await SendAsync(test, "CurrentProfileChanged", new { profileName = name });
                });
                return (100, null);
            });

            await test.Model.Profiles.CreateAsync("Stream").WithTimeoutAsync(Timeout);

            // The call did not end when the list had the profile and the current profile was the old one still.
            Assert.Equal("Stream", test.Model.Profiles.Current);
            Assert.Equal(["Gaming", "Podcast", "Stream", "Untitled"], test.Model.Profiles.Names.Order(StringComparer.Ordinal).ToArray());
            Assert.Equal("Stream", test.AllSent("CreateProfile").Single().GetProperty("profileName").GetString());
        }

        [Fact]
        public async Task AProfileThatOBSStudioSaysItMade_AndThatTheModelDoesNotList_IsNotConfirmed()
        {
            await using var test = await StartAsync();
            test.Answer("CreateProfile");

            // OBS Studio makes no profile of a name that it can not make a folder name of, and still answers that it did.
            var failure = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.Profiles.CreateAsync("What?").WithTimeoutAsync(Timeout));

            Assert.Contains("creating profile 'What?'", failure.Message);
            Assert.DoesNotContain("What?", test.Model.Profiles.Names);
        }

        [Fact]
        public async Task AProfileThatOBSStudioSaysItRemoved_AndThatTheModelStillLists_IsNotConfirmed()
        {
            await using var test = await StartAsync();
            test.Answer("RemoveProfile");

            var failure = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.Profiles.RemoveAsync("Podcast").WithTimeoutAsync(Timeout));

            Assert.Contains("removing profile 'Podcast'", failure.Message);
            Assert.Contains("Podcast", test.Model.Profiles.Names);
        }

        [Fact]
        public async Task RemovingAProfile_SendsItsName_AndEndsWhenTheListHasNoMoreOfIt()
        {
            await using var test = await StartAsync();
            test.Answer("RemoveProfile", data =>
            {
                var name = data!.Value.GetProperty("profileName").GetString()!;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(30);
                    test.Rig.Responder.Change(world => world.Profiles.Remove(name));
                    await SendAsync(test, "ProfileListChanged", new { profiles = new[] { "Untitled", "Gaming" } });
                });
                return (100, null);
            });

            await test.Model.Profiles.RemoveAsync("Podcast").WithTimeoutAsync(Timeout);

            Assert.DoesNotContain("Podcast", test.Model.Profiles.Names);
            Assert.Equal("Podcast", test.AllSent("RemoveProfile").Single().GetProperty("profileName").GetString());
        }

        [Fact]
        public async Task WhenOBSStudioHasTooFewProfiles_ItsRefusalIsAnObsResponseException_WithTheCode()
        {
            await using var test = await StartAsync();
            test.Answer("RemoveProfile", 603, "Your profile list must have at least two profiles");

            var failure = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Profiles.RemoveAsync("Podcast").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.NotEnoughResources, failure.ErrorCode);
        }

        [Fact]
        public async Task AnAnswerThatTheModelRefusesToWaitFor_IsAnUnknownOutcome_ForAProfile()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentProfile", _ =>
            {
                Thread.Sleep(900);
                return (100, null);
            });

            // OBS Studio did not answer in the time that a change is given (300 ms here): it is not known whether it switched.
            await Assert.ThrowsAsync<ObsUnknownOutcomeException>(() => test.Model.Profiles.SetCurrentAsync("Podcast").WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task ASceneCollectionThatIsLoadedForLongerThanAChangeIsGiven_IsWaitedFor_SinceOBSStudioAnswersWhenItIsDone()
        {
            await using var test = await StartAsync();
            Assert.Equal("Untitled", test.Model.SceneCollections.Current);
            test.Answer("SetCurrentSceneCollection", data =>
            {
                var name = data!.Value.GetProperty("sceneCollectionName").GetString()!;
                _ = Task.Run(async () => await SendAsync(test, "CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }));

                // The collection takes three times as long to load as a change is given (300 ms), and the model has to read it afterwards too.
                Thread.Sleep(900);
                test.Rig.Responder.Change(world =>
                {
                    world.CurrentSceneCollection = name;
                    world.Scenes.Clear();
                    world.Items.Clear();
                    world.Inputs.Clear();
                    world.Program = null;
                    world.AddScene("Fresh");
                });
                _ = Task.Run(async () => await SendAsync(test, "CurrentSceneCollectionChanged", new { sceneCollectionName = name }));
                return (100, null);
            });

            await test.Model.SceneCollections.SetCurrentAsync("Other").WithTimeoutAsync(Timeout);

            Assert.Equal("Other", test.Model.SceneCollections.Current);
            Assert.Equal(ObsModelState.Synchronized, test.Model.State);
            Assert.Equal(["Fresh"], test.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal("Other", test.AllSent("SetCurrentSceneCollection").Single().GetProperty("sceneCollectionName").GetString());
        }

        [Fact]
        public async Task ASceneCollectionThatOBSStudioDoesNotLoad_ThoughItSaidThatItDid_IsNotConfirmed()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentSceneCollection");

            var failure = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.SceneCollections.SetCurrentAsync("Other").WithTimeoutAsync(Timeout));

            Assert.Contains("loading the scene collection 'Other'", failure.Message);
            Assert.Equal("Untitled", test.Model.SceneCollections.Current);
        }

        [Fact]
        public async Task ASceneCollectionThatOBSStudioLoadsWithoutTheEventsThatSayIt_IsShownCompletely_WhenTheCallEnds()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentSceneCollection", data =>
            {
                var name = data!.Value.GetProperty("sceneCollectionName").GetString()!;
                test.Rig.Responder.Change(world =>
                {
                    world.CurrentSceneCollection = name;
                    world.Scenes.Clear();
                    world.Items.Clear();
                    world.Inputs.Clear();
                    world.Program = null;
                    world.AddScene("Fresh");
                });

                // The events that say that OBS Studio changed its collection never come: the model is told by the read that it asks for when they do not.
                return (100, null);
            });

            await test.Model.SceneCollections.SetCurrentAsync("Other").WithTimeoutAsync(Timeout);

            Assert.Equal("Other", test.Model.SceneCollections.Current);
            Assert.Equal(ObsModelState.Synchronized, test.Model.State);
            Assert.Equal(["Fresh"], test.Model.Scenes.Select(scene => scene.Name).ToArray());
        }

        [Fact]
        public async Task TheEventsOfASwitchThatTheModelReadAlready_ComeLate_AndWhatTheApplicationGotIsStillLive()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentSceneCollection", data =>
            {
                var name = data!.Value.GetProperty("sceneCollectionName").GetString()!;
                test.Rig.Responder.Change(world =>
                {
                    world.CurrentSceneCollection = name;
                    world.Scenes.Clear();
                    world.Items.Clear();
                    world.Inputs.Clear();
                    world.Program = null;
                    world.AddScene("Fresh");
                });
                return (100, null);
            });
            await test.Model.SceneCollections.SetCurrentAsync("Other").WithTimeoutAsync(Timeout);
            var fresh = test.Model.Scenes["Fresh"];

            // The events of the switch come after the call ended, and after the model read the collection that OBS Studio loaded. They are the events of a switch that the model has seen.
            await SendAsync(test, "CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" });
            await SendAsync(test, "CurrentSceneCollectionChanged", new { sceneCollectionName = "Other" });
            test.Rig.Responder.Change(world => world.Profiles.Add("Barrier"));
            await SendAsync(test, "ProfileListChanged", new { profiles = Array.Empty<string>() });
            await ModelRig.EventuallyAsync(() => test.Model.Profiles.Names.Contains("Barrier"), "the event after the late events");

            Assert.Equal(ObsObjectState.Live, fresh.State);
            Assert.Same(fresh, test.Model.Scenes["Fresh"]);
            Assert.Equal(ObsModelState.Synchronized, test.Model.State);
            Assert.Equal("Other", test.Model.SceneCollections.Current);
        }

        [Fact]
        public async Task ASceneCollectionThatOBSStudioSaysItMade_AndThatTheModelDoesNotList_IsNotConfirmed()
        {
            await using var test = await StartAsync();
            test.Answer("CreateSceneCollection");

            var failure = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.SceneCollections.CreateAsync("New").WithTimeoutAsync(Timeout));

            Assert.Contains("creating the scene collection 'New'", failure.Message);
            Assert.DoesNotContain("New", test.Model.SceneCollections.Names);
        }

        [Fact]
        public async Task ASceneCollectionThatTheModelListsAndOBSStudioDidNotLoad_IsNotConfirmedEither()
        {
            await using var test = await StartAsync();
            test.Answer("CreateSceneCollection", _ =>
            {
                // OBS Studio lists the collection, and the collection that is loaded is the one from before.
                test.Rig.Responder.Change(world => world.SceneCollections.Add("New"));
                return (100, null);
            });

            var failure = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.SceneCollections.CreateAsync("New").WithTimeoutAsync(Timeout));

            Assert.Contains("creating the scene collection 'New'", failure.Message);
            Assert.Contains("'Untitled' as the one that is loaded", failure.Message);
            Assert.Contains("New", test.Model.SceneCollections.Names);
            Assert.Equal("Untitled", test.Model.SceneCollections.Current);
        }

        [Fact]
        public async Task TheSceneCollectionThatIsLoaded_IsNotConfirmedTwice_EvenWhenOBSStudioSaysNothing()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentSceneCollection");

            // The model shows the collection already: OBS Studio does nothing for it, and the call ends at once.
            await test.Model.SceneCollections.SetCurrentAsync("Untitled").WithTimeoutAsync(Timeout);

            Assert.Equal("Untitled", test.Model.SceneCollections.Current);
        }

        [Fact]
        public async Task TheRequestsThatNeedAName_AreSentWithTheNameAsItIs()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentProfile", 600);
            test.Answer("SetCurrentSceneCollection", 600);

            await Assert.ThrowsAsync<ObsNotFoundException>(() => test.Model.Profiles.SetCurrentAsync(" Gaming ").WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotFoundException>(() => test.Model.SceneCollections.SetCurrentAsync("Écran").WithTimeoutAsync(Timeout));

            Assert.Equal(" Gaming ", test.AllSent("SetCurrentProfile").Single().GetProperty("profileName").GetString());
            Assert.Equal("Écran", test.AllSent("SetCurrentSceneCollection").Single().GetProperty("sceneCollectionName").GetString());
            Assert.Equal(JsonValueKind.Object, test.AllSent("SetCurrentProfile").Single().ValueKind);
        }
    }
}
