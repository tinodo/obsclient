namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// What the model holds about the filters of the scenes and the inputs: how it reads them, what the events of OBS Studio make of them (a filter has no UUID, and the events say the name of the
    /// source), and what it does with a read that was sent before an event that changed them.
    /// </summary>
    public class StudioEngineFilterTests
    {
        private const int FilterEvents = 32;

        // A world with a scene "Gaming" that has the filters "Color" and "Sharpen", an input "Mic" that has "Gain", "Gate" and "Limiter", and a scene "Scene" and an input "Camera" that have none.
        private static EngineHarness Started(out Guid gaming, out Guid mic, Action<FakeWorld>? setup = null)
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gamingUuid = gaming = h.World.AddScene("Gaming");
            var micUuid = mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.AddInput("Camera", "dshow_input", 8);
            h.World.AddItem(gamingUuid, micUuid, "Mic");
            h.World.AddFilter(gamingUuid, "Color", "color_filter_v2");
            h.World.AddFilter(gamingUuid, "Sharpen", "sharpness_filter_v2", enabled: false);
            h.World.AddFilter(micUuid, "Gain", "gain_filter");
            h.World.AddFilter(micUuid, "Gate", "noise_gate_filter");
            h.World.AddFilter(micUuid, "Limiter", "limiter_filter");
            setup?.Invoke(h.World);
            h.Start();
            h.SettleWithTime();
            h.Engine.TakeChanges();
            return h;
        }

        private static string[] Names(EngineHarness h, Guid source)
        {
            return h.Engine.FiltersOf(source).Select(filter => filter.Name).ToArray();
        }

        private static StudioChange[] Changes(EngineHarness h, StudioChangeKind kind)
        {
            return h.Engine.TakeChanges().Where(change => change.Kind == kind).ToArray();
        }

        private static void Enabled(EngineHarness h, string source, string filter, bool enabled)
        {
            h.Event("SourceFilterEnableStateChanged", new { sourceName = source, filterName = filter, filterEnabled = enabled }, FilterEvents);
        }

        private static void Created(EngineHarness h, string source, string filter, int index)
        {
            h.Event("SourceFilterCreated", new { sourceName = source, filterName = filter, filterKind = "color_filter_v2", filterIndex = index, filterSettings = new { }, defaultFilterSettings = new { } }, FilterEvents);
        }

        private static void Removed(EngineHarness h, string source, string filter)
        {
            h.Event("SourceFilterRemoved", new { sourceName = source, filterName = filter }, FilterEvents);
        }

        private static void Renamed(EngineHarness h, string source, string oldName, string newName)
        {
            h.Event("SourceFilterNameChanged", new { sourceName = source, oldFilterName = oldName, filterName = newName }, FilterEvents);
        }

        private static void Reindexed(EngineHarness h, string source, params string[] order)
        {
            var filters = order.Select((name, index) => new { filterEnabled = true, filterIndex = index, filterKind = "color_filter_v2", filterName = name, filterSettings = new { } }).ToArray();
            h.Event("SourceFilterListReindexed", new { sourceName = source, filters }, FilterEvents);
        }

        // The read of the filters that the engine wants next, or null.
        private static ReadRequest[] NextRead(EngineHarness h)
        {
            return h.Take() is { } ticket ? ticket.Requests.ToArray() : [];
        }

        [Fact]
        public void TheFilters_OfEverySceneAndInput_AreRead_BeforeTheModelIsSettled()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.AddFilter(gaming, "Color", "color_filter_v2");
            h.World.AddFilter(mic, "Gain", "gain_filter");
            h.Start();

            var requests = new List<ReadRequest>();
            while (h.Take() is { } ticket)
            {
                requests.AddRange(ticket.Requests);
                Assert.False(h.Engine.IsSettled);
                h.Complete(ticket);
            }

            var reads = requests.Where(request => request.Kind == ReadKind.Filters).ToArray();
            Assert.Equal(3, reads.Length);
            Assert.All(reads, read => Assert.Equal(OBSStudioClient.Enums.RequestType.GetSourceFilterList, read.Type));
            Assert.Equal(new[] { gaming, mic }.Order(), reads.Select(read => read.Target).Where(target => target == gaming || target == mic).Order());
            Assert.True(h.Engine.IsSettled);
            Assert.Equal(new[] { "Color" }, Names(h, gaming));
            Assert.Equal(new[] { "Gain" }, Names(h, mic));
        }

        [Fact]
        public void TheFilters_AreInTheModel_WithTheirKindPositionAndState()
        {
            var h = Started(out var gaming, out var mic);

            var filters = h.Engine.FiltersOf(mic);
            Assert.Equal(new[] { "Gain", "Gate", "Limiter" }, filters.Select(filter => filter.Name).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, filters.Select(filter => filter.Index).ToArray());
            Assert.Equal(new[] { "gain_filter", "noise_gate_filter", "limiter_filter" }, filters.Select(filter => filter.Kind).ToArray());
            Assert.All(filters, filter => Assert.True(filter.Enabled));
            Assert.Equal(new[] { true, false }, h.Engine.FiltersOf(gaming).Select(filter => filter.Enabled).ToArray());
            Assert.Equal(5, filters.Concat(h.Engine.FiltersOf(gaming)).Select(filter => filter.Id).Distinct().Count());
            Assert.Empty(h.Engine.FiltersOf(Guid.NewGuid()));
        }

        [Fact]
        public void EveryFilterThatAReadFinds_IsToldAsAdded()
        {
            var h = new EngineHarness();
            var scene = h.World.AddScene("Scene");
            h.World.AddFilter(scene, "Color");
            h.World.AddFilter(scene, "Sharpen");
            h.Start();
            h.SettleWithTime();

            var added = Changes(h, StudioChangeKind.FilterAdded);

            Assert.Equal(new[] { "Color", "Sharpen" }, added.Select(change => change.Text).Order().ToArray());
            Assert.All(added, change => Assert.Equal(scene, change.Subject));
        }

        [Fact]
        public void AnEventThatEnablesAFilter_IsAppliedAtOnce_WithoutARead()
        {
            var h = Started(out _, out var mic);

            Enabled(h, "Mic", "Gate", false);

            Assert.False(h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Gate").Enabled);
            var change = Assert.Single(Changes(h, StudioChangeKind.FilterEnabledChanged));
            Assert.Equal("Gate", change.Text);
            Assert.Equal(true, change.OldValue);
            Assert.Equal(false, change.NewValue);
            Assert.Null(h.Take());
            Assert.True(h.Engine.IsSettled);

            // The same again says nothing new.
            Enabled(h, "Mic", "Gate", false);
            Assert.Empty(h.Engine.TakeChanges());
        }

        [Fact]
        public void AnEventThatEnablesAFilterThatTheModelDoesNotHold_MakesTheFiltersBeRead()
        {
            var h = Started(out _, out var mic);

            Enabled(h, "Mic", "Unknown", true);

            var request = Assert.Single(NextRead(h));
            Assert.Equal(ReadKind.Filters, request.Kind);
            Assert.Equal(mic, request.Target);
        }

        [Fact]
        public void AFilterThatWasRemoved_IsGoneAtOnce_TheOnesBehindItMoveUp_AndTheFiltersAreRead()
        {
            var h = Started(out _, out var mic);

            Removed(h, "Mic", "Gain");

            Assert.Equal(new[] { "Gate", "Limiter" }, Names(h, mic));
            Assert.Equal(new[] { 0, 1 }, h.Engine.FiltersOf(mic).Select(filter => filter.Index).ToArray());
            Assert.Equal("Gain", Assert.Single(Changes(h, StudioChangeKind.FilterRemoved)).Text);
            var request = Assert.Single(NextRead(h));
            Assert.Equal(mic, request.Target);

            // The read says the same.
            h.World.RemoveFilter(mic, "Gain");
            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            h.SettleWithTime();
            Assert.Equal(new[] { "Gate", "Limiter" }, Names(h, mic));
            Assert.Equal(new[] { 0, 1 }, h.Engine.FiltersOf(mic).Select(filter => filter.Index).ToArray());
        }

        [Fact]
        public void AFilterThatWasCreated_AppearsWhenTheFiltersHaveBeenRead_WithItsState()
        {
            var h = Started(out var gaming, out _);
            h.World.AddFilter(gaming, "Blur", "obs_blur", enabled: false);

            Created(h, "Gaming", "Blur", 2);

            // Its state is not in the event: it is not in the model before the read.
            Assert.Equal(new[] { "Color", "Sharpen" }, Names(h, gaming));
            Assert.False(h.Engine.IsSettled);
            h.SettleWithTime();

            var blur = h.Engine.FiltersOf(gaming).Last();
            Assert.Equal("Blur", blur.Name);
            Assert.Equal(2, blur.Index);
            Assert.False(blur.Enabled);
            Assert.Equal("obs_blur", blur.Kind);
            Assert.Equal("Blur", Assert.Single(Changes(h, StudioChangeKind.FilterAdded)).Text);
        }

        [Fact]
        public void AFilterThatWasRenamed_KeepsItsIdAndItsPlace()
        {
            var h = Started(out _, out var mic);
            var before = h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Gate");

            Renamed(h, "Mic", "Gate", "Noise gate");

            var after = h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Noise gate");
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(1, after.Index);
            var change = Assert.Single(Changes(h, StudioChangeKind.FilterRenamed));
            Assert.Equal("Noise gate", change.Text);
            Assert.Equal("Gate", change.OldValue);

            // The read that follows (the rename is a structural event) says the same, and the object is the same.
            h.World.RenameFilter(mic, "Gate", "Noise gate");
            h.SettleWithTime();
            Assert.Equal(before.Id, h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Noise gate").Id);
            Assert.Equal(3, h.Engine.FiltersOf(mic).Count);
        }

        [Fact]
        public void AReadThatWasSentBeforeARename_IsNotApplied_AndDoesNotBringTheOldNameBack()
        {
            var h = Started(out _, out var mic);
            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            var ticket = h.Take();
            Assert.NotNull(ticket);

            // The read went out when the filter had its old name; the event that renames it comes before the answer.
            Renamed(h, "Mic", "Gate", "Noise gate");
            h.Complete(ticket!);

            Assert.Equal(new[] { "Gain", "Noise gate", "Limiter" }, Names(h, mic));
            Assert.False(h.Engine.IsSettled);

            h.World.RenameFilter(mic, "Gate", "Noise gate");
            h.SettleWithTime();
            Assert.Equal(new[] { "Gain", "Noise gate", "Limiter" }, Names(h, mic));
        }

        [Fact]
        public void AReadThatWasSentBeforeARemoval_DoesNotBringTheFilterBack()
        {
            var h = Started(out _, out var mic);
            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            var ticket = h.Take();
            Assert.NotNull(ticket);

            Removed(h, "Mic", "Gain");
            h.Complete(ticket!);

            Assert.Equal(new[] { "Gate", "Limiter" }, Names(h, mic));

            h.World.RemoveFilter(mic, "Gain");
            h.SettleWithTime();
            Assert.Equal(new[] { "Gate", "Limiter" }, Names(h, mic));
        }

        [Fact]
        public void AReadThatWasSentBeforeACreation_DoesNotTakeAwayTheFilterThatItBroughtIn()
        {
            var h = Started(out var gaming, out _);
            h.Engine.RequestRefresh(ReadKind.Filters, gaming, h.Now);
            var ticket = h.Take();
            Assert.NotNull(ticket);

            // The read does not list the new filter (it was sent before it was made): it is not applied, and the next one lists it.
            h.World.AddFilter(gaming, "Blur");
            Created(h, "Gaming", "Blur", 2);
            h.Complete(ticket!, request => (100, new { filters = new[] { new { filterEnabled = true, filterIndex = 0, filterKind = "color_filter_v2", filterName = "Color", filterSettings = new { } } } }));
            Assert.Equal(new[] { "Color", "Sharpen" }, Names(h, gaming));

            h.SettleWithTime();
            Assert.Equal(new[] { "Color", "Sharpen", "Blur" }, Names(h, gaming));
        }

        [Fact]
        public void AReadThatWasSentBeforeAnEnableEvent_DoesNotUndoIt()
        {
            var h = Started(out _, out var mic);
            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            var ticket = h.Take();
            Assert.NotNull(ticket);

            // The read says that the filter is enabled, as it was when it was sent; the event that disables it is newer.
            Enabled(h, "Mic", "Gain", false);
            h.Complete(ticket!);

            Assert.False(h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Gain").Enabled);
        }

        [Fact]
        public void AReadThatIsNewerThanAnEnableEvent_ShowsWhatOBSStudioHolds()
        {
            var h = Started(out _, out var mic);
            Enabled(h, "Mic", "Gain", false);

            // Somebody enabled it again and the model was not told: a read that is sent after the event says so.
            h.World.SetFilterEnabled(mic, "Gain", true);
            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            h.Settle();

            Assert.True(h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Gain").Enabled);
        }

        [Fact]
        public void TheOrderEvent_GivesThePositions_AndTheFiltersAreRead()
        {
            var h = Started(out _, out var mic);

            Reindexed(h, "Mic", "Limiter", "Gain", "Gate");

            Assert.Equal(new[] { "Limiter", "Gain", "Gate" }, Names(h, mic));
            Assert.Equal(new[] { 0, 1, 2 }, h.Engine.FiltersOf(mic).Select(filter => filter.Index).ToArray());
            Assert.Equal(3, Changes(h, StudioChangeKind.FilterIndexChanged).Length);
            Assert.Equal(mic, Assert.Single(NextRead(h)).Target);

            h.World.MoveFilter(mic, "Limiter", 0);
            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            h.SettleWithTime();
            Assert.Equal(new[] { "Limiter", "Gain", "Gate" }, Names(h, mic));
        }

        [Fact]
        public void TheOrderOfTheFilters_IsWhatTheLastReadSays_WhenAnEventDidNotListThem()
        {
            var h = Started(out _, out var mic);
            h.World.MoveFilter(mic, "Limiter", 0);

            // The event of a reorder that was missed: the next read puts them right.
            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            h.Settle();

            Assert.Equal(new[] { "Limiter", "Gain", "Gate" }, Names(h, mic));
            Assert.Equal(new[] { 0, 1, 2 }, h.Engine.FiltersOf(mic).Select(filter => filter.Index).ToArray());
        }

        [Fact]
        public void AFilterThatNoReadListsAnyMore_IsRemoved()
        {
            var h = Started(out _, out var mic);
            h.World.RemoveFilter(mic, "Gate");

            h.Engine.RequestRefresh(ReadKind.Filters, mic, h.Now);
            h.Settle();

            Assert.Equal(new[] { "Gain", "Limiter" }, Names(h, mic));
            Assert.Equal("Gate", Assert.Single(Changes(h, StudioChangeKind.FilterRemoved)).Text);
        }

        [Fact]
        public void AFilterEvent_OfASourceThatTheModelDoesNotHold_IsIgnored()
        {
            var h = Started(out _, out _);

            Enabled(h, "Nobody", "Gain", false);
            Removed(h, "Nobody", "Gain");
            Renamed(h, "Nobody", "Gain", "Other");
            Created(h, "Nobody", "Gain", 0);
            Reindexed(h, "Nobody", "Gain");

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Null(h.Take());
        }

        [Fact]
        public void AFilterEvent_GoesByTheCurrentNameOfTheSource()
        {
            var h = Started(out _, out var mic);
            h.World.Rename(mic, "Microphone");
            h.Event("InputNameChanged", new { inputUuid = mic, oldInputName = "Mic", inputName = "Microphone" }, 8);
            h.Settle();
            h.Engine.TakeChanges();

            Enabled(h, "Mic", "Gain", false);
            Assert.True(h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Gain").Enabled);

            Enabled(h, "Microphone", "Gain", false);
            Assert.False(h.Engine.FiltersOf(mic).Single(filter => filter.Name == "Gain").Enabled);
        }

        [Fact]
        public void WhenASceneAndAnInputHaveTheName_TheEventIsNotApplied_BothAreRead()
        {
            var h = Started(out _, out _, world =>
            {
                var scene = world.AddScene("Shared");
                var input = world.AddInput("Shared", "color_source_v3", 32777);
                world.AddFilter(scene, "Color");
                world.AddFilter(input, "Color");
            });
            var shared = h.Engine.Snapshot().Scenes.Single(scene => scene.Name == "Shared").Uuid;
            var sharedInput = h.Engine.Snapshot().Inputs.Single(input => input.Name == "Shared").Uuid;

            Enabled(h, "Shared", "Color", false);

            Assert.True(h.Engine.FiltersOf(shared).Single().Enabled);
            Assert.True(h.Engine.FiltersOf(sharedInput).Single().Enabled);
            Assert.Equal(new[] { shared, sharedInput }.Order(), NextRead(h).Select(request => request.Target).Order());
        }

        [Theory]
        [InlineData(204)]
        [InlineData(500)]
        [InlineData(702)]
        public void AnOBSStudioThatRefusesToListTheFiltersForGood_IsNotAskedAgain_AndTheModelSettles(int code)
        {
            var h = new EngineHarness();
            var scene = h.World.AddScene("Scene");
            h.World.AddFilter(scene, "Color");
            h.Start();

            h.SettleWithTime(request => request.Kind == ReadKind.Filters ? (code, null) : h.World.Answer(request));

            Assert.True(h.Engine.IsSettled);
            Assert.Empty(h.Engine.FiltersOf(scene));
            Assert.Null(h.Take());
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void ANotReadyAnswer_IsRepeatedAfterAWait()
        {
            var h = new EngineHarness();
            var scene = h.World.AddScene("Scene");
            h.World.AddFilter(scene, "Color");
            h.Start();
            var start = h.Now;
            var refused = 2;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Filters && refused > 0)
                {
                    refused--;
                    return (207, null);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(0, refused);
            Assert.True(h.Now > start, "The read was repeated without waiting.");
            Assert.Equal(new[] { "Color" }, Names(h, scene));
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASourceThatOBSStudioDoesNotKnow_IsAskedAFewTimes_AndThenLeftAlone_SoThatTheModelSettles()
        {
            var h = new EngineHarness();
            var scene = h.World.AddScene("Scene");
            var requests = 0;
            h.Start();

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Filters && request.Target == scene)
                {
                    requests++;
                    return (600, null);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(4, requests);
            Assert.True(h.Engine.IsSettled);
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void AReadThatDidNotComeBack_IsRepeated()
        {
            var h = new EngineHarness();
            var scene = h.World.AddScene("Scene");
            h.World.AddFilter(scene, "Color");
            h.Start();
            var ticket = h.Take();
            Assert.NotNull(ticket);

            h.Engine.OnReadFailed(ticket!, h.Now);
            Assert.False(h.Engine.IsSettled);
            h.SettleWithTime();

            Assert.Equal(new[] { "Color" }, Names(h, scene));
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AfterAReconnect_TheFiltersAreReadAgain_AndTheOnesThatAreStillThereKeepTheirIds()
        {
            var h = Started(out _, out var mic);
            var before = h.Engine.FiltersOf(mic);
            h.End();

            // Changes that were made while there was no connection to hear them.
            h.World.RemoveFilter(mic, "Gain");
            h.World.AddFilter(mic, "Compressor", "compressor_filter");
            h.Start();
            Assert.False(h.Engine.IsSettled);
            h.SettleWithTime();

            var after = h.Engine.FiltersOf(mic);
            Assert.Equal(new[] { "Gate", "Limiter", "Compressor" }, after.Select(filter => filter.Name).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, after.Select(filter => filter.Index).ToArray());
            Assert.Equal(before.Single(filter => filter.Name == "Gate").Id, after.Single(filter => filter.Name == "Gate").Id);
            Assert.DoesNotContain(after, filter => filter.Id == before.Single(candidate => candidate.Name == "Gain").Id);
            var changes = h.Engine.TakeChanges();
            Assert.Contains(changes, change => change.Kind == StudioChangeKind.FilterRemoved && change.Text == "Gain");
            Assert.Contains(changes, change => change.Kind == StudioChangeKind.FilterAdded && change.Text == "Compressor");
        }

        [Fact]
        public void TheFiltersOfANewInput_AreRead()
        {
            var h = Started(out _, out _);
            var input = h.World.AddInput("Overlay", "browser_source", 8);
            h.World.AddFilter(input, "Chroma key", "chroma_key_filter_v2");

            h.Event("InputCreated", new { inputName = "Overlay", inputUuid = input, inputKind = "browser_source", unversionedInputKind = "browser_source", inputSettings = new { }, defaultInputSettings = new { } }, 8);
            h.SettleWithTime();

            Assert.Equal(new[] { "Chroma key" }, Names(h, input));
        }

        [Fact]
        public void TheFiltersOfAnInputThatWasRemoved_GoWithIt()
        {
            var h = Started(out _, out _);
            var camera = h.Engine.Snapshot().Inputs.Single(input => input.Name == "Camera").Uuid;
            h.World.AddFilter(camera, "Blur");
            h.Engine.RequestRefresh(ReadKind.Filters, camera, h.Now);
            h.Settle();
            Assert.Equal(new[] { "Blur" }, Names(h, camera));

            h.World.Inputs.RemoveAll(input => input.Uuid == camera);
            h.Event("InputRemoved", new { inputName = "Camera", inputUuid = camera }, 8);
            h.SettleWithTime();

            Assert.Empty(h.Engine.FiltersOf(camera));
            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == camera);
        }

        [Fact]
        public void WhenOBSStudioChangesItsSceneCollection_TheFiltersOfTheNewOnesAreRead()
        {
            var h = Started(out var gaming, out _);
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Other" }, 2);

            // The new collection has other scenes, with filters of their own.
            h.World.Scenes.Clear();
            h.World.Items.Clear();
            h.World.Inputs.Clear();
            h.World.Filters.Clear();
            h.World.Program = null;
            var fresh = h.World.AddScene("Fresh");
            h.World.AddFilter(fresh, "Mask", "mask_filter_v2");
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Other" }, 2);
            h.SettleWithTime();

            Assert.Empty(h.Engine.FiltersOf(gaming));
            Assert.Equal(new[] { "Mask" }, Names(h, fresh));
            Assert.Empty(h.Engine.CheckConsistency());
        }

        [Fact]
        public void AFilterEvent_ThatComesWhileOBSStudioChangesItsSceneCollection_IsNotTaken()
        {
            var h = Started(out _, out var mic);
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Other" }, 2);

            Enabled(h, "Mic", "Gain", false);
            Removed(h, "Mic", "Gate");

            Assert.Equal(new[] { "Gain", "Gate", "Limiter" }, Names(h, mic));
            Assert.True(h.Engine.FiltersOf(mic).First().Enabled);
        }

        [Fact]
        public void TheConsistencyCheck_AcceptsTheFilters()
        {
            var h = Started(out _, out _);

            Assert.Empty(h.Engine.CheckConsistency());
        }
    }
}
