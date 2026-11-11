namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The canvases of the simulated OBS Studio: the list, what a plugin that makes, renames and removes a canvas announces, how names are made unique, what the main canvas does not let anyone do,
    /// and what a scene collection saves and loads. What the simulation does here was read from the source of OBS Studio (obs.c, obs-canvas.c, OBSBasic_Canvases.cpp) and obs-websocket 5.7.6, and most of it was checked
    /// on OBS Studio 33.0.0 with a script that makes, renames, removes and releases canvases (tools/live/canvas-driver.lua, see LiveCanvasModelTests): the order of the list (a canvas that is renamed is last), the names that
    /// are made unique, a canvas that is removed while something holds it (it stays listed until it is released, and one CanvasRemoved says it), the canvases that a scene collection saves and loads (the UUID and the flags stay, the
    /// video is null) and the list of the main canvas (the capture). Only read in the source: what the main canvas does not allow, and what is done with an empty name.
    /// </summary>
    public class SimCanvasTests
    {
        private static readonly string[] VideoFields = ["baseHeight", "baseWidth", "fpsDenominator", "fpsNumerator", "outputHeight", "outputWidth"];

        private static JsonElement[] Listed(SimHarness h)
        {
            return h.Ok("GetCanvasList").Data.GetProperty("canvases").EnumerateArray().ToArray();
        }

        private static string[] NamesOf(SimHarness h)
        {
            return Listed(h).Select(canvas => canvas.GetProperty("canvasName").GetString()!).ToArray();
        }

        private static string[] NamesOf(SimSnapshot snapshot)
        {
            return snapshot.Canvases!.Select(canvas => canvas.Name).ToArray();
        }

        private static string[] FlagNames(JsonElement canvas)
        {
            return canvas.GetProperty("canvasFlags").EnumerateObject().Where(flag => flag.Value.GetBoolean()).Select(flag => flag.Name).Order(StringComparer.Ordinal).ToArray();
        }

        [Fact]
        public void GetCanvasList_ListsTheMainCanvas_WithTheFlagsOfARealObsStudio_AndTheVideoOfTheProfile()
        {
            var h = new SimHarness();

            var canvases = Listed(h);
            var video = h.Ok("GetVideoSettings");

            var main = Assert.Single(canvases);
            Assert.Equal("Main", main.GetProperty("canvasName").GetString());
            Assert.Equal(SimWorld.MainCanvasUuid, main.GetProperty("canvasUuid").GetString());
            Assert.Equal(["ACTIVATE", "MAIN", "MIX_AUDIO", "SCENE_REF"], FlagNames(main));
            foreach (var field in VideoFields)
            {
                Assert.Equal(video.Int(field), main.GetProperty("canvasVideoSettings").GetProperty(field).GetInt32());
            }

            Assert.Equal(["Main"], NamesOf(h.Sim.Snapshot()));
        }

        [Fact]
        public void TheVideoOfTheMainCanvas_IsTheVideoOfTheProfile_AlsoAfterItWasChanged()
        {
            var h = new SimHarness();
            h.Ok("SetVideoSettings", new { baseWidth = 1280, baseHeight = 720, outputWidth = 640, outputHeight = 360, fpsNumerator = 25, fpsDenominator = 1 });

            var video = Listed(h).Single().GetProperty("canvasVideoSettings");

            Assert.Equal(1280, video.GetProperty("baseWidth").GetInt32());
            Assert.Equal(360, video.GetProperty("outputHeight").GetInt32());
            Assert.Equal(25, video.GetProperty("fpsNumerator").GetInt32());
        }

        [Fact]
        public void ACanvasThatAPluginMakes_IsAnnounced_ListedAfterTheMainCanvas_AndSaysNullForTheVideoThatItDoesNotHave()
        {
            var h = new SimHarness();
            h.ClearLog();

            var uuid = h.Sim.External.CreateCanvas("Overlay", SimCanvasFlags.Program);
            h.Settle();

            var created = Assert.Single(h.Events("CanvasCreated"));
            Assert.Equal("Overlay", created.String("canvasName"));
            Assert.Equal(uuid, created.Uuid("canvasUuid"));
            Assert.Equal(SimIntent.Canvases, created.Intent);
            var canvases = Listed(h);
            Assert.Equal(["Main", "Overlay"], canvases.Select(canvas => canvas.GetProperty("canvasName").GetString()!).ToArray());
            Assert.Equal(["ACTIVATE", "MIX_AUDIO", "SCENE_REF"], FlagNames(canvases[1]));
            Assert.All(VideoFields, field => Assert.Equal(JsonValueKind.Null, canvases[1].GetProperty("canvasVideoSettings").GetProperty(field).ValueKind));
            Assert.Equal(["Main", "Overlay"], NamesOf(h.Sim.Snapshot()));
        }

        [Fact]
        public void ACanvasThatHasAVideoMix_ListsItsVideo()
        {
            var h = new SimHarness();

            h.Sim.External.CreateCanvas("Vertical", SimCanvasFlags.Program, new SimVideo(30, 1, 1080, 1920, 1080, 1920));

            var video = Listed(h)[1].GetProperty("canvasVideoSettings");
            Assert.Equal(1080, video.GetProperty("baseWidth").GetInt32());
            Assert.Equal(1920, video.GetProperty("baseHeight").GetInt32());
            Assert.Equal(30, video.GetProperty("fpsNumerator").GetInt32());
        }

        [Fact]
        public void ACanvasCanNotBeMadeMain_TheFlagIsTakenOff_AndAnEphemeralOneSaysSo()
        {
            var h = new SimHarness();

            h.Sim.External.CreateCanvas("Impostor", SimCanvasFlags.Main | SimCanvasFlags.Ephemeral);

            var canvases = Listed(h);
            Assert.Equal(["EPHEMERAL"], FlagNames(canvases[1]));
            Assert.Single(canvases, canvas => canvas.GetProperty("canvasFlags").GetProperty("MAIN").GetBoolean());
            Assert.Equal(SimWorld.MainCanvasUuid, canvases[0].GetProperty("canvasUuid").GetString());
        }

        [Fact]
        public void ANameThatIsTaken_IsMadeUnique_WhenACanvasIsMade_AndWhenOneIsRenamed()
        {
            var h = new SimHarness();
            h.Sim.External.CreateCanvas("Overlay");
            var second = h.Sim.External.CreateCanvas("Overlay");
            h.Sim.External.CreateCanvas("Main");
            h.ClearLog();

            // The name of the main canvas is taken too, and "Overlay 2" is: the next free one is "Main 2".
            Assert.Equal(["Main", "Overlay", "Overlay 2", "Main 2"], NamesOf(h));

            // libobs takes the canvas out of its table before it looks for a free name, so a canvas that asks for the name that is taken gets the name that it had: and says so.
            h.Sim.External.RenameCanvas(second, "Overlay");
            h.Settle();

            var renamed = Assert.Single(h.Events("CanvasNameChanged"));
            Assert.Equal("Overlay 2", renamed.String("oldCanvasName"));
            Assert.Equal("Overlay 2", renamed.String("canvasName"));
            Assert.Equal(["Main", "Overlay", "Main 2", "Overlay 2"], NamesOf(h));
        }

        [Fact]
        public void ACanvasThatIsRenamed_IsAnnounced_AndIsLastInTheList()
        {
            var h = new SimHarness();
            var first = h.Sim.External.CreateCanvas("A");
            h.Sim.External.CreateCanvas("B");
            h.Sim.External.CreateCanvas("C");
            h.ClearLog();

            h.Sim.External.RenameCanvas(first, "D");
            h.Settle();

            var renamed = Assert.Single(h.Events("CanvasNameChanged"));
            Assert.Equal(first, renamed.Uuid("canvasUuid"));
            Assert.Equal("A", renamed.String("oldCanvasName"));
            Assert.Equal("D", renamed.String("canvasName"));
            Assert.Equal(SimIntent.Canvases, renamed.Intent);
            Assert.Equal(["Main", "B", "C", "D"], NamesOf(h));
            Assert.Equal(["Main", "B", "C", "D"], NamesOf(h.Sim.Snapshot()));
        }

        [Fact]
        public void TheMainCanvas_CanNeitherBeRenamedNorRemoved_AnEmptyNameAndTheSameNameAreIgnored_AndNothingIsAnnounced()
        {
            var h = new SimHarness();
            var uuid = h.Sim.External.CreateCanvas("Overlay");
            h.ClearLog();
            var main = Guid.Parse(SimWorld.MainCanvasUuid);

            h.Sim.External.RenameCanvas(main, "Renamed");
            h.Sim.External.RemoveCanvas(main);
            h.Sim.External.RenameCanvas(uuid, string.Empty);
            h.Sim.External.RenameCanvas(uuid, "Overlay");
            h.Settle();

            Assert.Empty(h.EventNames());
            Assert.Equal(["Main", "Overlay"], NamesOf(h));
        }

        [Fact]
        public void ACanvasThatIsRemoved_IsAnnounced_AndNotListed_AndAnotherRemovalOfItIsNotAThing()
        {
            var h = new SimHarness();
            var uuid = h.Sim.External.CreateCanvas("Overlay");
            h.ClearLog();

            h.Sim.External.RemoveCanvas(uuid);
            h.Settle();

            var removed = Assert.Single(h.Events("CanvasRemoved"));
            Assert.Equal("Overlay", removed.String("canvasName"));
            Assert.Equal(uuid, removed.Uuid("canvasUuid"));
            Assert.Equal(SimIntent.Canvases, removed.Intent);
            Assert.Equal(["Main"], NamesOf(h));
            var again = Assert.Throws<InvalidOperationException>(() => h.Sim.External.RemoveCanvas(uuid));
            Assert.IsType<SimFailure>(again.InnerException);
        }

        [Fact]
        public void ACanvasThatSomethingHolds_IsStillListedAfterItWasRemoved_UntilTheLingerIsOver_WithoutAnotherEvent()
        {
            var h = new SimHarness(SimQuirks.Ideal with { CanvasRemovedLinger = SimRange.Exactly(500) });
            var uuid = h.Sim.External.CreateCanvas("Overlay");
            h.ClearLog();

            h.Sim.External.RemoveCanvas(uuid);
            h.Run(499);

            // What OBS Studio holds does not have it, and what it lists does, and its name is taken.
            Assert.Equal(["Main"], NamesOf(h.Sim.Snapshot()));
            Assert.Equal(["Main", "Overlay"], NamesOf(h));
            h.Sim.External.CreateCanvas("Overlay");
            Assert.Equal(["Main", "Overlay", "Overlay 2"], NamesOf(h));

            h.Run(2);

            Assert.Equal(["Main", "Overlay 2"], NamesOf(h));
            Assert.Equal(["CanvasRemoved", "CanvasCreated"], h.EventNames());
        }

        [Fact]
        public void TheCanvasesBelongToTheSceneCollection_TheOnesThatAreNotEphemeralComeBack_WithoutAVideo_InTheirOrder()
        {
            var h = new SimHarness();
            var overlay = h.Sim.External.CreateCanvas("Overlay", SimCanvasFlags.Program, new SimVideo(30, 1, 1080, 1920, 1080, 1920));
            h.Sim.External.CreateCanvas("Scratch", SimCanvasFlags.Device);
            var vertical = h.Sim.External.CreateCanvas("Vertical", SimCanvasFlags.Activate);
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Other" });
            h.Settle();

            Assert.Equal(["Main"], NamesOf(h));

            h.Ok("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            h.Settle();

            var canvases = Listed(h);
            Assert.Equal(["Main", "Overlay", "Vertical"], canvases.Select(canvas => canvas.GetProperty("canvasName").GetString()!).ToArray());
            Assert.Equal(overlay, Guid.Parse(canvases[1].GetProperty("canvasUuid").GetString()!));
            Assert.Equal(vertical, Guid.Parse(canvases[2].GetProperty("canvasUuid").GetString()!));
            Assert.Equal(["ACTIVATE", "MIX_AUDIO", "SCENE_REF"], FlagNames(canvases[1]));
            Assert.All(VideoFields, field => Assert.Equal(JsonValueKind.Null, canvases[1].GetProperty("canvasVideoSettings").GetProperty(field).ValueKind));
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void ACanvasThatIsLoadedWithTheCollection_IsAnnouncedOnlyByAServerThatSendsTheEventsOfALoad()
        {
            var quiet = new SimHarness();
            var loud = new SimHarness(SimQuirks.Ideal with { CreationEventsDuringSwitch = true });
            foreach (var h in new[] { quiet, loud })
            {
                h.Sim.External.CreateCanvas("Overlay");
                h.Ok("CreateSceneCollection", new { sceneCollectionName = "Other" });
                h.Settle();
                h.ClearLog();

                h.Ok("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
                h.Settle();
            }

            Assert.DoesNotContain("CanvasCreated", quiet.EventNames());
            var events = loud.EventNames();
            var created = Array.IndexOf(events, "CanvasCreated");
            Assert.True(created > Array.IndexOf(events, "CurrentSceneCollectionChanging") && created < Array.IndexOf(events, "CurrentSceneCollectionChanged"), string.Join(", ", events));
        }

        [Fact]
        public void TheNamesOfTheCanvases_StayUnique_WhateverIsMadeAndRenamed_AndTheInvariantsOfTheWorldSayAllIsWell()
        {
            var h = new SimHarness();
            h.Sim.External.CreateCanvas("A");
            h.Sim.External.CreateCanvas("A");
            h.Sim.External.RenameCanvas(h.Sim.Snapshot().Canvases![1].Uuid, "Main");
            h.Sim.External.RenameCanvas(h.Sim.Snapshot().Canvases![2].Uuid, "Main");
            h.Settle();

            var names = NamesOf(h);

            Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
            h.Sim.CheckInvariants();
        }
    }
}
