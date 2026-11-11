namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The profiles of the simulated OBS Studio: the list, what a switch, a new profile and a removed one announce and when, and what OBS Studio does with a request to remove the profile that is current.
    /// What the simulation does here was measured with OBS Studio 33.0.0 (LiveProfilesTests) and read from the source of obs-websocket 5.7.6 and OBS Studio 33.0.0 (OBSBasic_Profiles.cpp).
    /// </summary>
    public class SimProfileTests
    {
        // Nothing is slow but what a test looks at, so that it can say what is where at which time.
        private static readonly SimQuirks Timed = SimQuirks.Ideal with
        {
            ProfileCreateDelay = SimRange.Exactly(40),
            ProfileActivationTime = SimRange.Exactly(90),
            ProfileRemoveDelay = SimRange.Exactly(30),
            CollectionSwitchTime = SimRange.Exactly(500),
        };

        private static string[] ProfilesOf(SimEntry entry, string member = "profiles")
        {
            return entry.Data.GetProperty(member).EnumerateArray().Select(name => name.GetString()!).ToArray();
        }

        [Fact]
        public void GetProfileList_ListsTheProfiles_SortedWithoutRegardToCase_AndTheCurrentOne()
        {
            var h = new SimHarness(Timed);

            var first = h.Ok("GetProfileList");
            h.Ok("CreateProfile", new { profileName = "beta" });
            h.Ok("CreateProfile", new { profileName = "Alpha" });
            h.Settle();
            var later = h.Ok("GetProfileList");

            Assert.Equal("Untitled", first.String("currentProfileName"));
            Assert.Equal(["Untitled"], ProfilesOf(first));
            Assert.Equal("Alpha", later.String("currentProfileName"));
            Assert.Equal(["Alpha", "beta", "Untitled"], ProfilesOf(later));
            Assert.Equal(["Alpha", "beta", "Untitled"], h.Sim.Profiles);
            Assert.Equal("Alpha", h.Sim.CurrentProfile);
        }

        [Fact]
        public void ASwitch_AnnouncesTheProfileThatItLeaves_AndTheOneThatItLoads_AtTheSameMoment()
        {
            var h = new SimHarness(Timed);
            h.Ok("CreateProfile", new { profileName = "Second" });
            h.Settle();
            h.ClearLog();

            h.Ok("SetCurrentProfile", new { profileName = "Untitled" });
            h.Settle();

            Assert.Equal(["CurrentProfileChanging", "CurrentProfileChanged"], h.EventNames());
            var changing = h.Events("CurrentProfileChanging")[0];
            var changed = h.Events("CurrentProfileChanged")[0];
            Assert.Equal("Second", changing.String("profileName"));
            Assert.Equal("Untitled", changed.String("profileName"));
            Assert.Equal(SimIntent.Config, changing.Intent);
            Assert.Equal(SimIntent.Config, changed.Intent);
            Assert.Equal(changing.At, changed.At);
            Assert.Equal("Untitled", h.Sim.CurrentProfile);
            Assert.Equal(["Second", "Untitled"], h.Sim.Profiles);
        }

        [Fact]
        public void ASwitchToTheCurrentProfile_DoesNothing_AndAnnouncesNothing()
        {
            var h = new SimHarness(Timed);
            h.ClearLog();

            h.Ok("SetCurrentProfile", new { profileName = "Untitled" });
            h.Settle();

            Assert.Empty(h.EventNames());
            Assert.Equal("Untitled", h.Sim.CurrentProfile);
        }

        [Fact]
        public void ASwitchToAProfileThatIsNotThere_IsRefused_With600AndNoComment_AndAMissingNameWithTheFieldThatIsMissing()
        {
            var h = new SimHarness(Timed);

            var unknown = h.Call("SetCurrentProfile", new { profileName = "Nope" });
            var missing = h.Call("SetCurrentProfile");

            Assert.False(unknown.Succeeded);
            Assert.Equal(SimStatus.ResourceNotFound, unknown.Code);
            Assert.Null(unknown.Comment);
            Assert.False(missing.Succeeded);
            Assert.True(missing.Code is SimStatus.MissingRequestData or SimStatus.MissingRequestField);
            Assert.Equal("Untitled", h.Sim.CurrentProfile);
            Assert.Empty(h.EventNames());
        }

        [Fact]
        public void ANewProfile_IsAnsweredAtOnce_ListedALittleLater_AndThenSaidToBeTheCurrentOne_WithoutAChangingEvent()
        {
            var h = new SimHarness(Timed);
            h.ClearLog();
            var asked = h.Now;

            var answer = h.Call("CreateProfile", new { profileName = "Gaming" });

            // OBS Studio answers before it has made the profile: what it lists is what it listed before.
            Assert.True(answer.Succeeded);
            Assert.Equal(asked, answer.At);
            Assert.Equal(["Untitled"], h.Sim.Profiles);
            Assert.Empty(h.EventNames());

            h.Settle();

            Assert.Equal(["ProfileListChanged", "CurrentProfileChanged"], h.EventNames());
            var listed = h.Events("ProfileListChanged")[0];
            var changed = h.Events("CurrentProfileChanged")[0];
            Assert.Equal(["Gaming", "Untitled"], ProfilesOf(listed));
            Assert.Equal("Gaming", changed.String("profileName"));
            Assert.Equal(asked + TimeSpan.FromMilliseconds(40), listed.At);
            Assert.Equal(listed.At + TimeSpan.FromMilliseconds(90), changed.At);
            Assert.Equal("Gaming", h.Sim.CurrentProfile);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void ANewProfile_IsTheCurrentOneFromTheMomentThatItIsListed()
        {
            var h = new SimHarness(Timed);
            h.Ok("CreateProfile", new { profileName = "Gaming" });

            h.Run(60);
            var between = h.Ok("GetProfileList");
            h.Settle();

            // OBS Studio sets its current profile while it resets its video and its outputs, and tells so when it is done.
            Assert.Equal("Gaming", between.String("currentProfileName"));
            Assert.Equal(["Gaming", "Untitled"], ProfilesOf(between));
            Assert.DoesNotContain(h.Events("CurrentProfileChanged"), entry => entry.At < between.At);
        }

        [Fact]
        public void AProfileThatIsThereAlready_IsRefused_With601AndNoComment_AndOneThatWasAskedForTwiceIsMadeOnce()
        {
            var h = new SimHarness(Timed);

            var first = h.Call("CreateProfile", new { profileName = "Gaming" });
            var second = h.Call("CreateProfile", new { profileName = "Gaming" });
            h.Settle();
            var again = h.Call("CreateProfile", new { profileName = "Gaming" });

            // The second request came before the first was done, so it was not refused: OBS Studio does not make the profile twice.
            Assert.True(first.Succeeded);
            Assert.True(second.Succeeded);
            Assert.Equal(["Gaming", "Untitled"], h.Sim.Profiles);
            Assert.Equal(["ProfileListChanged", "CurrentProfileChanged"], h.EventNames());
            Assert.False(again.Succeeded);
            Assert.Equal(SimStatus.ResourceAlreadyExists, again.Code);
            Assert.Null(again.Comment);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void ARemovedProfile_IsRemovedALittleAfterTheAnswer_AndTheOthersAreListed()
        {
            var h = new SimHarness(Timed);
            h.Ok("CreateProfile", new { profileName = "Second" });
            h.Settle();
            h.Ok("SetCurrentProfile", new { profileName = "Untitled" });
            h.Settle();
            h.ClearLog();
            var asked = h.Now;

            var answer = h.Call("RemoveProfile", new { profileName = "Second" });

            Assert.True(answer.Succeeded);
            Assert.Equal(["Second", "Untitled"], h.Sim.Profiles);

            h.Settle();

            var listed = Assert.Single(h.Events("ProfileListChanged"));
            Assert.Equal(["Untitled"], ProfilesOf(listed));
            Assert.Equal(asked + TimeSpan.FromMilliseconds(30), listed.At);
            Assert.Equal(["ProfileListChanged"], h.EventNames());
            Assert.Equal(["Untitled"], h.Sim.Profiles);
            Assert.Equal("Untitled", h.Sim.CurrentProfile);
        }

        [Fact]
        public void ARequestToRemoveTheCurrentProfile_IsAnsweredWithSuccess_AndOBSStudioDoesNothing()
        {
            var h = new SimHarness(Timed);
            h.Ok("CreateProfile", new { profileName = "Second" });
            h.Settle();
            h.ClearLog();

            // It asks the user in its window, and answers at once as if it had removed it.
            h.Ok("RemoveProfile", new { profileName = "Second" });
            h.Settle();

            Assert.Empty(h.EventNames());
            Assert.Equal(["Second", "Untitled"], h.Sim.Profiles);
            Assert.Equal("Second", h.Sim.CurrentProfile);
        }

        [Fact]
        public void RemovingAProfile_IsRefused_WhenThereIsNoSuchProfile_600_AndBeforeThat_WhenItIsTheOnlyOne_603()
        {
            var h = new SimHarness(Timed);

            var unknown = h.Call("RemoveProfile", new { profileName = "Nope" });
            var only = h.Call("RemoveProfile", new { profileName = "Untitled" });

            Assert.Equal(SimStatus.ResourceNotFound, unknown.Code);
            Assert.Null(unknown.Comment);
            Assert.Equal(SimStatus.NotEnoughResources, only.Code);
            Assert.Null(only.Comment);
            Assert.Equal(["Untitled"], h.Sim.Profiles);
        }

        [Fact]
        public void TheUserWhoSwitches_MakesAProfile_OrRemovesAnother_IsAnnouncedAsARequestIs()
        {
            var h = new SimHarness(Timed);
            h.Sim.External.CreateProfile("Gaming");
            h.Settle();
            Assert.Equal(["ProfileListChanged", "CurrentProfileChanged"], h.EventNames());
            h.ClearLog();

            h.Sim.External.SwitchProfile("Untitled");
            h.Settle();
            Assert.Equal(["CurrentProfileChanging", "CurrentProfileChanged"], h.EventNames());
            h.ClearLog();

            h.Sim.External.RemoveProfile("Gaming");
            h.Settle();
            Assert.Equal(["ProfileListChanged"], h.EventNames());
            Assert.Equal(["Untitled"], h.Sim.Profiles);
        }

        [Fact]
        public void TheUserWhoRemovesTheCurrentProfile_SwitchesToTheOneThatComesFirstByTheBytesOfItsName_AndTheListIsNotAnnounced()
        {
            var h = new SimHarness(Timed);
            h.Sim.External.CreateProfile("apple");
            h.Settle();
            h.Sim.External.CreateProfile("Zebra");
            h.Settle();
            h.ClearLog();
            Assert.Equal("Zebra", h.Sim.CurrentProfile);

            h.Sim.External.RemoveProfile("Zebra");
            h.Settle();

            // OBS Studio keeps its profiles in a std::map: "Untitled" comes before "apple" there, though not in the list that it shows.
            Assert.Equal(["CurrentProfileChanging", "CurrentProfileChanged"], h.EventNames());
            Assert.Equal("Zebra", h.Events("CurrentProfileChanging")[0].String("profileName"));
            Assert.Equal("Untitled", h.Events("CurrentProfileChanged")[0].String("profileName"));
            Assert.Equal("Untitled", h.Sim.CurrentProfile);
            Assert.Equal(["apple", "Untitled"], h.Sim.Profiles);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void TheUserCannotRemoveTheOnlyProfile_OrOneThatIsNotThere_OrMakeOneThatIsThere()
        {
            var h = new SimHarness(Timed);

            Assert.IsType<SimFailure>(Assert.Throws<InvalidOperationException>(() => h.Sim.External.RemoveProfile("Untitled")).InnerException);
            Assert.IsType<SimFailure>(Assert.Throws<InvalidOperationException>(() => h.Sim.External.RemoveProfile("Nope")).InnerException);
            Assert.IsType<SimFailure>(Assert.Throws<InvalidOperationException>(() => h.Sim.External.SwitchProfile("Nope")).InnerException);
            Assert.IsType<SimFailure>(Assert.Throws<InvalidOperationException>(() => h.Sim.External.CreateProfile("Untitled")).InnerException);
            Assert.Equal(["Untitled"], h.Sim.Profiles);
        }

        [Fact]
        public void TheProfiles_AreNotPartOfTheSceneCollection_AndSurviveItsSwitch()
        {
            var h = new SimHarness(Timed);
            h.Ok("CreateProfile", new { profileName = "Gaming" });
            h.Settle();

            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Other" });
            h.Settle();

            Assert.Equal("Other", h.Sim.CurrentCollection);
            Assert.Equal("Gaming", h.Sim.CurrentProfile);
            Assert.Equal(["Gaming", "Untitled"], h.Sim.Profiles);
        }

        [Fact]
        public void ARequestThatIsAnsweredBeforeTheSwitch_HasItsProfileMadeAfterIt()
        {
            var h = new SimHarness(Timed);
            h.Ok("CreateProfile", new { profileName = "Gaming" });
            var switchId = h.Send("CreateSceneCollection", new { sceneCollectionName = "Other" });
            h.Run(5);
            Assert.True(h.Sim.IsSwitching);
            h.Run(100);

            // The profile was to be made at 40 ms, and OBS Studio is loading its collection until 500 ms: it is made when the user interface is free, and announced then.
            Assert.Equal(["Untitled"], h.Sim.Profiles);
            Assert.DoesNotContain("ProfileListChanged", h.EventNames());

            h.AwaitResponse(switchId);
            h.Settle();

            Assert.Equal(["Gaming", "Untitled"], h.Sim.Profiles);
            var names = h.EventNames();
            Assert.True(Array.IndexOf(names, "CurrentSceneCollectionChanged") < Array.IndexOf(names, "ProfileListChanged"));
            Assert.Contains("CurrentProfileChanged", names);
        }

        [Fact]
        public void TheSnapshot_HasTheProfilesAndTheSceneCollections_AndTwoSnapshotsDifferWhenTheyDo()
        {
            var h = new SimHarness(Timed);
            var before = h.Sim.Snapshot();

            h.Ok("CreateProfile", new { profileName = "Gaming" });
            h.Settle();
            var after = h.Sim.Snapshot();

            Assert.Equal("Untitled", before.Profiles!.Current);
            Assert.Equal("Gaming", after.Profiles!.Current);
            Assert.Equal(["Gaming", "Untitled"], after.Profiles.Names);
            Assert.Equal("Untitled", after.SceneCollections!.Current);
            Assert.Equal(["Untitled"], after.SceneCollections.Names);
            Assert.NotEqual(before, after);
            Assert.Contains("profiles: \"Gaming\" of [\"Gaming\", \"Untitled\"]", after.ToString());
        }
    }
}
