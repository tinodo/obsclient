namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The transform and the blend mode of a scene item, and a copy of it: what the model shows of the transform when it is asked to follow it (<see cref="ObsHighVolume.Transforms"/>) and what it does when it is
    /// not, what the calls send, what they wait for, and what they do when OBS Studio refuses; and the event that says that a scene item was selected.
    /// </summary>
    public class ModelTransformTests
    {
        private const int TransformEvents = 524288;
        private const int SceneItemEvents = 128;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsModelOptions Following()
        {
            var options = CallFixture.DefaultOptions();
            options.HighVolume = ObsHighVolume.Transforms;
            return options;
        }

        private static SceneItemTransform At(float x, float y = 0f)
        {
            return new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 0, 0, 0, 1080f, x, y, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = false };
        }

        private static async Task<CallFixture> StartAsync(bool follow, Action<FakeWorld, Guid>? setup = null, Action<ObsModelOptions>? tune = null)
        {
            var gaming = Guid.Empty;
            var options = follow ? Following() : null;
            if (tune is not null)
            {
                options ??= CallFixture.DefaultOptions();
                tune(options);
            }

            return await CallFixture.StartAsync(options, world =>
            {
                gaming = world.Scenes.First(scene => scene.Name == "Gaming").Uuid;
                setup?.Invoke(world, gaming);
            });
        }

        private static Task SendTransformAsync(CallFixture test, ObsSceneItem item, SceneItemTransform transform)
        {
            return test.Rig.Responder.SendEventAsync(
                "SceneItemTransformChanged",
                new { sceneName = item.Scene.Name, sceneUuid = item.Scene.Uuid, sceneItemId = item.Id, sceneItemTransform = transform },
                TransformEvents);
        }

        private static int Reads(CallFixture test)
        {
            return test.Rig.Responder.RequestLog.Count(type => type == "GetSceneItemList");
        }

        // How many times the model told that the transform of an item changed.
        private static int Told(CallFixture test)
        {
            return test.Rig.Changes.Count(change => change.PropertyName == nameof(ObsSceneItem.Transform));
        }

        [Fact]
        public async Task TheTransform_IsNull_WhenTheModelWasNotAskedToFollowIt_AndItsEventsAreNotActedOn()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[0];

            await SendTransformAsync(test, item, At(100));
            await Task.Delay(100);
            await test.Model.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);

            Assert.Null(item.Transform);
            Assert.Equal(0, Told(test));
        }

        [Fact]
        public async Task TheTransform_IsWhatOBSStudioSays_WhenTheModelFollowsIt()
        {
            await using var test = await StartAsync(follow: true, (world, gaming) => world.Transforms[(gaming, 1)] = At(250, 40));

            Assert.Equal(At(250, 40), test.Gaming.Items[0].Transform);
            Assert.Equal(FakeWorld.DefaultTransform, test.Gaming.Items[1].Transform);
        }

        [Fact]
        public async Task ATransformThatChanges_IsTold_WithTheObjectBeforeAndAfter()
        {
            await using var test = await StartAsync(follow: true);
            var item = test.Gaming.Items[0];

            await SendTransformAsync(test, item, At(100));
            await ModelRig.EventuallyAsync(() => Told(test) == 1, "the change of the transform to be told");

            Assert.Equal(At(100), item.Transform);
            var change = Assert.Single(test.Rig.Changes, entry => entry.PropertyName == nameof(ObsSceneItem.Transform));
            Assert.Equal(ObsChangeKind.PropertyChanged, change.Kind);
            Assert.Same(item, change.Entity);
            Assert.Equal(FakeWorld.DefaultTransform, change.OldValue);
            Assert.Equal(At(100), change.NewValue);
        }

        [Fact]
        public async Task ATransformEvent_ThatSaysWhatTheModelHolds_IsNotTold()
        {
            await using var test = await StartAsync(follow: true);
            var item = test.Gaming.Items[0];
            await SendTransformAsync(test, item, At(100));
            await ModelRig.EventuallyAsync(() => Told(test) == 1, "the change of the transform to be told");

            // OBS Studio sends the transform again when something else about the item changes: a blend mode, a source that is the same size.
            await SendTransformAsync(test, item, At(100));
            await SendTransformAsync(test, item, At(100));
            await test.Model.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);
            await Task.Delay(100);

            Assert.Equal(1, Told(test));
        }

        [Fact]
        public async Task TheTransform_OfAnItemThatWasMadeAfterTheConnection_IsThere_WhenTheItemIs()
        {
            await using var test = await StartAsync(follow: true);
            var created = Guid.Empty;
            var id = 0;
            test.Rig.Responder.Change(world =>
            {
                var scene = world.Scenes.First(entry => entry.Name == "Gaming").Uuid;
                created = world.AddInput("Logo");
                id = world.AddItem(scene, created, "Logo");
                world.Transforms[(scene, id)] = At(77, 88);
            });
            test.Rig.Responder.Change(world => { });
            await test.Rig.Responder.SendEventAsync("InputCreated", new { inputName = "Logo", inputUuid = created, inputKind = "color_source_v3", unversionedInputKind = "color_source", inputKindCaps = 32777, inputSettings = new { }, defaultInputSettings = new { } }, 8);
            await test.Rig.Responder.SendEventAsync("SceneItemCreated", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sourceName = "Logo", sourceUuid = created, sceneItemId = id, sceneItemIndex = 2 }, SceneItemEvents);

            await ModelRig.EventuallyAsync(() => test.Gaming.Items.Any(item => item.Id == id), "the new scene item to be shown");

            Assert.Equal(At(77, 88), test.Gaming.Items.Single(item => item.Id == id).Transform);
        }

        [Fact]
        public async Task GetTransformAsync_AsksOBSStudio_AndReturnsTheTransformAsItSaidIt()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[1];
            test.Answer("GetSceneItemTransform", 100, new { sceneItemTransform = At(12.5f, 7f) });

            var transform = await item.GetTransformAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(At(12.5f, 7f), transform);
            var sent = Assert.Single(test.AllSent("GetSceneItemTransform"));
            Assert.Equal(test.GamingUuid, sent.GetProperty("sceneUuid").GetGuid());
            Assert.Equal(item.Id, sent.GetProperty("sceneItemId").GetInt32());
            Assert.Null(item.Transform);
        }

        [Fact]
        public async Task GetTransformAsync_OfAnItemThatOBSStudioDoesNotKnow_IsARemoval()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[0];
            test.Answer("GetSceneItemTransform", 600, "No scene items were found in scene `Gaming` with the ID `1`.");

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => item.GetTransformAsync().WithTimeoutAsync(Timeout));

            Assert.Contains("scene item 1", exception.Message);
        }

        [Fact]
        public async Task SetTransformAsync_SendsTheMembersThatAreSet_AndOnlyThose()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[0];
            test.Answer("SetSceneItemTransform");

            await item.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 100f, BoundsType = Bounds.OBS_BOUNDS_STRETCH, CropToBounds = true }).WithTimeoutAsync(Timeout);

            var sent = Assert.Single(test.AllSent("SetSceneItemTransform"));
            Assert.Equal(test.GamingUuid, sent.GetProperty("sceneUuid").GetGuid());
            Assert.Equal(item.Id, sent.GetProperty("sceneItemId").GetInt32());
            var transform = sent.GetProperty("sceneItemTransform");
            Assert.Equal(["boundsType", "cropToBounds", "positionX"], transform.EnumerateObject().Select(property => property.Name).Order().ToArray());
            Assert.Equal(100f, transform.GetProperty("positionX").GetSingle());
            Assert.Equal("OBS_BOUNDS_STRETCH", transform.GetProperty("boundsType").GetString());
            Assert.True(transform.GetProperty("cropToBounds").GetBoolean());
        }

        [Fact]
        public async Task SetTransformAsync_WithAnUpdateThatSetsNothing_IsAnArgumentException_AndSendsNothing()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[0];

            Assert.Throws<ArgumentNullException>(() => { _ = item.SetTransformAsync(null!); });
            Assert.Throws<ArgumentException>(() => { _ = item.SetTransformAsync(new SceneItemTransformUpdate()); });

            Assert.Empty(test.AllSent("SetSceneItemTransform"));
        }

        [Fact]
        public async Task SetTransformAsync_WhenTheModelFollowsTheTransform_ReturnsWhenTheModelShowsTheChange()
        {
            await using var test = await StartAsync(follow: true);
            var item = test.Gaming.Items[0];
            test.Answer("SetSceneItemTransform");

            var task = item.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 100f });
            await ModelRig.EventuallyAsync(() => test.Count("SetSceneItemTransform") == 1, "the change to be sent");
            await Task.Delay(50);
            Assert.False(task.IsCompleted);

            await SendTransformAsync(test, item, At(100));
            await task.WithTimeoutAsync(Timeout);

            Assert.Equal(100f, item.Transform!.PositionX);
        }

        [Fact]
        public async Task SetTransformAsync_WhenTheChangeComesInTwoSteps_WaitsForTheLastOfThem()
        {
            await using var test = await StartAsync(follow: true);
            var item = test.Gaming.Items[0];
            test.Answer("SetSceneItemTransform");
            var update = new SceneItemTransformUpdate { CropToBounds = true, CropLeft = 10 };
            var halfWay = new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 0, 0, 0, 1080f, 0f, 0f, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = true };
            var done = new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 10, 0, 0, 1080f, 0f, 0f, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = true };

            var task = item.SetTransformAsync(update);
            await ModelRig.EventuallyAsync(() => test.Count("SetSceneItemTransform") == 1, "the change to be sent");
            await SendTransformAsync(test, item, halfWay);
            await Task.Delay(50);
            Assert.False(task.IsCompleted);

            await SendTransformAsync(test, item, done);
            await task.WithTimeoutAsync(Timeout);

            Assert.Equal(10, item.Transform!.CropLeft);
        }

        [Fact]
        public async Task SetTransformAsync_WhenTheEventDoesNotComeInTime_ReadsTheSceneItems_AndEndsWithWhatOBSStudioHolds()
        {
            await using var test = await StartAsync(follow: true);
            var item = test.Gaming.Items[0];
            test.Answer("SetSceneItemTransform");
            test.Rig.Responder.Change(world => world.Transforms[(test.GamingUuid, 1)] = At(100));
            var readsBefore = Reads(test);

            await item.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 100f }).WithTimeoutAsync(Timeout);

            Assert.True(Reads(test) > readsBefore, "The model did not read the scene items.");
            Assert.Equal(100f, item.Transform!.PositionX);
        }

        [Fact]
        public async Task SetTransformAsync_WhenTheModelDoesNotFollowTheTransform_ReturnsWhenOBSStudioHasAnswered_WithoutReading()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[0];
            test.Answer("SetSceneItemTransform");
            var readsBefore = Reads(test);

            await item.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 100f }).WithTimeoutAsync(Timeout);

            Assert.Equal(readsBefore, Reads(test));
            Assert.Null(item.Transform);
        }

        [Fact]
        public async Task SetTransformAsync_WhenOBSStudioRefuses_IsAResponseException_WithTheCode()
        {
            await using var test = await StartAsync(follow: true);
            var item = test.Gaming.Items[0];
            test.Answer("SetSceneItemTransform", 402, "The field value of `rotation` is above the maximum of `360.000000`");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => item.SetTransformAsync(new SceneItemTransformUpdate { Rotation = 370f }).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.RequestFieldOutOfRange, exception.ErrorCode);
            Assert.Equal(FakeWorld.DefaultTransform, item.Transform);
        }

        [Fact]
        public async Task TheBlendMode_IsAskedFor_AndSet_ByItsName()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[0];
            test.Answer("GetSceneItemBlendMode", 100, new { sceneItemBlendMode = "OBS_BLEND_MULTIPLY" });
            test.Answer("SetSceneItemBlendMode");

            var mode = await item.GetBlendModeAsync().WithTimeoutAsync(Timeout);
            await item.SetBlendModeAsync(BlendMode.OBS_BLEND_SCREEN).WithTimeoutAsync(Timeout);

            Assert.Equal(BlendMode.OBS_BLEND_MULTIPLY, mode);
            var sent = Assert.Single(test.AllSent("SetSceneItemBlendMode"));
            Assert.Equal(test.GamingUuid, sent.GetProperty("sceneUuid").GetGuid());
            Assert.Equal(item.Id, sent.GetProperty("sceneItemId").GetInt32());
            Assert.Equal("OBS_BLEND_SCREEN", sent.GetProperty("sceneItemBlendMode").GetString());
        }

        [Fact]
        public async Task ABlendModeThatIsNotOne_IsAnArgumentOutOfRangeException_AndSendsNothing()
        {
            await using var test = await StartAsync(follow: false);

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Gaming.Items[0].SetBlendModeAsync((BlendMode)99); });

            Assert.Empty(test.AllSent("SetSceneItemBlendMode"));
        }

        [Fact]
        public async Task ASetBlendMode_DoesNotChangeTheTransform_WhenOBSStudioSendsItAgain()
        {
            await using var test = await StartAsync(follow: true);
            var item = test.Gaming.Items[0];
            test.Answer("SetSceneItemBlendMode");
            await SendTransformAsync(test, item, At(100));
            await ModelRig.EventuallyAsync(() => Told(test) == 1, "the change of the transform to be told");

            await item.SetBlendModeAsync(BlendMode.OBS_BLEND_ADDITIVE).WithTimeoutAsync(Timeout);
            await SendTransformAsync(test, item, At(100));
            await test.Model.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);
            await Task.Delay(100);

            Assert.Equal(1, Told(test));
        }

        [Fact]
        public async Task ADuplicate_IsMadeInTheSceneOfTheOriginal_WithoutADestination_AndReturnedWhenTheModelShowsIt()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[0];
            var copyId = 0;
            test.Rig.Responder.Change(world => copyId = world.AddItem(test.GamingUuid, world.Inputs.First(input => input.Name == "Camera").Uuid, "Camera"));
            test.Answer("DuplicateSceneItem", 100, new { sceneItemId = copyId });

            var copy = await item.DuplicateAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(copyId, copy.Id);
            Assert.Same(test.Gaming, copy.Scene);
            Assert.Contains(copy, test.Gaming.Items);
            var sent = Assert.Single(test.AllSent("DuplicateSceneItem"));
            Assert.Equal(test.GamingUuid, sent.GetProperty("sceneUuid").GetGuid());
            Assert.Equal(item.Id, sent.GetProperty("sceneItemId").GetInt32());
            Assert.Equal(["sceneItemId", "sceneUuid"], sent.EnumerateObject().Select(property => property.Name).Order().ToArray());
        }

        [Fact]
        public async Task ADuplicateOfACroppedItem_IsReturnedWhenTheModelShowsTheCrop_NotWhenItFirstShowsTheCopy()
        {
            // OBS Studio announces a copy in steps, and gives the crop last: the copy is in the model with the transform that comes before it, and the call waits for the rest.
            var cropped = new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 20, 0, 0, 1080f, 10f, 0f, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = false };
            await using var test = await StartAsync(
                follow: true,
                (world, gaming) => world.Transforms[(gaming, 1)] = cropped,
                options =>
                {
                    options.ConfirmationTimeout = TimeSpan.FromSeconds(5);
                    options.WriteTimeout = TimeSpan.FromSeconds(10);
                });
            var item = test.Gaming.Items[0];
            var copyId = 0;
            test.Rig.Responder.Change(world =>
            {
                copyId = world.AddItem(test.GamingUuid, world.Inputs.First(input => input.Name == "Camera").Uuid, "Camera");
                world.Transforms[(test.GamingUuid, copyId)] = At(10);
            });
            test.Answer("DuplicateSceneItem", 100, new { sceneItemId = copyId });

            var duplicating = item.DuplicateAsync();
            await test.Rig.Responder.SendEventAsync(
                "SceneItemCreated",
                new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sceneItemId = copyId, sceneItemIndex = test.Gaming.Items.Count, sourceName = "Camera", sourceUuid = test.Model.Inputs["Camera"].Uuid },
                SceneItemEvents);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => test.Gaming.Items.Any(candidate => candidate.Id == copyId), Timeout, TimeSpan.FromMilliseconds(5)), "The copy is not in the model.");
            await Task.Delay(100);

            var first = test.Gaming.Items.Single(candidate => candidate.Id == copyId);
            Assert.False(duplicating.IsCompleted, duplicating.IsFaulted ? duplicating.Exception!.ToString() : "the call completed");
            Assert.Equal(0, first.Transform!.CropLeft);

            test.Rig.Responder.Change(world => world.Transforms[(test.GamingUuid, copyId)] = cropped);
            await SendTransformAsync(test, first, cropped);

            var copy = await duplicating.WithTimeoutAsync(Timeout);
            Assert.Same(first, copy);
            Assert.Equal(20, copy.Transform!.CropLeft);
        }

        [Fact]
        public async Task ADuplicateInAnotherScene_GivesTheNameAndTheUuidOfTheDestination()
        {
            // OBS Studio 33.0.0 reads the destination only when the request has its name, and then the UUID wins: a UUID alone makes the copy in the scene of the original.
            Guid other = Guid.Empty;
            await using var test = await StartAsync(follow: false, (world, gaming) => other = world.AddScene("Other"));
            var item = test.Gaming.Items[0];
            var copyId = 0;
            test.Rig.Responder.Change(world => copyId = world.AddItem(other, world.Inputs.First(input => input.Name == "Camera").Uuid, "Camera"));
            test.Answer("DuplicateSceneItem", 100, new { sceneItemId = copyId });
            var destination = test.Model.Scenes["Other"];

            var copy = await item.DuplicateAsync(destination).WithTimeoutAsync(Timeout);

            Assert.Same(destination, copy.Scene);
            var sent = Assert.Single(test.AllSent("DuplicateSceneItem"));
            Assert.Equal("Other", sent.GetProperty("destinationSceneName").GetString());
            Assert.Equal(other, sent.GetProperty("destinationSceneUuid").GetGuid());
            Assert.Equal(test.GamingUuid, sent.GetProperty("sceneUuid").GetGuid());
        }

        [Fact]
        public async Task ADuplicateInAScene_OfAnotherModel_IsAnArgumentException_AndSendsNothing()
        {
            await using var test = await StartAsync(follow: false);
            await using var other = await StartAsync(follow: false);

            Assert.Throws<ArgumentException>(() => { _ = test.Gaming.Items[0].DuplicateAsync(other.Gaming); });

            Assert.Empty(test.AllSent("DuplicateSceneItem"));
        }

        [Fact]
        public async Task ADuplicateOfAnItemOBSStudioDoesNotKnow_IsARemoval()
        {
            await using var test = await StartAsync(follow: false);
            test.Answer("DuplicateSceneItem", 600, "No scene items were found in scene `Gaming` with the ID `1`.");

            await Assert.ThrowsAsync<ObsRemovedException>(() => test.Gaming.Items[0].DuplicateAsync().WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task ADuplicateInADestinationThatOBSStudioDoesNotKnow_IsARemovalOfTheDestination_NotOfTheItem()
        {
            Guid other = Guid.Empty;
            await using var test = await StartAsync(follow: false, (world, gaming) => other = world.AddScene("Other"));
            test.Answer("DuplicateSceneItem", 600, "No source was found by the name of `Other` within the canvas `Main`.");
            var destination = test.Model.Scenes["Other"];

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Gaming.Items[0].DuplicateAsync(destination).WithTimeoutAsync(Timeout));

            Assert.Contains("Other", exception.Message);
            Assert.DoesNotContain("scene item", exception.Message);
        }

        [Fact]
        public async Task ADuplicateInADestinationThatOBSStudioDoesNotFindByItsUuid_IsARemovalOfTheDestination_NotOfTheItem()
        {
            // The UUID that is there is the one that finds the destination, and so it is the one that the answer names.
            Guid other = Guid.Empty;
            await using var test = await StartAsync(follow: false, (world, gaming) => other = world.AddScene("Other"));
            test.Answer("DuplicateSceneItem", 600, $"No source was found by the UUID of `{other:D}`.");
            var destination = test.Model.Scenes["Other"];

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Gaming.Items[0].DuplicateAsync(destination).WithTimeoutAsync(Timeout));

            Assert.Contains("Other", exception.Message);
            Assert.DoesNotContain("scene item", exception.Message);
        }

        [Fact]
        public async Task AnItemThatIsSelected_IsToldOf_WithTheItemOfTheModel()
        {
            await using var test = await StartAsync(follow: false);
            var item = test.Gaming.Items[1];
            var selected = new List<ObsSceneItem>();
            test.Model.SceneItemSelected += (sender, e) =>
            {
                Assert.Same(test.Model, sender);
                lock (selected)
                {
                    selected.Add(e.Item);
                }
            };

            await test.Rig.Responder.SendEventAsync("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sceneItemId = item.Id }, SceneItemEvents);
            await ModelRig.EventuallyAsync(() => { lock (selected) { return selected.Count == 1; } }, "the selection to be told");

            Assert.Same(item, selected[0]);
        }

        [Fact]
        public async Task ANewItemThatIsSelectedAtOnce_IsToldOf_AfterTheModelToldThatItExists()
        {
            await using var test = await StartAsync(follow: false);
            var order = new List<string>();
            test.Model.Changed += (_, e) =>
            {
                if (e.Kind == ObsChangeKind.Added && e.Entity is ObsSceneItem added)
                {
                    lock (order)
                    {
                        order.Add($"added {added.Id}");
                    }
                }
            };
            test.Model.SceneItemSelected += (_, e) =>
            {
                lock (order)
                {
                    order.Add($"selected {e.Item.Id}");
                }
            };
            var created = Guid.Empty;
            var id = 0;
            test.Rig.Responder.Change(world =>
            {
                created = world.AddInput("Logo");
                id = world.AddItem(test.GamingUuid, created, "Logo");
            });

            // What OBS Studio does when a scene item is made: it is selected at once, before the model has read it.
            await test.Rig.Responder.SendEventAsync("InputCreated", new { inputName = "Logo", inputUuid = created, inputKind = "color_source_v3", unversionedInputKind = "color_source", inputKindCaps = 32777, inputSettings = new { }, defaultInputSettings = new { } }, 8);
            await test.Rig.Responder.SendEventAsync("SceneItemCreated", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sourceName = "Logo", sourceUuid = created, sceneItemId = id, sceneItemIndex = 2 }, SceneItemEvents);
            await test.Rig.Responder.SendEventAsync("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sceneItemId = id }, SceneItemEvents);
            await ModelRig.EventuallyAsync(() => { lock (order) { return order.Count == 2; } }, "the item to be told of and selected");

            Assert.Equal([$"added {id}", $"selected {id}"], order.ToArray());
        }

        // A new item that is made and selected while OBS Studio is not ready to give the settings of its input: the model has read the item, and cannot tell of it until the input is complete.
        private static async Task<int> MakeAnItemThatWaitsAsync(CallFixture test, string name)
        {
            test.Answer("GetInputSettings", 207);
            var created = Guid.Empty;
            var id = 0;
            test.Rig.Responder.Change(world =>
            {
                created = world.AddInput(name);
                id = world.AddItem(test.GamingUuid, created, name);
            });
            var reads = Reads(test);
            await test.Rig.Responder.SendEventAsync("InputCreated", new { inputName = name, inputUuid = created, inputKind = "color_source_v3", unversionedInputKind = "color_source", inputKindCaps = 32777, inputSettings = new { }, defaultInputSettings = new { } }, 8);
            await test.Rig.Responder.SendEventAsync("SceneItemCreated", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sourceName = name, sourceUuid = created, sceneItemId = id, sceneItemIndex = 2 }, SceneItemEvents);
            await test.Rig.Responder.SendEventAsync("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sceneItemId = id }, SceneItemEvents);
            await ModelRig.EventuallyAsync(() => Reads(test) > reads, "the item to be read");
            await Task.Delay(300);
            return id;
        }

        [Fact]
        public async Task ASelection_OfANewItemThatTheModelCannotTellOfYet_IsToldOf_WhenTheItemIs()
        {
            await using var test = await StartAsync(follow: false);
            var order = new List<string>();
            test.Model.Changed += (_, e) =>
            {
                if (e.Kind == ObsChangeKind.Added && e.Entity is ObsSceneItem added)
                {
                    lock (order)
                    {
                        order.Add($"added {added.Id}");
                    }
                }
            };
            test.Model.SceneItemSelected += (_, e) =>
            {
                lock (order)
                {
                    order.Add($"selected {e.Item.Id}");
                }
            };

            var id = await MakeAnItemThatWaitsAsync(test, "Logo");
            lock (order)
            {
                Assert.Empty(order);
            }

            test.StopAnswering("GetInputSettings");
            await ModelRig.EventuallyAsync(() => { lock (order) { return order.Count == 2; } }, "the item to be told of and selected", TimeSpan.FromSeconds(15));

            Assert.Equal([$"added {id}", $"selected {id}"], order.ToArray());
        }

        [Fact]
        public async Task TheSelections_AreToldOf_InTheOrderOfArrival_AlsoWhenTheFirstOneWaitsForItsItem()
        {
            await using var test = await StartAsync(follow: false);
            var selected = new List<int>();
            test.Model.SceneItemSelected += (_, e) =>
            {
                lock (selected)
                {
                    selected.Add(e.Item.Id);
                }
            };
            var known = test.Gaming.Items[1];

            var id = await MakeAnItemThatWaitsAsync(test, "Logo");

            // An item that the application knows is selected after the new one: it does not overtake it.
            await test.Rig.Responder.SendEventAsync("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sceneItemId = known.Id }, SceneItemEvents);
            await Task.Delay(300);
            lock (selected)
            {
                Assert.Empty(selected);
            }

            test.StopAnswering("GetInputSettings");
            await ModelRig.EventuallyAsync(() => { lock (selected) { return selected.Count == 2; } }, "both selections to be told", TimeSpan.FromSeconds(15));

            Assert.Equal([id, known.Id], selected.ToArray());
        }

        [Fact]
        public async Task AnItemThatIsSelected_ButTheModelDoesNotHaveIt_IsNotToldOf()
        {
            await using var test = await StartAsync(follow: false);
            var told = 0;
            test.Model.SceneItemSelected += (_, _) => Interlocked.Increment(ref told);

            await test.Rig.Responder.SendEventAsync("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = test.GamingUuid, sceneItemId = 99 }, SceneItemEvents);
            await test.Model.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);
            await Task.Delay(100);

            Assert.Equal(0, told);
        }

        [Fact]
        public void TheOption_AcceptsTheFlagsThatThereAre_AndNoOther()
        {
            using var model = new ObsModel(new ObsModelOptions { HighVolume = ObsHighVolume.Transforms });

            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsModel(new ObsModelOptions { HighVolume = (ObsHighVolume)(1 << 20) }));
            Assert.Equal(ObsHighVolume.None, new ObsModelOptions().HighVolume);
        }

        [Fact]
        public void TheEventsForTheTransforms_AreSubscribedToOnlyWhenAskedFor()
        {
            Assert.Equal(EventSubscriptions.None, ModelHost.SubscriptionsFor(ObsHighVolume.None));
            Assert.Equal(EventSubscriptions.SceneItemTransformChanged, ModelHost.SubscriptionsFor(ObsHighVolume.Transforms));
            Assert.False(ModelHost.RequiredEvents.HasFlag(EventSubscriptions.SceneItemTransformChanged));
        }
    }
}
