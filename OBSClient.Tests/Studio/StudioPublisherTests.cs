namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// What the application sees of the model: the objects that are made for the scenes, inputs and scene items, the collections, the lookup by name,
    /// and the notices. The engine is fed by hand (see <see cref="EngineHarness"/>) and the publisher is run after every step, as the host does.
    /// </summary>
    public class StudioPublisherTests
    {
        private static PublisherHarness Started(Action<FakeWorld>? setup = null, bool ignoreCase = false, StudioEngineOptions? engineOptions = null)
        {
            var harness = new PublisherHarness(ignoreCase, engineOptions);
            harness.World.AddScene("Scene");
            setup?.Invoke(harness.World);
            harness.Engine.Start();
            harness.Engine.SettleWithTime();
            harness.Publish();
            return harness;
        }

        [Fact]
        public void TheFirstRead_IsPublished_ByNameAndByPosition()
        {
            Guid gaming = Guid.Empty;
            using var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                world.Program = gaming;
                var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });

            var view = h.View;
            Assert.Equal(new[] { "Gaming", "Scene" }, view.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(new[] { 0, 1 }, view.Scenes.Select(scene => scene.Index).ToArray());
            Assert.Same(view.Scenes[0], view.Scenes["Gaming"]);
            Assert.Same(view.Scenes["Gaming"], view.Scenes[gaming]);
            Assert.Same(view.Scenes["Gaming"], view.ProgramScene);

            var item = view.Scenes["Gaming"].Items["Mic"];
            Assert.Same(view.Inputs["Mic"], item.Source);
            Assert.Same(view.Scenes["Gaming"], item.Scene);
            Assert.Same(item, view.Scenes["Gaming"].Items[0]);
            Assert.Same(item, view.Scenes["Gaming"].Items.ById(1));
            Assert.Equal((1, 0, true, false, false), (item.Id, item.Index, item.IsEnabled, item.IsLocked, item.IsRemoved));
            var input = view.Inputs["Mic"];
            Assert.Equal(("wasapi_input_capture", true, false, ObsObjectState.Live), (input.Kind, input.HasAudio, input.IsMuted, input.State));
            Assert.Empty(view.Scenes["Scene"].Items);
        }

        [Fact]
        public void TheInputs_AreOrderedByName_WhateverTheCase()
        {
            using var h = Started(world =>
            {
                world.AddInput("zebra");
                world.AddInput("Alpha");
                world.AddInput("beta");
            });

            Assert.Equal(new[] { "Alpha", "beta", "zebra" }, h.View.Inputs.Select(input => input.Name).ToArray());
        }

        [Fact]
        public void TwoSceneItemsOfOneSource_MakeTheNameAmbiguous_AndTheExceptionNamesThem()
        {
            using var h = Started(world =>
            {
                var gaming = world.AddScene("Gaming");
                var mic = world.AddInput("Mic");
                world.AddItem(gaming, mic, "Mic");
                world.AddItem(gaming, mic, "Mic");
            });

            var items = h.View.Scenes["Gaming"].Items;
            var exception = Assert.Throws<ObsAmbiguousNameException>(() => items["Mic"]);
            Assert.Contains("id 1", exception.Message);
            Assert.Contains("id 2", exception.Message);
            Assert.Contains("scene 'Gaming'", exception.Message);
            Assert.Equal(2, items.FindAll("Mic").Count);
            Assert.Throws<ObsAmbiguousNameException>(() => items.TryGet("Mic", out _));
            Assert.Equal(2, items.ById(2).Id);
        }

        [Fact]
        public void ANameThatIsNotThere_GivesTheCloseOnes()
        {
            using var h = Started(world =>
            {
                var gaming = world.AddScene("Gaming");
                world.AddItem(gaming, world.AddInput("Microphone"), "Microphone");
            });

            var scene = Assert.Throws<ObsNotFoundException>(() => h.View.Scenes["Gamng"]);
            Assert.Equal(new[] { "Gaming" }, scene.Suggestions);
            Assert.Contains("Did you mean \"Gaming\"?", scene.Message);

            var item = Assert.Throws<ObsNotFoundException>(() => h.View.Scenes["Gaming"].Items["Microphon"]);
            Assert.Contains("scene 'Gaming'", item.Message);
            Assert.Equal(new[] { "Microphone" }, item.Suggestions);

            Assert.False(h.View.Scenes.TryGet("Gamng", out var missing));
            Assert.Null(missing);
            Assert.Null(h.View.Scenes.Find("Gamng"));
            Assert.False(h.View.Scenes.Contains("Gamng"));
            Assert.True(h.View.Scenes.Contains("Gaming"));
            Assert.Throws<ObsNotFoundException>(() => h.View.Scenes[Guid.NewGuid()]);
            Assert.Throws<ObsNotFoundException>(() => h.View.Scenes["Gaming"].Items.ById(99));
        }

        [Fact]
        public void Names_AreCaseSensitive_UnlessTheOptionSaysOtherwise()
        {
            using (var strict = Started(world => world.AddScene("Gaming")))
            {
                Assert.Throws<ObsNotFoundException>(() => strict.View.Scenes["gaming"]);
            }

            using var relaxed = Started(world => world.AddScene("Gaming"), ignoreCase: true);
            Assert.Same(relaxed.View.Scenes["Gaming"], relaxed.View.Scenes["gAMING"]);
        }

        [Fact]
        public void WithIgnoreCase_AnExactMatchWins_AndSeveralThatDifferOnlyByCaseAreAmbiguous()
        {
            using var h = Started(
                world =>
                {
                    world.AddScene("gaming");
                    world.AddScene("Gaming");
                },
                ignoreCase: true);

            Assert.Equal("Gaming", h.View.Scenes["Gaming"].Name);
            Assert.Equal("gaming", h.View.Scenes["gaming"].Name);
            Assert.Throws<ObsAmbiguousNameException>(() => h.View.Scenes["GAMING"]);
            Assert.Equal(2, h.View.Scenes.FindAll("GAMING").Count);
        }

        [Fact]
        public void ARenamedObject_IsTheSameObject_AndItsNameChangesOnce()
        {
            Guid gaming = Guid.Empty;
            using var h = Started(world => gaming = world.AddScene("Gaming"));
            var before = h.View.Scenes["Gaming"];

            h.Engine.Event("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "Playing" }, 4);
            h.Publish();

            Assert.Same(before, h.View.Scenes["Playing"]);
            Assert.Equal("Playing", before.Name);
            Assert.Throws<ObsNotFoundException>(() => h.View.Scenes["Gaming"]);
            var change = Assert.Single(h.Notices, notice => notice.Args is not null);
            Assert.Equal((ObsChangeKind.PropertyChanged, "Name", "Gaming", "Playing"), (change.Args!.Kind, change.Args.PropertyName, change.Args.OldValue, change.Args.NewValue));
            Assert.Same(before, change.Args.Entity);
            Assert.Contains(h.Notices, notice => ReferenceEquals(notice.Entity, before) && notice.Property == "Name");
        }

        [Fact]
        public void ACollectionThatWasTaken_DoesNotChange_WhenTheModelDoes()
        {
            using var h = Started(world => world.AddScene("Gaming"));
            var taken = h.View.Scenes;

            var created = h.World.AddScene("Chatting");
            h.Engine.Event("SceneCreated", new { sceneName = "Chatting", sceneUuid = created, isGroup = false }, 4);
            h.Engine.SettleWithTime();
            h.Publish();

            Assert.Equal(2, taken.Count);
            Assert.Equal(3, h.View.Scenes.Count);
            Assert.Equal(new[] { "Chatting", "Gaming", "Scene" }, h.View.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, h.View.Scenes.Select(scene => scene.Index).ToArray());
        }

        [Fact]
        public void ASceneThatIsRemoved_IsGone_AndTheReferenceStaysValid()
        {
            Guid gaming = Guid.Empty;
            using var h = Started(world => gaming = world.AddScene("Gaming"));
            var scene = h.View.Scenes["Gaming"];

            h.World.Scenes.RemoveAll(s => s.Uuid == gaming);
            h.World.Items.Remove(gaming);
            h.Engine.Event("SceneRemoved", new { sceneName = "Gaming", sceneUuid = gaming, isGroup = false }, 4);
            h.Engine.SettleWithTime();
            h.Publish();

            Assert.DoesNotContain(h.View.Scenes, s => ReferenceEquals(s, scene));
            Assert.Equal((ObsObjectState.Gone, true, -1), (scene.State, scene.IsRemoved, scene.Index));
            Assert.Equal("Gaming", scene.Name);
            Assert.Contains(h.Notices, notice => notice.Args is { Kind: ObsChangeKind.Removed } args && ReferenceEquals(args.Entity, scene));
            Assert.Equal(new[] { 0 }, h.View.Scenes.Select(s => s.Index).ToArray());
        }

        [Fact]
        public void AnInputThatIsRemovedWhileItIsInUse_IsDetached_AndItsItemsStay_UntilTheLastOneGoes()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            using var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });
            var input = h.View.Inputs["Mic"];
            var item = h.View.Scenes["Gaming"].Items["Mic"];

            // OBS Studio only says that the input was removed: the scene item keeps it alive, and so does the list of inputs.
            h.Engine.Event("InputRemoved", new { inputName = "Mic", inputUuid = mic }, 8);
            h.Engine.SettleWithTime();
            h.Publish();

            Assert.Equal(ObsObjectState.Detached, input.State);
            Assert.True(input.IsRemoved);
            Assert.DoesNotContain(h.View.Inputs, i => ReferenceEquals(i, input));
            Assert.Same(item, h.View.Scenes["Gaming"].Items["Mic"]);
            Assert.Same(input, item.Source);
            Assert.False(item.IsRemoved);
            Assert.Contains(h.Notices, notice => notice.Args is { Kind: ObsChangeKind.StateChanged } args && ReferenceEquals(args.Entity, input) && Equals(args.NewValue, ObsObjectState.Detached));

            // The last scene item goes: OBS Studio destroys the input, and says nothing more about it.
            h.World.Items[gaming].Clear();
            h.World.Inputs.RemoveAll(i => i.Uuid == mic);
            h.Engine.Event("SceneItemRemoved", new { sceneName = "Gaming", sceneUuid = gaming, sourceName = "Mic", sourceUuid = mic, sceneItemId = 1 }, 128);
            h.Engine.SettleWithTime();
            h.Publish();

            Assert.Equal(ObsObjectState.Gone, input.State);
            Assert.True(item.IsRemoved);
            Assert.Empty(h.View.Scenes["Gaming"].Items);
            var removed = h.Notices.Where(notice => notice.Args is { Kind: ObsChangeKind.Removed }).Select(notice => notice.Args!.Entity).ToArray();
            Assert.Equal(new object[] { item, input }, removed);
        }

        [Fact]
        public void ASceneItem_AppearsWithItsSource_AndNeverBefore()
        {
            // Small reads, so that the scene items of a scene are read before the audio state of the input that they show: the input is
            // not complete yet then, and a scene item that shows it must wait.
            using var h = Started(
                world =>
                {
                    var gaming = world.AddScene("Gaming");
                    world.AddItem(gaming, world.AddInput("Mic", "wasapi_input_capture", 2), "Mic");
                },
                engineOptions: new StudioEngineOptions { MaxBatch = 3 });
            var created = h.World.AddScene("Chatting");
            var camera = h.World.AddInput("Camera", "wasapi_input_capture", 2);
            h.World.AddItem(created, camera, "Camera");

            h.Engine.Event("SceneCreated", new { sceneName = "Chatting", sceneUuid = created, isGroup = false }, 4);
            h.Engine.Event("InputCreated", new { inputName = "Camera", inputUuid = camera, inputKind = "wasapi_input_capture", unversionedInputKind = "wasapi_input_capture", inputKindCaps = 2u, inputSettings = new { }, defaultInputSettings = new { } }, 8);
            h.Engine.Event("SceneItemCreated", new { sceneName = "Chatting", sceneUuid = created, sourceName = "Camera", sourceUuid = camera, sceneItemId = 1, sceneItemIndex = 0 }, 128);

            var steps = 0;
            var itemSeen = false;
            while (h.Engine.Take() is { } ticket)
            {
                h.Engine.Complete(ticket);
                h.Publish();
                steps++;
                foreach (var scene in h.View.Scenes)
                {
                    foreach (var item in scene.Items)
                    {
                        // Whatever is visible has a source that is visible too.
                        Assert.Contains(h.View.AllScenes.Cast<ObsSource>().Concat(h.View.Inputs), source => ReferenceEquals(source, item.Source));
                        itemSeen |= item.Source.Name == "Camera";
                    }
                }
            }

            Assert.True(steps > 1, "The reads were not split.");
            Assert.True(itemSeen);
            Assert.Equal("Camera", h.View.Scenes["Chatting"].Items[0].Source.Name);
        }

        [Fact]
        public void AChangeOfAValue_IsReportedOnce_WithTheOldAndTheNewValue()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            using var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });
            var input = h.View.Inputs["Mic"];
            var item = h.View.Scenes["Gaming"].Items["Mic"];

            h.Engine.Event("InputMuteStateChanged", new { inputName = "Mic", inputUuid = mic, inputMuted = true }, 8);
            h.Engine.Event("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 1, sceneItemEnabled = false }, 128);
            h.Publish();

            Assert.True(input.IsMuted);
            Assert.False(item.IsEnabled);
            var changes = h.Notices.Where(notice => notice.Args is not null).Select(notice => notice.Args!).ToArray();
            Assert.Collection(
                changes,
                change => Assert.Equal((ObsChangeKind.PropertyChanged, "IsMuted", false, true), (change.Kind, change.PropertyName, change.OldValue, change.NewValue)),
                change => Assert.Equal((ObsChangeKind.PropertyChanged, "IsEnabled", true, false), (change.Kind, change.PropertyName, change.OldValue, change.NewValue)));
            Assert.Same(input, changes[0].Entity);
            Assert.Same(item, changes[1].Entity);

            h.Engine.Event("InputVolumeChanged", new { inputName = "Mic", inputUuid = mic, inputVolumeMul = 0.1, inputVolumeDb = -20.0 }, 8);
            h.Publish();

            // A single-precision number, as OBS Studio and the ObsClient have it: a volume that was set to 0.1 reads 0.1 (and not 0.10000000149011612),
            // and the values of the change are floats as well, so that a handler can cast them to the type of the property.
            Assert.Equal(0.1f, input.VolumeMul);
            Assert.Equal("0.1", input.VolumeMul.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(-20f, input.VolumeDb);
            var volumeChanges = h.Notices.Where(notice => notice.Args is not null).Select(notice => notice.Args!).ToArray();
            Assert.Equal(new[] { "VolumeMul", "VolumeDb" }, volumeChanges.Select(change => change.PropertyName!).ToArray());
            Assert.All(volumeChanges, change => Assert.Equal((typeof(float), typeof(float)), (change.OldValue!.GetType(), change.NewValue!.GetType())));
            Assert.Equal(0.1f, (float)volumeChanges[0].NewValue!);
        }

        [Fact]
        public void TheProgramSceneAndStudioMode_ArePublished_AndTheirChangesAreReported()
        {
            Guid gaming = Guid.Empty;
            Guid chatting = Guid.Empty;
            using var h = Started(world =>
            {
                chatting = world.AddScene("Chatting");
                gaming = world.AddScene("Gaming");
                world.Program = gaming;
            });
            Assert.Equal("Gaming", h.View.ProgramScene!.Name);
            Assert.False(h.View.StudioMode);
            var previous = h.View.ProgramScene;

            h.World.Program = chatting;
            h.Engine.Event("CurrentProgramSceneChanged", new { sceneName = "Chatting", sceneUuid = chatting }, 4);
            h.World.StudioMode = true;
            h.Engine.Event("StudioModeStateChanged", new { studioModeEnabled = true }, 1 << 10);
            h.Engine.SettleWithTime();
            h.Publish();

            Assert.Same(h.View.Scenes["Chatting"], h.View.ProgramScene);
            Assert.True(h.View.StudioMode);
            var program = Assert.Single(h.Notices, notice => notice.Args is { PropertyName: "ProgramScene" });
            Assert.Equal((previous, h.View.ProgramScene), (program.Args!.OldValue, program.Args.NewValue));
            Assert.Contains(h.Notices, notice => ReferenceEquals(notice.Entity, previous) && notice.Property == "IsProgram");
            Assert.Contains(h.Notices, notice => ReferenceEquals(notice.Entity, h.View.ProgramScene) && notice.Property == "IsProgram");
            Assert.Contains(h.Notices, notice => notice.Args is { PropertyName: "IsStudioModeEnabled", NewValue: true });
        }

        [Fact]
        public void WhenNothingChanged_TheViewIsTheSameObject_AndThereAreNoNotices()
        {
            using var h = Started(world => world.AddScene("Gaming"));
            var view = h.View;

            h.Publish();

            Assert.Same(view, h.View);
            Assert.Empty(h.Notices);
        }

        [Fact]
        public void ANewSceneWithItsItems_IsAnnouncedBeforeTheirItems()
        {
            using var h = Started(world => world.AddInput("Mic"));
            var scene = h.World.AddScene("Chatting");
            h.World.AddItem(scene, h.World.Inputs[0].Uuid, "Mic");

            h.Engine.Event("SceneCreated", new { sceneName = "Chatting", sceneUuid = scene, isGroup = false }, 4);
            h.Engine.SettleWithTime();
            h.Publish();

            var added = h.Notices.Where(notice => notice.Args is { Kind: ObsChangeKind.Added }).Select(notice => notice.Args!.Entity).ToArray();
            Assert.Equal(2, added.Length);
            Assert.IsType<ObsScene>(added[0]);
            Assert.IsType<ObsSceneItem>(added[1]);
        }

        private sealed class PublisherHarness : IDisposable
        {
            public PublisherHarness(bool ignoreCase, StudioEngineOptions? engineOptions)
            {
                this.Engine = new EngineHarness(engineOptions);
                this.Studio = new ObsStudio(new ObsStudioOptions { IgnoreCase = ignoreCase });
                this.Publisher = new StudioPublisher(this.Studio, ignoreCase);
                this.View = this.Publisher.View;
            }

            public EngineHarness Engine { get; }

            public FakeWorld World => this.Engine.World;

            public ObsStudio Studio { get; }

            public StudioPublisher Publisher { get; }

            public StudioView View { get; private set; }

            public IReadOnlyList<StudioNotice> Notices => this.Publisher.Notices.ToArray();

            public StudioView Publish()
            {
                this.View = this.Publisher.Publish(this.Engine.Engine, this.Engine.Engine.TakeChanges());
                return this.View;
            }

            public void Dispose()
            {
                this.Studio.Dispose();
            }
        }
    }
}
