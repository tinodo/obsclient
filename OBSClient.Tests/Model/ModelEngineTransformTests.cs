namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds of the transform of a scene item when it is asked to follow the transforms (the events come with <see cref="ObsHighVolume.Transforms"/>): that it is not kept, and its events are not even
    /// acted on, when it is not asked; how the transform comes with the items and with the events, and how an event and a read decide which of them is newer; and the event that says that a scene item was selected.
    /// </summary>
    public class ModelEngineTransformTests
    {
        private const int TransformEvents = 524288;
        private const int SceneItemEvents = 128;

        // A scene "Gaming" with one scene item, the camera.
        private static EngineHarness Started(bool follow, out Guid scene, out int item, Action<FakeWorld, Guid, int>? setup = null)
        {
            var h = new EngineHarness(new ModelEngineOptions { HighVolume = follow ? ObsHighVolume.Transforms : ObsHighVolume.None });
            h.World.AddScene("Scene");
            var gaming = scene = h.World.AddScene("Gaming");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            var id = item = h.World.AddItem(gaming, camera, "Camera");
            setup?.Invoke(h.World, gaming, id);
            h.Start();
            h.SettleWithTime();
            h.Engine.TakeChanges();
            return h;
        }

        private static SceneItemTransform At(float x, float y = 0f)
        {
            return new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 0, 0, 0, 1080f, x, y, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = false };
        }

        private static void Transformed(EngineHarness h, Guid scene, int item, SceneItemTransform transform)
        {
            h.Event("SceneItemTransformChanged", new { sceneName = "Gaming", sceneUuid = scene, sceneItemId = item, sceneItemTransform = transform }, TransformEvents);
        }

        private static SceneItemTransform? TransformOf(EngineHarness h, Guid scene, int item)
        {
            return h.Engine.Snapshot().Scenes.Single(s => s.Uuid == scene).Items.Single(i => i.Id == item).Transform;
        }

        private static ModelChange[] Changes(EngineHarness h, ModelChangeKind kind)
        {
            return h.Engine.TakeChanges().Where(change => change.Kind == kind).ToArray();
        }

        private static ReadRequest[] NextRead(EngineHarness h)
        {
            return h.Take() is { } ticket ? ticket.Requests.ToArray() : [];
        }

        [Fact]
        public void WhenTheTransformsAreNotFollowed_AnItemHasNoTransform_AndItsEventIsNotActedOn()
        {
            var h = Started(follow: false, out var scene, out var item);

            Assert.Null(TransformOf(h, scene, item));
            Assert.False(h.Engine.Wants(EventType.SceneItemTransformChanged));
            Assert.True(ModelEngine.Handles(EventType.SceneItemTransformChanged));

            Transformed(h, scene, item, At(100));

            Assert.Null(TransformOf(h, scene, item));
            Assert.Empty(h.Engine.TakeChanges());
            Assert.Null(h.Take());
        }

        [Fact]
        public void WhenTheTransformsAreFollowed_ThePartOfTheEventsThatTheEngineWantsIsTheTransform_AndNothingElseThatIsSentOften()
        {
            var h = Started(follow: true, out _, out _);

            Assert.True(h.Engine.Wants(EventType.SceneItemTransformChanged));
            Assert.Equal(ObsHighVolume.Transforms, ModelEngine.HighVolumeOf(EventType.SceneItemTransformChanged));
            Assert.All(
                Enum.GetValues<EventType>().Where(type => type != EventType.SceneItemTransformChanged && ModelEngine.Handles(type)),
                type => Assert.Null(ModelEngine.HighVolumeOf(type)));
            Assert.True(new EngineHarness().Engine.Wants(EventType.SceneItemSelected));
        }

        [Fact]
        public void TheTransformComesWithTheItems_WhenTheyAreRead()
        {
            var h = Started(follow: true, out var scene, out var item, (world, sceneUuid, id) => world.Transforms[(sceneUuid, id)] = At(250, 40));

            Assert.Equal(At(250, 40), TransformOf(h, scene, item));
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void TheTransformOfAnItemThatNoTestGaveOne_IsWhatOBSStudioSaysOfANewItem()
        {
            var h = Started(follow: true, out var scene, out var item);

            Assert.Equal(FakeWorld.DefaultTransform, TransformOf(h, scene, item));
        }

        [Fact]
        public void AnEventOfTheTransform_ChangesIt_AndTheChangeSaysFromWhatToWhat()
        {
            var h = Started(follow: true, out var scene, out var item);

            Transformed(h, scene, item, At(100));

            Assert.Equal(At(100), TransformOf(h, scene, item));
            var change = Assert.Single(Changes(h, ModelChangeKind.ItemTransformChanged));
            Assert.Equal((scene, item), (change.Subject, change.ItemId));
            Assert.Equal(FakeWorld.DefaultTransform, change.OldValue);
            Assert.Equal(At(100), change.NewValue);
            Assert.Null(h.Take());
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnEventWithTheTransformThatTheModelHolds_IsNoChange()
        {
            var h = Started(follow: true, out var scene, out var item);
            Transformed(h, scene, item, At(100));
            h.Engine.TakeChanges();

            // OBS Studio 33.0.0 sends the transform again when something else about the item changes (a blend mode, for one).
            Transformed(h, scene, item, At(100));
            Transformed(h, scene, item, At(100));

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Equal(At(100), TransformOf(h, scene, item));
        }

        [Fact]
        public void ATransformThatIsChangedInTwoSteps_IsTwoChanges_AndTheLastIsWhatTheModelHolds()
        {
            var h = Started(follow: true, out var scene, out var item);
            var halfWay = new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 0, 0, 0, 1080f, 0f, 0f, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = true };
            var done = new SceneItemTransform(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 0, 10, 0, 3, 1080f, 0f, 0f, 0f, 1f, 1f, 1080f, 1920f, 1920f) { CropToBounds = true };

            // What a copy of a scene item gives: the crop comes after the rest.
            Transformed(h, scene, item, halfWay);
            Transformed(h, scene, item, done);

            Assert.Equal(done, TransformOf(h, scene, item));
            Assert.Equal(2, Changes(h, ModelChangeKind.ItemTransformChanged).Length);
        }

        [Fact]
        public void AnEventOfAnItemThatTheModelDoesNotKnow_MakesItsSceneBeRead()
        {
            var h = Started(follow: true, out var scene, out _);

            Transformed(h, scene, 99, At(5));

            Assert.Equal([ReadKind.Items], NextRead(h).Select(request => request.Kind).ToArray());
            Assert.Empty(h.Engine.TakeChanges());
        }

        [Fact]
        public void AnEventOfAnItemOfAScene_ThatWasRemoved_IsIgnored()
        {
            var h = Started(follow: true, out var scene, out var item);
            h.World.Scenes.RemoveAll(entry => entry.Uuid == scene);
            h.World.Items.Remove(scene);
            EngineEvents.SceneRemoved(h, scene, "Gaming");
            h.SettleWithTime();
            h.Engine.TakeChanges();

            Transformed(h, scene, item, At(5));

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Null(h.Take());
        }

        [Fact]
        public void ARead_ThatWasSentBeforeAnEvent_DoesNotUndoIt()
        {
            var h = Started(follow: true, out var scene, out var item);
            h.Engine.RequestRefresh(ReadKind.Items, scene, h.Now);
            var read = h.Take()!;

            // The event arrived after the read was sent: what the read says is older.
            Transformed(h, scene, item, At(300));
            h.Complete(read);

            Assert.Equal(At(300), TransformOf(h, scene, item));
        }

        [Fact]
        public void ARead_ThatWasSentAfterAnEvent_ShowsWhatTheEventDidAndWhatCameAfterIt()
        {
            var h = Started(follow: true, out var scene, out var item);
            Transformed(h, scene, item, At(300));
            h.Engine.TakeChanges();

            // OBS Studio moved the item again, and the event for that has not reached the model.
            h.World.Transforms[(scene, item)] = At(400);
            h.Engine.RequestRefresh(ReadKind.Items, scene, h.Now);
            h.Settle();

            Assert.Equal(At(400), TransformOf(h, scene, item));
            Assert.Single(Changes(h, ModelChangeKind.ItemTransformChanged));
        }

        [Fact]
        public void AnItemThatAnEventMade_ShowsItsTransform_WhenItIsAnnounced()
        {
            var h = Started(follow: true, out var scene, out _);
            var source = h.World.AddInput("Logo", "image_source", 8);
            var id = h.World.AddItem(scene, source, "Logo");
            h.World.Transforms[(scene, id)] = At(77, 88);

            EngineEvents.ItemCreated(h, scene, "Gaming", id, 1, source, "Logo");
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes.Single(s => s.Uuid == scene).Items, entry => entry.Id == id);
            h.SettleWithTime();

            Assert.Equal(At(77, 88), TransformOf(h, scene, id));
            var changes = h.Engine.TakeChanges();
            Assert.Contains(changes, change => change.Kind == ModelChangeKind.ItemAdded && change.ItemId == id);

            // What a read says of an item that is not announced yet is not a change of it.
            Assert.DoesNotContain(changes, change => change.Kind == ModelChangeKind.ItemTransformChanged && change.ItemId == id);
        }

        [Fact]
        public void ANewConnection_ReadsTheTransformsAgain()
        {
            var h = Started(follow: true, out var scene, out var item);
            h.End();
            h.World.Transforms[(scene, item)] = At(900);
            h.Engine.TakeChanges();

            h.Start();
            h.SettleWithTime();

            Assert.Equal(At(900), TransformOf(h, scene, item));
            var change = Assert.Single(Changes(h, ModelChangeKind.ItemTransformChanged));
            Assert.Equal(FakeWorld.DefaultTransform, change.OldValue);
        }

        [Fact]
        public void AnEventOfATransform_ThatCameWhileOBSStudioChangesItsSceneCollection_IsIgnored()
        {
            var h = Started(follow: true, out var scene, out var item);
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Other" }, 2);
            h.Engine.TakeChanges();

            Transformed(h, scene, item, At(100));

            Assert.Empty(h.Engine.TakeChanges());
        }

        [Fact]
        public void AnItemThatIsSelected_IsAnEventThatHappened_InTheOrderOfArrival()
        {
            var h = Started(follow: false, out var scene, out var item);

            h.Event("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = scene, sceneItemId = item }, SceneItemEvents);
            h.Event("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = scene, sceneItemId = item, sceneItemEnabled = false }, SceneItemEvents);
            h.Event("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = scene, sceneItemId = item }, SceneItemEvents);

            var changes = h.Engine.TakeChanges();
            Assert.Equal(
                [ModelChangeKind.Notification, ModelChangeKind.ItemEnabledChanged, ModelChangeKind.Notification],
                changes.Select(change => change.Kind).ToArray());
            var first = Assert.IsType<SceneItemSelectedEventArgs>(changes[0].NewValue);
            Assert.Equal((scene, item, EventType.SceneItemSelected), (first.SceneUuid, first.SceneItemId, (EventType)changes[0].OldValue!));
            Assert.Null(h.Take());
        }

        [Fact]
        public void AnItemThatIsSelectedBeforeItIsAnnounced_IsToldOf_WhenItIs_AndOnlyOnce()
        {
            var h = Started(follow: false, out var scene, out _);
            var source = h.World.AddInput("Logo", "image_source", 8);
            var id = h.World.AddItem(scene, source, "Logo");

            // A new item is selected at once, and the model reads its state after that.
            EngineEvents.ItemCreated(h, scene, "Gaming", id, 1, source, "Logo");
            h.Event("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = scene, sceneItemId = id }, SceneItemEvents);
            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.Notification);

            h.SettleWithTime();

            var changes = h.Engine.TakeChanges();
            var announced = Array.FindIndex(changes.ToArray(), change => change.Kind == ModelChangeKind.ItemAdded && change.ItemId == id);
            var selected = changes.Where(change => change.Kind == ModelChangeKind.Notification).ToArray();
            Assert.True(announced >= 0);
            var told = Assert.Single(selected);
            Assert.Equal(EventType.SceneItemSelected, (EventType)told.OldValue!);
            Assert.True(Array.IndexOf(changes.ToArray(), told) > announced, "The item is told of before the application knows that it exists.");
            Assert.Equal(id, Assert.IsType<SceneItemSelectedEventArgs>(told.NewValue).SceneItemId);

            h.Engine.TakeChanges();
            h.Settle();
            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.Notification);
        }

        [Fact]
        public void AnItemThatIsSelected_ButIsNotKnown_IsNotToldOf_AndMakesItsSceneBeRead()
        {
            var h = Started(follow: false, out var scene, out _);

            h.Event("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = scene, sceneItemId = 99 }, SceneItemEvents);

            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.Notification);
            Assert.Equal([ReadKind.Items], NextRead(h).Select(request => request.Kind).ToArray());
        }

        [Fact]
        public void AnItemThatIsSelected_WhileOBSStudioChangesItsSceneCollection_IsNotToldOf()
        {
            var h = Started(follow: false, out var scene, out var item);
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Other" }, 2);
            h.Engine.TakeChanges();

            h.Event("SceneItemSelected", new { sceneName = "Gaming", sceneUuid = scene, sceneItemId = item }, SceneItemEvents);

            Assert.Empty(h.Engine.TakeChanges());
        }
    }
}
