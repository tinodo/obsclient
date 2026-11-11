namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The canvases and the calls on them, against a stand-in for OBS Studio that answers as the test says: how a canvas is found, which objects the model keeps when the list changes, the request that
    /// the video settings of a canvas are asked with, what the call says when OBS Studio does not have the canvas any more or does not know canvases at all, and the text of a change.
    /// </summary>
    public class ModelCanvasesTests
    {
        private const int Canvases = 1 << 11;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsModelOptions Options(bool ignoreCase = false)
        {
            return new ObsModelOptions
            {
                ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromMilliseconds(600),
                SynchronizationTimeout = TimeSpan.FromSeconds(5),
                IgnoreCase = ignoreCase,
            };
        }

        private static Task<CallFixture> StartAsync(Action<FakeWorld>? setup = null, bool ignoreCase = false)
        {
            return CallFixture.StartAsync(Options(ignoreCase), setup);
        }

        [Fact]
        public async Task ACanvas_IsFoundByNameAndByUuid_AndAWrongNameSaysWhichAreClose()
        {
            var overlay = Guid.NewGuid();
            await using var test = await StartAsync(world =>
            {
                world.Canvases.Add((overlay, "Overlay", 14, false));
                world.Canvases.Add((Guid.NewGuid(), "Vertical", 14, true));
            });
            var canvases = test.Model.Canvases;

            Assert.Equal(["Main", "Overlay", "Vertical"], canvases.All.Select(canvas => canvas.Name).ToArray());
            Assert.Same(canvases.All[1], canvases["Overlay"]);
            Assert.Same(canvases.All[1], canvases[overlay]);
            Assert.True(canvases.TryGet("Vertical", out var vertical));
            Assert.Same(canvases.All[2], vertical);
            Assert.True(canvases.TryGet(overlay, out var byUuid));
            Assert.Same(canvases.All[1], byUuid);
            Assert.Same(canvases.Main, canvases["Main"]);
            Assert.False(canvases.TryGet("Nope", out _));
            Assert.False(canvases.TryGet(Guid.NewGuid(), out _));

            // Names are case sensitive, as OBS Studio has them.
            Assert.False(canvases.TryGet("overlay", out _));
            var exception = Assert.Throws<ObsNotFoundException>(() => canvases["Overlya"]);
            Assert.Equal("canvas", exception.Kind);
            Assert.Equal("Overlya", exception.RequestedName);
            Assert.Contains("Overlay", exception.Suggestions);
            var byId = Assert.Throws<ObsNotFoundException>(() => canvases[Guid.Empty]);
            Assert.Equal("canvas", byId.Kind);
        }

        [Fact]
        public async Task WithIgnoreCase_ACanvasIsFoundWhateverTheCase_AndTwoThatDifferOnlyByCaseAreAmbiguous()
        {
            await using var test = await StartAsync(
                world =>
                {
                    world.Canvases.Add((Guid.NewGuid(), "Overlay", 14, false));
                    world.Canvases.Add((Guid.NewGuid(), "Cam", 14, false));
                    world.Canvases.Add((Guid.NewGuid(), "cam", 14, false));
                },
                ignoreCase: true);
            var canvases = test.Model.Canvases;

            Assert.Same(canvases.All[1], canvases["OVERLAY"]);
            Assert.True(canvases.TryGet("main", out var main));
            Assert.Same(canvases.Main, main);
            var exception = Assert.Throws<ObsAmbiguousNameException>(() => canvases["CAM"]);
            Assert.Equal("canvas", exception.Kind);
            Assert.Equal(2, exception.Candidates.Count);
            Assert.Throws<ObsAmbiguousNameException>(() => canvases.TryGet("CAM", out _));

            // The name that is written exactly as it is always wins.
            Assert.Same(canvases.All[2], canvases["Cam"]);
            Assert.Same(canvases.All[3], canvases["cam"]);
        }

        [Fact]
        public async Task TheCanvasesThatDidNotChange_KeepTheirSnapshot_WhenAnotherCanvasIsMade()
        {
            await using var test = await StartAsync(world => world.Canvases.Add((Guid.NewGuid(), "Overlay", 14, false)));
            var canvases = test.Model.Canvases;
            var before = canvases.All;
            var main = canvases.Main;
            var overlay = canvases["Overlay"];
            var properties = new List<string>();
            canvases.PropertyChanged += (_, e) =>
            {
                lock (properties)
                {
                    properties.Add(e.PropertyName ?? string.Empty);
                }
            };
            var vertical = Guid.NewGuid();

            test.Rig.Responder.Change(world => world.Canvases.Add((vertical, "Vertical", 14, true)));
            await test.Rig.Responder.SendEventAsync("CanvasCreated", new { canvasName = "Vertical", canvasUuid = vertical }, Canvases);
            await ModelRig.EventuallyAsync(() => canvases.All.Count == 3, "the canvas that was made");

            // The list is current before the notifier has told of it: wait for the PropertyChanged event, and for whatever else would follow it.
            await ModelRig.EventuallyAsync(() => { lock (properties) { return properties.Count > 0; } }, "the change of the list to be told");
            await Task.Delay(100);

            Assert.NotSame(before, canvases.All);
            Assert.Equal(2, before.Count);
            Assert.Same(main, canvases.Main);
            Assert.Same(main, canvases.All[0]);
            Assert.Same(overlay, canvases.All[1]);
            lock (properties)
            {
                Assert.Equal(["All"], properties.ToArray());
            }

            Assert.Equal(vertical, canvases.All[2].Uuid);
        }

        [Fact]
        public async Task ACanvasThatWasRemoved_IsNotInTheListAnyMore_WhateverOBSStudioGoesOnSaying()
        {
            var overlay = Guid.NewGuid();
            await using var test = await StartAsync(world => world.Canvases.Add((overlay, "Overlay", 14, false)));
            var canvases = test.Model.Canvases;
            Assert.True(canvases.TryGet(overlay, out _));

            // The stand-in goes on listing the canvas: that is what OBS Studio does while something holds on to it.
            await test.Rig.Responder.SendEventAsync("CanvasRemoved", new { canvasName = "Overlay", canvasUuid = overlay }, Canvases);

            await ModelRig.EventuallyAsync(() => canvases.All.Count == 1, "the canvas that was removed");
            Assert.False(canvases.TryGet(overlay, out _));
            Assert.Equal(["Main"], canvases.All.Select(canvas => canvas.Name).ToArray());
            await test.Model.WaitUntilSettledAsync(Timeout);
            Assert.Equal(["Main"], canvases.All.Select(canvas => canvas.Name).ToArray());
        }

        [Fact]
        public async Task TheVideoSettingsOfACanvas_AreAskedWithTheListOfCanvases_AndAreTheOnesOfThatCanvas()
        {
            await using var test = await StartAsync(world =>
            {
                world.Canvases.Add((Guid.NewGuid(), "Bare", 14, false));
                world.Canvases.Add((Guid.NewGuid(), "Vertical", 14, true));
            });
            var before = test.Count("GetCanvasList");

            var main = await test.Model.Canvases.Main!.GetVideoSettingsAsync().WithTimeoutAsync(Timeout);
            var bare = await test.Model.Canvases["Bare"].GetVideoSettingsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(before + 2, test.Count("GetCanvasList"));
            Assert.NotNull(main);
            Assert.Equal((1920, 1080, 1280, 720, 30, 1), (main.BaseWidth, main.BaseHeight, main.OutputWidth, main.OutputHeight, main.FpsNumerator, main.FpsDenominator));
            Assert.Equal(30.0, main.FramesPerSecond);

            // A canvas that has no video mix says nothing of it: all six numbers are null.
            Assert.Null(bare);
        }

        [Fact]
        public async Task TheFrameRateOfACanvas_IsAFractionOfWholeNumbers_AsTheyComeFromOBSStudio()
        {
            await using var test = await StartAsync();
            test.Answer("GetCanvasList", _ => (100, new
            {
                canvases = new[]
                {
                    new
                    {
                        canvasFlags = new { ACTIVATE = true, EPHEMERAL = false, MAIN = true, MIX_AUDIO = true, SCENE_REF = true },
                        canvasName = "Main",
                        canvasUuid = new Guid("6c69626f-6273-4c00-9d88-c5136d61696e"),
                        canvasVideoSettings = new { baseHeight = (int?)1080, baseWidth = (int?)1920, fpsDenominator = (double?)1001.0, fpsNumerator = (double?)60000.0, outputHeight = (int?)1080, outputWidth = (int?)1920 },
                    },
                },
            }));

            var video = await test.Model.Canvases.Main!.GetVideoSettingsAsync().WithTimeoutAsync(Timeout);

            Assert.NotNull(video);
            Assert.Equal((60000, 1001), (video.FpsNumerator, video.FpsDenominator));
            Assert.Equal(59.94, video.FramesPerSecond, 2);
        }

        [Fact]
        public async Task TheVideoSettingsOfACanvasThatOBSStudioDoesNotListAnyMore_AreRefused_WithTheCanvasThatIsGone()
        {
            var overlay = Guid.NewGuid();
            await using var test = await StartAsync(world => world.Canvases.Add((overlay, "Overlay", 14, true)));
            var held = test.Model.Canvases["Overlay"];
            test.Rig.Responder.Change(world => world.Canvases.RemoveAll(canvas => canvas.Uuid == overlay));

            var gone = await Assert.ThrowsAsync<ObsRemovedException>(() => held.GetVideoSettingsAsync().WithTimeoutAsync(Timeout));

            Assert.Contains("canvas 'Overlay'", gone.Message);
        }

        [Fact]
        public async Task TheVideoSettingsOfACanvas_NeedAConnection_AndEndWhenTheWaitIsCancelled()
        {
            await using var test = await StartAsync();
            var main = test.Model.Canvases.Main!;
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => main.GetVideoSettingsAsync(cancelled.Token));
            await test.Model.DisconnectAsync();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => main.GetVideoSettingsAsync());
        }

        [Fact]
        public async Task AnOBSStudioThatDoesNotKnowCanvases_HasNoCanvases_AndTheModelIsSynchronizedAllTheSame()
        {
            await using var rig = ModelRig.Create(options: Options());
            rig.Responder.WebSocketVersion = "5.6.2";

            // obs-websocket before 5.7.0 answers 204 to a request that it does not know.
            rig.Responder.OnRequest = (type, _) => type == "GetCanvasList" ? (204, null) : null;
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.Model.WaitUntilSettledAsync(Timeout);

            Assert.Equal(ObsModelState.Synchronized, rig.Model.State);
            Assert.Empty(rig.Model.Canvases.All);
            Assert.Null(rig.Model.Canvases.Main);
            Assert.False(rig.Model.Canvases.TryGet("Main", out _));
            Assert.Contains("GetCanvasList", rig.Responder.RequestLog);

            // A refusal is not believed the first time (see ModelEngineCanvasesTests): the list was asked for more than once.
            Assert.True(rig.Responder.RequestLog.Count(type => type == "GetCanvasList") >= 2);
        }

        [Fact]
        public async Task TheTextOfAChange_NamesTheProfilesTheSceneCollectionsAndTheCanvases_AndShowsTheirListsByName()
        {
            await using var rig = await ModelRig.ConnectedAsync();
            var overlay = Guid.NewGuid();
            rig.Responder.Change(world => world.Canvases.Add((overlay, "Overlay", 14, false)));
            await rig.Responder.SendEventAsync("CanvasCreated", new { canvasName = "Overlay", canvasUuid = overlay }, Canvases);
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => ReferenceEquals(change.Entity, rig.Model.Canvases)), "the record about the canvases");

            var listed = rig.Changes.Single(change => ReferenceEquals(change.Entity, rig.Model.Canvases));
            var profiles = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, rig.Model.Profiles, nameof(ObsProfiles.Current), "Untitled", "Gaming");
            var profileNames = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, rig.Model.Profiles, nameof(ObsProfiles.Names), new[] { "Untitled" }, new[] { "Gaming", "Untitled" });
            var collections = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, rig.Model.SceneCollections, nameof(ObsSceneCollections.Current), null, "Stream");

            Assert.Equal(nameof(ObsCanvases.All), listed.PropertyName);
            Assert.Equal("the canvases: All [Main] -> [Main, Overlay]", listed.ToString());
            Assert.Equal("the profiles: Current Untitled -> Gaming", profiles.ToString());
            Assert.Equal("the profiles: Names [Untitled] -> [Gaming, Untitled]", profileNames.ToString());
            Assert.Equal("the scene collections: Current (nothing) -> Stream", collections.ToString());
        }
    }
}
