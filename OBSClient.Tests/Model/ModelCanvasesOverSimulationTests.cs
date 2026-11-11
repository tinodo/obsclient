namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The canvases, over the whole stack: an <see cref="ObsModel"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio (<see cref="SimObsServer"/>), with the lags and the order of
    /// what OBS Studio really does. What the model shows is what the simulation holds (<see cref="ModelMirror"/>), after what a plugin did (made, renamed and removed a canvas), after what the scene collection
    /// loads, and a canvas that OBS Studio removes and goes on listing is not in the model.
    /// </summary>
    public class ModelCanvasesOverSimulationTests
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

        private static string[] Names(ObsModel model)
        {
            return model.Canvases.All.Select(canvas => canvas.Name).ToArray();
        }

        [Fact]
        public async Task TheModel_ShowsTheMainCanvas_AsOBSStudioHoldsIt()
        {
            await using var rig = await StartAsync();

            var main = Assert.Single(rig.Model.Canvases.All);
            Assert.Same(main, rig.Model.Canvases.Main);
            Assert.Equal("Main", main.Name);
            Assert.Equal(new Guid("6c69626f-6273-4c00-9d88-c5136d61696e"), main.Uuid);
            Assert.True(main.IsMain);
            Assert.Equal(ObsCanvasFlags.Main | ObsCanvasFlags.Activate | ObsCanvasFlags.MixAudio | ObsCanvasFlags.SceneRef, main.Flags);
            Assert.Equal("Canvas 'Main' (main)", main.ToString());
            Assert.Null(rig.Compare());
        }

        [Fact]
        public void BeforeTheModelIsConnected_ThereAreNoCanvases_AndTheObjectIsTheSameForever()
        {
            using var model = new ObsModel();

            Assert.Empty(model.Canvases.All);
            Assert.Null(model.Canvases.Main);
            Assert.False(model.Canvases.TryGet("Main", out _));
            Assert.False(model.Canvases.TryGet(Guid.NewGuid(), out _));
            Assert.Same(model.Canvases, model.Canvases);
        }

        [Fact]
        public async Task TheFirstSynchronization_TellsTheApplicationThatTheCanvasesAreKnown()
        {
            await using var rig = new ModelSimRig();
            var raised = Watch(rig.Model.Canvases);

            await rig.ConnectAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("the first read", Patience);

            // The object is the same before and after, and the application can hold it from the start (bind to it): it is told what it was not.
            await ModelRig.EventuallyAsync(() => Told(raised).Contains("Main"), "the notifications of the first synchronization");
            Assert.Equal(["All", "Main"], Told(raised));
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheCanvases_FollowWhatAPluginDoes_AndTheApplicationIsToldWhatChanged(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var canvases = rig.Model.Canvases;
            var main = canvases.Main;
            var raised = Watch(canvases);

            var overlay = rig.World(world => world.CreateCanvas("Overlay", SimCanvasFlags.Program, new SimVideo(30, 1, 1080, 1920, 1080, 1920)));
            await rig.ConvergedAsync("a canvas that a plugin made", Patience);

            Assert.Equal(["Main", "Overlay"], Names(rig.Model));
            var made = canvases["Overlay"];
            Assert.Same(made, canvases[overlay]);
            Assert.Equal(overlay, made.Uuid);
            Assert.False(made.IsMain);
            Assert.Equal(ObsCanvasFlags.Activate | ObsCanvasFlags.MixAudio | ObsCanvasFlags.SceneRef, made.Flags);

            // The main canvas did not change: it is the same snapshot, and the application is told about the list only.
            Assert.Same(main, canvases.Main);
            await ModelRig.EventuallyAsync(() => Told(raised).Contains("All"), "the notification of the list");
            Assert.Equal(["All"], Told(raised));
            Assert.Contains(
                rig.Changes,
                change => ReferenceEquals(change.Entity, canvases) && change.PropertyName == nameof(ObsCanvases.All) && change.NewValue is IReadOnlyList<ObsCanvas> list && list.Select(canvas => canvas.Name).SequenceEqual(["Main", "Overlay"]));

            rig.World(world => world.RenameCanvas(overlay, "Vertical"));
            await rig.ConvergedAsync("a canvas that a plugin renamed", Patience);

            // A canvas that was renamed is another snapshot with the same UUID, and it is the last in the list.
            Assert.Equal(["Main", "Vertical"], Names(rig.Model));
            Assert.NotSame(made, canvases["Vertical"]);
            Assert.Equal(overlay, canvases["Vertical"].Uuid);
            Assert.Same(main, canvases.Main);
            Assert.False(canvases.TryGet("Overlay", out _));

            // The snapshot from before the rename is of the same canvas still (the collection is the same one).
            Assert.NotNull(await made.GetVideoSettingsAsync().WithTimeoutAsync(Patience));

            rig.World(world => world.RemoveCanvas(overlay));
            await rig.ConvergedAsync("a canvas that a plugin removed", Patience);

            Assert.Equal(["Main"], Names(rig.Model));
            Assert.False(canvases.TryGet(overlay, out _));
            Assert.Throws<ObsNotFoundException>(() => canvases[overlay]);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task TheOrderOfTheList_IsTheOneOfOBSStudio_AndAMoveToTheEndIsAChange()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            var first = rig.World(world => world.CreateCanvas("A"));
            rig.World(world => world.CreateCanvas("B"));
            rig.World(world => world.CreateCanvas("C"));
            await rig.ConvergedAsync("three canvases", Patience);
            var before = rig.Model.Canvases.All;

            rig.World(world => world.RenameCanvas(first, "D"));
            await rig.ConvergedAsync("the first canvas, renamed", Patience);

            Assert.Equal(["Main", "A", "B", "C"], before.Select(canvas => canvas.Name).ToArray());
            Assert.Equal(["Main", "B", "C", "D"], Names(rig.Model));
            Assert.Same(before[2], rig.Model.Canvases["B"]);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task ANameThatWasTaken_IsTheNameThatOBSStudioMadeUnique()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            rig.World(world => world.CreateCanvas("Overlay"));
            rig.World(world => world.CreateCanvas("Overlay"));
            await rig.ConvergedAsync("two canvases with the same name", Patience);

            Assert.Equal(["Main", "Overlay", "Overlay 2"], Names(rig.Model));
            Assert.Same(rig.Model.Canvases.All[2], rig.Model.Canvases["Overlay 2"]);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task ACanvasThatOBSStudioRemovedAndGoesOnListing_IsNotInTheModel_AndNeverComesBack()
        {
            // OBS Studio lists the canvas for as long as the test runs (it is let go of long after this test is over).
            await using var rig = await StartAsync(Quirks("Ideal") with { CanvasRemovedLinger = SimRange.Exactly(60_000) });
            var overlay = rig.World(world => world.CreateCanvas("Overlay"));
            await rig.ConvergedAsync("a canvas", Patience);

            rig.World(world => world.RemoveCanvas(overlay));
            await rig.ConvergedAsync("the canvas that a plugin removed", Patience);
            Assert.Equal(["Main"], Names(rig.Model));

            // A read that OBS Studio answers while it lists the canvas still (the next canvas, and the rename of it, make the model read the list): the canvas is not brought back.
            var next = rig.World(world => world.CreateCanvas("Next"));
            await rig.ConvergedAsync("a canvas that was made while the removed one is still listed", Patience);
            Assert.Equal(["Main", "Next"], Names(rig.Model));

            rig.World(world => world.RenameCanvas(next, "Last"));
            await rig.ConvergedAsync("a canvas, renamed while the removed one is still listed", Patience);
            Assert.Equal(["Main", "Last"], Names(rig.Model));
            Assert.Null(rig.Compare());
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheCanvasesBelongToTheSceneCollection_TheOnesThatAreSavedComeBack_AsNewSnapshots(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var overlay = rig.World(world => world.CreateCanvas("Overlay", SimCanvasFlags.Program, new SimVideo(30, 1, 1080, 1920, 1080, 1920)));
            rig.World(world => world.CreateCanvas("Scratch", SimCanvasFlags.Device));
            await rig.ConvergedAsync("two canvases", Patience);
            var held = rig.Model.Canvases["Overlay"];
            var mainBefore = rig.Model.Canvases.Main!;
            Assert.NotNull(await held.GetVideoSettingsAsync().WithTimeoutAsync(Patience));

            await rig.Model.SceneCollections.CreateAsync("Podcast").WithTimeoutAsync(Patience);

            // The new collection has the main canvas only, and the canvas that the application holds is of the collection that OBS Studio left.
            Assert.Equal(ObsModelState.Synchronized, rig.Model.State);
            Assert.Equal(["Main"], Names(rig.Model));
            Assert.Null(rig.Compare());

            // The main canvas has the same UUID in every collection, and the snapshot of the first collection is not the canvas of the second.
            Assert.NotSame(mainBefore, rig.Model.Canvases.Main);
            await Assert.ThrowsAsync<ObsRemovedException>(() => mainBefore.GetVideoSettingsAsync().WithTimeoutAsync(Patience));

            await rig.Model.SceneCollections.SetCurrentAsync("Untitled").WithTimeoutAsync(Patience);

            // The canvas that was saved is back, with the UUID that it had, and without the canvas that is not saved. It has no video any more: a canvas that is loaded has no mix.
            Assert.Equal(["Main", "Overlay"], Names(rig.Model));
            var loaded = rig.Model.Canvases["Overlay"];
            Assert.Equal(overlay, loaded.Uuid);
            Assert.NotSame(held, loaded);
            Assert.Null(await loaded.GetVideoSettingsAsync().WithTimeoutAsync(Patience));
            Assert.Null(rig.Compare());

            // The snapshot from before is of the canvas that was left, and not of the one with the same UUID that is loaded now.
            var gone = await Assert.ThrowsAsync<ObsRemovedException>(() => held.GetVideoSettingsAsync().WithTimeoutAsync(Patience));
            Assert.Contains("Overlay", gone.Message);
        }

        [Fact]
        public async Task TheVideoOfTheMainCanvas_IsTheVideoOfTheProfile_AlsoAfterTheApplicationChangedIt()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            var main = rig.Model.Canvases.Main!;

            var before = await main.GetVideoSettingsAsync().WithTimeoutAsync(Patience);
            var profile = await rig.Model.GetVideoSettingsAsync().WithTimeoutAsync(Patience);

            Assert.NotNull(before);
            Assert.Equal((profile.BaseWidth, profile.BaseHeight, profile.OutputWidth, profile.OutputHeight, profile.FpsNumerator, profile.FpsDenominator), (before.BaseWidth, before.BaseHeight, before.OutputWidth, before.OutputHeight, before.FpsNumerator, before.FpsDenominator));

            await rig.Model.SetVideoSettingsAsync(baseResolution: (1280, 720), outputResolution: (640, 360), frameRate: (25, 1)).WithTimeoutAsync(Patience);

            var after = await main.GetVideoSettingsAsync().WithTimeoutAsync(Patience);
            Assert.NotNull(after);
            Assert.Equal((1280, 720, 640, 360, 25, 1), (after.BaseWidth, after.BaseHeight, after.OutputWidth, after.OutputHeight, after.FpsNumerator, after.FpsDenominator));
        }

        [Fact]
        public async Task TheVideoOfACanvas_IsWhatOBSStudioSays_AndNullWhenTheCanvasHasNone()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            rig.World(world => world.CreateCanvas("Vertical", SimCanvasFlags.Program, new SimVideo(30, 1, 1080, 1920, 1080, 1920)));
            rig.World(world => world.CreateCanvas("Bare", SimCanvasFlags.Program));
            await rig.ConvergedAsync("two canvases", Patience);

            var vertical = await rig.Model.Canvases["Vertical"].GetVideoSettingsAsync().WithTimeoutAsync(Patience);
            var bare = await rig.Model.Canvases["Bare"].GetVideoSettingsAsync().WithTimeoutAsync(Patience);

            Assert.NotNull(vertical);
            Assert.Equal((1080, 1920, 1080, 1920, 30, 1), (vertical.BaseWidth, vertical.BaseHeight, vertical.OutputWidth, vertical.OutputHeight, vertical.FpsNumerator, vertical.FpsDenominator));
            Assert.Null(bare);
        }

        [Fact]
        public async Task TheVideoOfACanvasThatWasRemoved_IsGone_AlsoWhileOBSStudioGoesOnListingIt()
        {
            await using var rig = await StartAsync(Quirks("Ideal") with { CanvasRemovedLinger = SimRange.Exactly(60_000) });
            var overlay = rig.World(world => world.CreateCanvas("Overlay", SimCanvasFlags.Program, new SimVideo(30, 1, 1080, 1920, 1080, 1920)));
            await rig.ConvergedAsync("a canvas", Patience);
            var held = rig.Model.Canvases["Overlay"];

            rig.World(world => world.RemoveCanvas(overlay));
            await rig.ConvergedAsync("the canvas that a plugin removed", Patience);

            // OBS Studio still lists the canvas and says what its video is: the application is told that it is not there.
            var gone = await Assert.ThrowsAsync<ObsRemovedException>(() => held.GetVideoSettingsAsync().WithTimeoutAsync(Patience));
            Assert.Contains("Overlay", gone.Message);
        }

        [Fact]
        public async Task TheVideoOfACanvas_NeedsAConnection_AndAnEndedWait()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            var main = rig.Model.Canvases.Main!;
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => main.GetVideoSettingsAsync(cancelled.Token));
            await rig.Model.DisconnectAsync();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => main.GetVideoSettingsAsync());
        }
    }
}
