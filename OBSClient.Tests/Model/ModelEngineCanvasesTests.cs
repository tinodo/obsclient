namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds about the canvases of OBS Studio: the list, read with the rest and read again when OBS Studio says that a canvas was made or renamed (never taken from the payload of an event),
    /// a canvas that was removed leaving the list at once and staying out of it however long OBS Studio goes on listing it, and what a switch of the scene collection does to them (a collection saves its canvases,
    /// so they are dropped with the collection and read again with the next).
    /// </summary>
    public class ModelEngineCanvasesTests
    {
        private const int Canvases = 1 << 11;
        private const int Config = 2;

        private static readonly Guid MainUuid = new("6c69626f-6273-4c00-9d88-c5136d61696e");

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

        private static string[] Names(EngineHarness h)
        {
            return h.Engine.Snapshot().Canvases!.Canvases.Select(canvas => canvas.Name).ToArray();
        }

        private static void Rename(FakeWorld world, Guid uuid, string name)
        {
            // OBS Studio takes a canvas that is renamed out of its list and puts it in again, which makes it the last.
            var index = world.Canvases.FindIndex(canvas => canvas.Uuid == uuid);
            var held = world.Canvases[index];
            world.Canvases.RemoveAt(index);
            world.Canvases.Add((held.Uuid, name, held.Flags, held.HasVideo));
        }

        [Fact]
        public void TheCanvases_AreReadWithTheFirstReads_WithTheirFlags()
        {
            var overlay = Guid.NewGuid();
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.World.Canvases.Add((overlay, "Overlay", 31, false));
            h.Start();

            var first = h.Take()!;
            Assert.Contains(first.Requests, request => request.Kind == ReadKind.Canvases && request.Type == RequestType.GetCanvasList);
            h.Complete(first);
            h.SettleWithTime();

            var canvases = h.Engine.Snapshot().Canvases!.Canvases;
            Assert.Equal(["Main", "Overlay"], canvases.Select(canvas => canvas.Name));
            Assert.Equal(MainUuid, canvases[0].Uuid);
            Assert.Equal(ObsCanvasFlags.Main | ObsCanvasFlags.Activate | ObsCanvasFlags.MixAudio | ObsCanvasFlags.SceneRef, canvases[0].Flags);
            Assert.Equal(overlay, canvases[1].Uuid);
            Assert.Equal(ObsCanvasFlags.Main | ObsCanvasFlags.Activate | ObsCanvasFlags.MixAudio | ObsCanvasFlags.SceneRef | ObsCanvasFlags.Ephemeral, canvases[1].Flags);
            Assert.True(h.Engine.IsSettled);
        }

        [Theory]
        [InlineData(0, ObsCanvasFlags.None)]
        [InlineData(1, ObsCanvasFlags.Main)]
        [InlineData(2, ObsCanvasFlags.Activate)]
        [InlineData(4, ObsCanvasFlags.MixAudio)]
        [InlineData(8, ObsCanvasFlags.SceneRef)]
        [InlineData(16, ObsCanvasFlags.Ephemeral)]
        public void EachFlagOfACanvas_IsTheOneThatOBSStudioSaysIt(int flags, ObsCanvasFlags expected)
        {
            var h = Started(world => world.Canvases.Add((Guid.NewGuid(), "Probe", flags, false)));

            Assert.Equal(expected, h.Engine.Snapshot().Canvases!.Canvases.Single(canvas => canvas.Name == "Probe").Flags);
        }

        [Fact]
        public void TheModelIsNotSettled_UntilTheCanvasesAreRead()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();

            // Everything but the canvases is answered: the model is not complete without them.
            var first = h.Take()!;
            h.Complete(first, request => request.Kind == ReadKind.Canvases ? (207, null) : h.World.Answer(request));

            Assert.NotEqual(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
            Assert.False(h.Engine.IsSettled);
            Assert.Null(h.Engine.Snapshot().Canvases);

            h.SettleWithTime();

            Assert.Equal(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
            Assert.Equal(["Main"], Names(h));
        }

        [Fact]
        public void ACanvasThatIsMade_IsReadAfterTheEvent_AndNotTakenFromThePayload()
        {
            var h = Started();
            var uuid = Guid.NewGuid();
            h.World.Canvases.Add((uuid, "Overlay", 14, false));

            h.Event("CanvasCreated", new { canvasName = "the payload", canvasUuid = uuid }, Canvases);

            Assert.Equal(["Main"], Names(h));
            Assert.Equal([ReadKind.Canvases], NextRead(h));
            Assert.Empty(NextRead(h));
            Assert.Equal(["Main", "Overlay"], Names(h));
        }

        [Fact]
        public void ACanvasThatIsRenamed_IsReadAfterTheEvent_AndTheOrderIsTheOneOfOBSStudio()
        {
            var first = Guid.NewGuid();
            var h = Started(world =>
            {
                world.Canvases.Add((first, "A", 14, false));
                world.Canvases.Add((Guid.NewGuid(), "B", 14, false));
            });
            Assert.Equal(["Main", "A", "B"], Names(h));

            Rename(h.World, first, "C");
            h.Event("CanvasNameChanged", new { canvasUuid = first, oldCanvasName = "A", canvasName = "the payload" }, Canvases);

            Assert.Equal(["Main", "A", "B"], Names(h));
            Assert.Equal([ReadKind.Canvases], NextRead(h));
            Assert.Equal(["Main", "B", "C"], Names(h));
        }

        [Fact]
        public void ACanvasThatIsRemoved_LeavesTheListAtOnce_AndStaysOutOfItWhileOBSStudioGoesOnListingIt()
        {
            var overlay = Guid.NewGuid();
            var h = Started(world => world.Canvases.Add((overlay, "Overlay", 14, false)));
            Assert.Equal(["Main", "Overlay"], Names(h));

            // OBS Studio keeps a canvas that something holds on to, lists it, and says once that it was removed.
            h.Event("CanvasRemoved", new { canvasName = "Overlay", canvasUuid = overlay }, Canvases);

            Assert.Equal(["Main"], Names(h));
            Assert.Equal([ReadKind.Canvases], NextRead(h));
            Assert.Equal(["Main"], Names(h));
            h.World.Canvases.Add((Guid.NewGuid(), "Other", 14, false));
            h.Event("CanvasCreated", new { canvasName = "Other", canvasUuid = Guid.NewGuid() }, Canvases);
            Assert.Equal([ReadKind.Canvases], NextRead(h));
            Assert.Equal(["Main", "Other"], Names(h));
        }

        [Fact]
        public void ACanvasThatIsRemoved_IsNotBroughtBack_ByAReadThatWasSentBeforeTheRemoval()
        {
            var overlay = Guid.NewGuid();
            var h = Started(world => world.Canvases.Add((overlay, "Overlay", 14, false)));
            h.Event("CanvasNameChanged", new { canvasUuid = overlay, oldCanvasName = "Overlay", canvasName = "Renamed" }, Canvases);
            var ticket = h.Take()!;
            Assert.Equal([ReadKind.Canvases], ticket.Requests.Select(request => request.Kind));

            // The answer says what was true when it was asked: the canvas was there.
            h.Event("CanvasRemoved", new { canvasName = "Renamed", canvasUuid = overlay }, Canvases);
            h.World.Canvases.RemoveAll(canvas => canvas.Uuid == overlay);
            h.Complete(ticket, _ => (100, new { canvases = new[] { MainEntry(), OverlayEntry(overlay, "Renamed") } }));

            Assert.Equal(["Main"], Names(h));
        }

        [Fact]
        public void ACanvasThatWasRemoved_ComesBackWithTheCollectionThatSavedIt()
        {
            var overlay = Guid.NewGuid();
            var h = Started(world => world.Canvases.Add((overlay, "Overlay", 14, false)));
            h.Event("CanvasRemoved", new { canvasName = "Overlay", canvasUuid = overlay }, Canvases);
            Assert.Equal(["Main"], Names(h));

            // A scene collection saves the UUIDs of its canvases: the one that OBS Studio loads next has this canvas (it is the same one that this was, in the collection that it was made in).
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, Config);
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Untitled" }, Config);
            h.SettleWithTime();

            Assert.Equal(["Main", "Overlay"], Names(h));
        }

        [Fact]
        public void TheCanvases_AreKeptWhileTheSceneCollectionChanges_DroppedWhenItHasChanged_AndReadAgain()
        {
            var overlay = Guid.NewGuid();
            var h = Started(world => world.Canvases.Add((overlay, "Overlay", 14, false)));

            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, Config);
            Assert.True(h.Engine.IsSwitching);

            // What the model holds is what it knew of the collection that OBS Studio is leaving; nothing is read, and an event about a canvas is not about the collection that is loading.
            Assert.Equal(["Main", "Overlay"], Names(h));
            h.Event("CanvasCreated", new { canvasName = "Late", canvasUuid = Guid.NewGuid() }, Canvases);
            Assert.Empty(NextRead(h));
            Assert.Equal(["Main", "Overlay"], Names(h));

            h.World.Canvases.RemoveAll(canvas => canvas.Uuid == overlay);
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Other" }, Config);
            Assert.Null(h.Engine.Snapshot().Canvases);
            var asked = new List<ReadKind>();
            h.SettleWithTime(request =>
            {
                asked.Add(request.Kind);
                return h.World.Answer(request);
            });

            Assert.Contains(ReadKind.Canvases, asked);
            Assert.Equal(["Main"], Names(h));
            Assert.Equal(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
        }

        [Fact]
        public void AfterASwitch_TheModelIsNotSynchronized_UntilTheCanvasesOfTheNewCollectionAreRead()
        {
            var h = Started();
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, Config);
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Other" }, Config);

            // Everything but the canvases is read: what the model held of them was of the collection that OBS Studio left, and it is not true of the one that it loaded.
            h.Settle(request => request.Kind == ReadKind.Canvases ? Refuse(207) : h.World.Answer(request));

            Assert.Null(h.Engine.Snapshot().Canvases);
            Assert.NotEqual(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
            Assert.False(h.Engine.IsSettled);

            h.SettleWithTime();

            Assert.Equal(ModelEngineState.Synchronized, h.Engine.Snapshot().State);
            Assert.Equal(["Main"], Names(h));
        }

        [Fact]
        public void AnOBSStudioThatRefusesTheListForGood_HasNoCanvasesToShow_AndTheModelSettles()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();

            // An obs-websocket before 5.7.0 does not know the request: 204 is no moment that passes.
            h.SettleWithTime(request => request.Kind == ReadKind.Canvases ? Refuse(204) : h.World.Answer(request));

            var snapshot = h.Engine.Snapshot();
            Assert.Null(snapshot.Canvases);
            Assert.Equal(ModelEngineState.Synchronized, snapshot.State);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ARefusalThatPasses_IsReadAgain_AndTheListIsKnownWhenOBSStudioAnswers()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();
            var attempts = 0;

            // Not ready (207) is a refusal that passes: it is never believed, however often it comes.
            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Canvases && attempts++ < 5)
                {
                    return Refuse(207);
                }

                return h.World.Answer(request);
            });

            Assert.True(attempts > 5);
            Assert.Equal(["Main"], Names(h));
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
                if (request.Kind == ReadKind.Canvases && attempts++ == 0)
                {
                    return Refuse(204);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(2, attempts);
            Assert.Equal(["Main"], Names(h));
        }

        [Fact]
        public void ANewConnection_ReadsTheCanvasesAgain_AndForgetsWhatWasRemoved()
        {
            var overlay = Guid.NewGuid();
            var h = Started(world => world.Canvases.Add((overlay, "Overlay", 14, false)));
            h.Event("CanvasRemoved", new { canvasName = "Overlay", canvasUuid = overlay }, Canvases);
            h.End();

            h.Start();
            h.SettleWithTime();

            Assert.Equal(["Main", "Overlay"], Names(h));
        }

        [Fact]
        public void ARefreshOfTheCanvases_ReadsThem_AndSaysWhenTheyAreRead()
        {
            var h = Started();
            h.World.Canvases.Add((Guid.NewGuid(), "Overlay", 14, false));

            h.Engine.RequestRefresh(ReadKind.Canvases, Guid.Empty, h.Now);
            Assert.False(h.Engine.IsRefreshed(ReadKind.Canvases, Guid.Empty));
            Assert.Equal([ReadKind.Canvases], NextRead(h));

            Assert.True(h.Engine.IsRefreshed(ReadKind.Canvases, Guid.Empty));
            Assert.Equal(["Main", "Overlay"], Names(h));
        }

        [Fact]
        public void TwoListsAreTheSame_WhenTheCanvasesAndTheirOrderAre()
        {
            var uuid = Guid.NewGuid();
            var list = new CanvasList([new CanvasInfo("A", uuid, ObsCanvasFlags.Activate), new CanvasInfo("B", Guid.Empty, ObsCanvasFlags.None)]);

            Assert.True(list.Equals(new CanvasList([new CanvasInfo("A", uuid, ObsCanvasFlags.Activate), new CanvasInfo("B", Guid.Empty, ObsCanvasFlags.None)])));
            Assert.Equal(list.GetHashCode(), new CanvasList([new CanvasInfo("A", uuid, ObsCanvasFlags.Activate), new CanvasInfo("B", Guid.Empty, ObsCanvasFlags.None)]).GetHashCode());
            Assert.False(list.Equals(new CanvasList([new CanvasInfo("B", Guid.Empty, ObsCanvasFlags.None), new CanvasInfo("A", uuid, ObsCanvasFlags.Activate)])));
            Assert.False(list.Equals(new CanvasList([new CanvasInfo("A", uuid, ObsCanvasFlags.MixAudio), new CanvasInfo("B", Guid.Empty, ObsCanvasFlags.None)])));
            Assert.False(list.Equals(new CanvasList([new CanvasInfo("A2", uuid, ObsCanvasFlags.Activate), new CanvasInfo("B", Guid.Empty, ObsCanvasFlags.None)])));
            Assert.False(list.Equals(null));
        }

        [Fact]
        public void AListWithoutACanvas_HasTheOthersInTheirOrder_AndIsACopyThatNothingCanChange()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var canvases = new List<CanvasInfo> { new("A", first, ObsCanvasFlags.None), new("B", second, ObsCanvasFlags.None) };

            var list = new CanvasList(canvases);
            canvases.Add(new CanvasInfo("C", Guid.NewGuid(), ObsCanvasFlags.None));
            var without = list.Without(first);

            Assert.True(list.Contains(first));
            Assert.False(without.Contains(first));
            Assert.Equal(["A", "B"], list.Canvases.Select(canvas => canvas.Name));
            Assert.Equal(["B"], without.Canvases.Select(canvas => canvas.Name));
            Assert.Throws<NotSupportedException>(() => ((IList<CanvasInfo>)list.Canvases).Add(new CanvasInfo("D", Guid.Empty, ObsCanvasFlags.None)));
        }

        private static object MainEntry()
        {
            return new
            {
                canvasFlags = new { ACTIVATE = true, EPHEMERAL = false, MAIN = true, MIX_AUDIO = true, SCENE_REF = true },
                canvasName = "Main",
                canvasUuid = MainUuid,
                canvasVideoSettings = new { baseHeight = (int?)1080, baseWidth = (int?)1920, fpsDenominator = (int?)1, fpsNumerator = (int?)30, outputHeight = (int?)720, outputWidth = (int?)1280 },
            };
        }

        private static object OverlayEntry(Guid uuid, string name)
        {
            return new
            {
                canvasFlags = new { ACTIVATE = true, EPHEMERAL = false, MAIN = false, MIX_AUDIO = true, SCENE_REF = true },
                canvasName = name,
                canvasUuid = uuid,
                canvasVideoSettings = new { baseHeight = (int?)null, baseWidth = (int?)null, fpsDenominator = (int?)null, fpsNumerator = (int?)null, outputHeight = (int?)null, outputWidth = (int?)null },
            };
        }
    }
}
