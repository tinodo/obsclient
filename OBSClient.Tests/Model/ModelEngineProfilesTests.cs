namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds about the profiles and the scene collections of OBS Studio: the one that is current and the names of all, read with the rest and read again when OBS Studio
    /// says that they changed (never taken from the payload of an event), and what a switch of the scene collection does to them (nothing: they do not belong to a collection, but they are read
    /// again with the rest).
    /// </summary>
    public class ModelEngineProfilesTests
    {
        private const int Config = 2;

        private static EngineHarness Started(Action<FakeWorld>? setup = null)
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            setup?.Invoke(h.World);
            h.Start();
            h.SettleWithTime();
            h.Engine.TakeChanges();
            return h;
        }

        // The kinds of the requests of the next read, which is answered by the world; none when the engine has nothing to read.
        private static ReadKind[] NextRead(EngineHarness h)
        {
            if (h.Take() is not { } ticket)
            {
                return [];
            }

            var kinds = ticket.Requests.Select(request => request.Kind).ToArray();
            h.Complete(ticket);
            return kinds;
        }

        private static (int Code, object? Data) Refuse(int code)
        {
            return (code, null);
        }

        [Fact]
        public void TheProfilesAndTheSceneCollections_AreReadWithTheFirstReads()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.World.Profiles.AddRange(["Gaming", "Podcast"]);
            h.World.CurrentProfile = "Gaming";
            h.World.SceneCollections.Add("Stream");
            h.World.CurrentSceneCollection = "Stream";
            h.Start();

            var first = h.Take()!;
            Assert.Contains(first.Requests, request => request.Kind == ReadKind.Profiles && request.Type == RequestType.GetProfileList);
            Assert.Contains(first.Requests, request => request.Kind == ReadKind.SceneCollections && request.Type == RequestType.GetSceneCollectionList);
            h.Complete(first);
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal("Gaming", snapshot.Profiles!.Current);
            Assert.Equal(["Untitled", "Gaming", "Podcast"], snapshot.Profiles.Names);
            Assert.Equal("Stream", snapshot.SceneCollections!.Current);
            Assert.Equal(["Untitled", "Stream"], snapshot.SceneCollections.Names);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void TheModelIsNotSettled_UntilTheListsAreRead()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();

            // Everything but the two lists is answered: the model is not complete without them.
            var first = h.Take()!;
            h.Complete(first, request => request.Kind is ReadKind.Profiles or ReadKind.SceneCollections ? (207, null) : h.World.Answer(request));

            Assert.NotEqual(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
            Assert.False(h.Engine.IsSettled);
            Assert.Null(h.Engine.Snapshot().Profiles);
            Assert.Null(h.Engine.Snapshot().SceneCollections);

            h.SettleWithTime();

            Assert.Equal(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
            Assert.Equal("Untitled", h.Engine.Snapshot().Profiles!.Current);
        }

        [Fact]
        public void TheEventsAboutTheProfiles_AreFollowedByOneRead_AndTheModelHoldsWhatTheReadSays()
        {
            var h = Started();
            h.World.Profiles.Add("Gaming");
            h.World.CurrentProfile = "Gaming";

            // OBS Studio lists the profile and then switches to it: the two events are about one state of the list.
            h.Event("ProfileListChanged", new { profiles = new[] { "payload", "that is not taken" } }, Config);
            h.Event("CurrentProfileChanged", new { profileName = "also not taken" }, Config);

            Assert.Equal("Untitled", h.Engine.Snapshot().Profiles!.Current);
            Assert.Equal([ReadKind.Profiles], NextRead(h));
            Assert.Empty(NextRead(h));
            var profiles = h.Engine.Snapshot().Profiles!;
            Assert.Equal("Gaming", profiles.Current);
            Assert.Equal(["Untitled", "Gaming"], profiles.Names);
        }

        [Theory]
        [InlineData("CurrentProfileChanging")]
        [InlineData("CurrentProfileChanged")]
        [InlineData("ProfileListChanged")]
        public void EachEventAboutTheProfiles_MakesTheModelReadTheListAgain(string eventType)
        {
            var h = Started();

            h.Event(eventType, eventType == "ProfileListChanged" ? new { profiles = Array.Empty<string>() } : new { profileName = "Untitled" }, Config);

            Assert.Equal([ReadKind.Profiles], NextRead(h));
        }

        [Fact]
        public void TheEventAboutTheListOfTheSceneCollections_MakesTheModelReadTheListAgain()
        {
            var h = Started();
            h.World.SceneCollections.Add("Other");

            h.Event("SceneCollectionListChanged", new { sceneCollections = Array.Empty<string>() }, Config);

            Assert.Equal([ReadKind.SceneCollections], NextRead(h));
            Assert.Equal(["Untitled", "Other"], h.Engine.Snapshot().SceneCollections!.Names);
        }

        [Fact]
        public void AnEventThatComesWhileTheListIsRead_MakesTheModelReadItOnceMore()
        {
            var h = Started();
            h.Event("ProfileListChanged", new { profiles = new[] { "Untitled" } }, Config);
            var ticket = h.Take()!;
            Assert.Equal([ReadKind.Profiles], ticket.Requests.Select(request => request.Kind));

            // What the read answers was true before this event: it is read again.
            h.World.Profiles.Add("Gaming");
            h.Event("ProfileListChanged", new { profiles = new[] { "Untitled", "Gaming" } }, Config);
            h.Complete(ticket, request => (100, new { currentProfileName = "Untitled", profiles = new[] { "Untitled" } }));

            Assert.Equal(["Untitled"], h.Engine.Snapshot().Profiles!.Names);
            Assert.Equal([ReadKind.Profiles], NextRead(h));
            Assert.Equal(["Untitled", "Gaming"], h.Engine.Snapshot().Profiles!.Names);
        }

        [Fact]
        public void TheOrderOfTheNames_IsTheOrderOfOBSStudio_AndAChangeOfOrderIsAChange()
        {
            var h = Started(world => world.Profiles.AddRange(["B", "A"]));
            var before = h.Engine.Snapshot().Profiles!;

            h.World.Profiles.Clear();
            h.World.Profiles.AddRange(["A", "B", "Untitled"]);
            h.Event("ProfileListChanged", new { profiles = Array.Empty<string>() }, Config);
            NextRead(h);

            var after = h.Engine.Snapshot().Profiles!;
            Assert.Equal(["Untitled", "B", "A"], before.Names);
            Assert.Equal(["A", "B", "Untitled"], after.Names);
            Assert.NotEqual(before, after);
        }

        [Fact]
        public void TheLists_AreKeptWhileTheSceneCollectionChanges_AndReadAgainWithTheRest()
        {
            var h = Started();
            h.World.SceneCollections.Add("Other");
            h.World.CurrentSceneCollection = "Other";

            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, Config);
            Assert.True(h.Engine.IsSwitching);

            // Nothing is read while OBS Studio changes its collection, and what the model holds of the lists is what it knew.
            Assert.Empty(NextRead(h));
            Assert.Equal("Untitled", h.Engine.Snapshot().SceneCollections!.Current);
            Assert.Equal(["Untitled"], h.Engine.Snapshot().SceneCollections!.Names);
            Assert.NotNull(h.Engine.Snapshot().Profiles);

            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Other" }, Config);
            var asked = new List<ReadKind>();
            h.SettleWithTime(request =>
            {
                asked.Add(request.Kind);
                return h.World.Answer(request);
            });

            Assert.Contains(ReadKind.SceneCollections, asked);
            Assert.Contains(ReadKind.Profiles, asked);
            Assert.Equal("Other", h.Engine.Snapshot().SceneCollections!.Current);
            Assert.Equal(["Untitled", "Other"], h.Engine.Snapshot().SceneCollections!.Names);
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
        }

        [Fact]
        public void AnEventAboutTheProfiles_IsActedOn_WhileTheSceneCollectionChanges()
        {
            var h = Started();
            h.World.Profiles.Add("Gaming");
            h.World.CurrentProfile = "Gaming";
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, Config);

            // A profile does not belong to a scene collection: what OBS Studio says about it holds meanwhile too.
            h.Event("ProfileListChanged", new { profiles = Array.Empty<string>() }, Config);
            Assert.Empty(NextRead(h));
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Untitled" }, Config);
            h.SettleWithTime();

            Assert.Equal("Gaming", h.Engine.Snapshot().Profiles!.Current);
        }

        [Fact]
        public void AnOBSStudioThatRefusesTheListsForGood_HasNoListsToShow_AndTheModelSettles()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();

            // A simulation (or an obs-websocket) that does not know the requests: 204 is no moment that passes.
            h.SettleWithTime(request => request.Kind is ReadKind.Profiles or ReadKind.SceneCollections ? Refuse(204) : h.World.Answer(request));

            var snapshot = h.Engine.Snapshot();
            Assert.Null(snapshot.Profiles);
            Assert.Null(snapshot.SceneCollections);
            Assert.Equal(ModelEngineState.Synchronized, snapshot.State);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ARefusalThatPasses_IsReadAgain_AndTheListIsKnownWhenOBSStudioAnswers()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.World.Profiles.Add("Gaming");
            h.Start();
            var attempts = 0;

            // Not ready (207) is a refusal that passes: it is never believed, however often it comes.
            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Profiles && attempts++ < 5)
                {
                    return Refuse(207);
                }

                return h.World.Answer(request);
            });

            Assert.True(attempts > 5);
            Assert.Equal(["Untitled", "Gaming"], h.Engine.Snapshot().Profiles!.Names);
        }

        [Fact]
        public void ARefusalThatCameOnce_IsNotBelieved_ButReadAgain()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();
            var attempts = 0;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.SceneCollections && attempts++ == 0)
                {
                    return Refuse(204);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(2, attempts);
            Assert.Equal("Untitled", h.Engine.Snapshot().SceneCollections!.Current);
        }

        [Fact]
        public void ANewConnection_ReadsTheListsAgain()
        {
            var h = Started();
            h.End();
            h.World.Profiles.Add("Gaming");
            h.World.CurrentProfile = "Gaming";

            h.Start();
            h.SettleWithTime();

            Assert.Equal("Gaming", h.Engine.Snapshot().Profiles!.Current);
            Assert.Equal(["Untitled", "Gaming"], h.Engine.Snapshot().Profiles!.Names);
        }

        [Fact]
        public void ARefreshOfAList_ReadsIt_AndSaysWhenItIsRead()
        {
            var h = Started();
            h.World.Profiles.Add("Gaming");

            h.Engine.RequestRefresh(ReadKind.Profiles, Guid.Empty, h.Now);
            Assert.False(h.Engine.IsRefreshed(ReadKind.Profiles, Guid.Empty));
            Assert.True(h.Engine.IsRefreshed(ReadKind.SceneCollections, Guid.Empty));
            Assert.Equal([ReadKind.Profiles], NextRead(h));

            Assert.True(h.Engine.IsRefreshed(ReadKind.Profiles, Guid.Empty));
            Assert.Equal(["Untitled", "Gaming"], h.Engine.Snapshot().Profiles!.Names);
        }

        [Fact]
        public void TheNames_AreACopy_ThatNothingCanChange()
        {
            var names = new List<string> { "A", "B" };

            var list = new NameList("A", names);
            names.Add("C");

            Assert.Equal(["A", "B"], list.Names);
            Assert.True(list.Contains("B"));
            Assert.False(list.Contains("b"));
            Assert.False(list.Contains("C"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)list.Names).Add("D"));
        }

        [Fact]
        public void TwoListsAreTheSame_WhenTheCurrentOneAndTheNamesInTheirOrderAre()
        {
            var list = new NameList("A", ["A", "B"]);

            Assert.Equal(list, new NameList("A", ["A", "B"]));
            Assert.Equal(list.GetHashCode(), new NameList("A", ["A", "B"]).GetHashCode());
            Assert.NotEqual(list, new NameList("B", ["A", "B"]));
            Assert.NotEqual(list, new NameList("A", ["B", "A"]));
            Assert.NotEqual(list, new NameList("A", ["A", "B", "C"]));
            Assert.NotEqual(list, new NameList("a", ["A", "B"]));
            Assert.NotEqual(list, new NameList(null, ["A", "B"]));
            Assert.False(list.Equals(null));
        }
    }
}
