namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The filters as the application sees them (<see cref="ObsSource.Filters"/>): what they show, what they tell when it changes, and the changes that enable, rename, move, remove and make
    /// them, against a stand-in for OBS Studio that answers and sends what a test tells it to.
    /// </summary>
    public class ModelFilterTests
    {
        private const int FilterEvents = 32;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsModelOptions Options(bool ignoreCase = false)
        {
            return new ObsModelOptions
            {
                IgnoreCase = ignoreCase,
                ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromMilliseconds(600),
            };
        }

        // A scene "Gaming" that shows the input "Mic" and has the filter "Color", the input "Mic" with "Gain", "Gate" (disabled) and "Limiter", an input "Camera" that nothing shows, and a scene "Scene".
        private static async Task<ModelRig> StartAsync(ObsModelOptions? options = null)
        {
            return await ModelRig.ConnectedAsync(
                world =>
                {
                    var gaming = world.AddScene("Gaming");
                    var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    world.AddInput("Camera", "dshow_input", 8);
                    world.AddItem(gaming, mic, "Mic");
                    world.AddFilter(gaming, "Color", "color_filter_v2");
                    world.AddFilter(mic, "Gain", "gain_filter");
                    world.AddFilter(mic, "Gate", "noise_gate_filter", enabled: false);
                    world.AddFilter(mic, "Limiter", "limiter_filter");
                },
                options ?? Options());
        }

        private static Task EnabledAsync(ModelRig rig, string source, string filter, bool enabled)
        {
            return rig.Responder.SendEventAsync("SourceFilterEnableStateChanged", new { sourceName = source, filterName = filter, filterEnabled = enabled }, FilterEvents);
        }

        private static Task CreatedAsync(ModelRig rig, string source, string filter, int index)
        {
            return rig.Responder.SendEventAsync("SourceFilterCreated", new { sourceName = source, filterName = filter, filterKind = "color_filter_v2", filterIndex = index, filterSettings = new { }, defaultFilterSettings = new { } }, FilterEvents);
        }

        private static Task RemovedAsync(ModelRig rig, string source, string filter)
        {
            return rig.Responder.SendEventAsync("SourceFilterRemoved", new { sourceName = source, filterName = filter }, FilterEvents);
        }

        private static Task RenamedAsync(ModelRig rig, string source, string oldName, string newName)
        {
            return rig.Responder.SendEventAsync("SourceFilterNameChanged", new { sourceName = source, oldFilterName = oldName, filterName = newName }, FilterEvents);
        }

        private static Task ReindexedAsync(ModelRig rig, string source, params string[] order)
        {
            var filters = order.Select((name, index) => new { filterEnabled = true, filterIndex = index, filterKind = "color_filter_v2", filterName = name, filterSettings = new { } }).ToArray();
            return rig.Responder.SendEventAsync("SourceFilterListReindexed", new { sourceName = source, filters }, FilterEvents);
        }

        // OBS Studio answers first, and what it did is told a moment after, as the events come.
        private static void Later(Func<Task> events)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                await events();
            });
        }

        private static string[] Names(ObsSource source)
        {
            return source.Filters.Select(filter => filter.Name).ToArray();
        }

        private static int Count(ModelRig rig, string type)
        {
            return rig.Responder.RequestLog.Count(requested => requested == type);
        }

        private static string Text(JsonElement? data, string property)
        {
            return data!.Value.GetProperty(property).GetString()!;
        }

        [Fact]
        public async Task TheFiltersOfEverySceneAndInput_AreInTheModel_ByNameAndPosition()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];

            Assert.Equal(new[] { "Gain", "Gate", "Limiter" }, Names(mic));
            Assert.Equal(new[] { 0, 1, 2 }, mic.Filters.Select(filter => filter.Index).ToArray());
            Assert.Equal(new[] { "gain_filter", "noise_gate_filter", "limiter_filter" }, mic.Filters.Select(filter => filter.Kind).ToArray());
            Assert.Equal(new[] { true, false, true }, mic.Filters.Select(filter => filter.IsEnabled).ToArray());
            Assert.All(mic.Filters, filter => Assert.Same(mic, filter.Source));
            Assert.All(mic.Filters, filter => Assert.False(filter.IsRemoved));
            Assert.Equal(new[] { "Color" }, Names(rig.Model.Scenes["Gaming"]));
            Assert.Empty(rig.Model.Scenes["Scene"].Filters);
            Assert.Empty(rig.Model.Inputs["Camera"].Filters);
            Assert.Equal("Filter 'Gate' of input 'Mic'", mic.Filters["Gate"].ToString());
            Assert.Same(mic.Filters, mic.Filters);
        }

        [Fact]
        public async Task AFilter_IsFoundByName_AndAWrongNameSaysWhichAreClose()
        {
            await using var rig = await StartAsync();
            var filters = rig.Model.Inputs["Mic"].Filters;

            Assert.Same(filters[1], filters["Gate"]);
            Assert.True(filters.TryGet("Gain", out var gain));
            Assert.Same(filters[0], gain);
            Assert.Same(filters[2], filters.Find("Limiter"));
            Assert.Null(filters.Find("Nope"));
            Assert.False(filters.TryGet("Nope", out _));
            Assert.True(filters.Contains("Gate"));
            Assert.False(filters.Contains("gate"));
            var exception = Assert.Throws<ObsNotFoundException>(() => filters["Gian"]);
            Assert.Equal("filter", exception.Kind);
            Assert.Contains("Gain", exception.Suggestions);
            Assert.Contains("input 'Mic'", exception.Message);
        }

        [Fact]
        public async Task WithIgnoreCase_AFilterIsFoundWhateverTheCase()
        {
            await using var rig = await StartAsync(Options(ignoreCase: true));
            var filters = rig.Model.Inputs["mic"].Filters;

            Assert.Same(filters[0], filters["GAIN"]);
            Assert.True(filters.Contains("limiter"));
        }

        [Fact]
        public async Task TwoFiltersThatDifferOnlyByCase_AreAmbiguousWhenCaseIsIgnored_AndTheMessageSaysToUseTheExactName()
        {
            await using var rig = await StartAsync(Options(ignoreCase: true));
            var mic = rig.Model.Inputs["mic"];
            rig.Responder.Change(world => world.AddFilter(mic.Uuid, "GAIN", "gain_filter"));
            await CreatedAsync(rig, "Mic", "GAIN", 3);
            await ModelRig.EventuallyAsync(() => mic.Filters.Count == 4, "the second filter to be added");

            var exception = Assert.Throws<ObsAmbiguousNameException>(() => mic.Filters["gain"]);

            // Filters have no id and no FindAll: the way out is the exact name, which always wins.
            Assert.Equal("filter", exception.Kind);
            Assert.Equal(2, exception.Candidates.Count);
            Assert.Contains("exactly as it is written", exception.Message);
            Assert.DoesNotContain("FindAll", exception.Message);
            Assert.Same(mic.Filters[0], mic.Filters["Gain"]);
        }

        [Fact]
        public async Task AFilterThatIsEnabledInOBSStudio_IsShown_AndTold()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            var properties = new List<string>();
            gate.PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                {
                    lock (properties)
                    {
                        properties.Add(e.PropertyName);
                    }
                }
            };
            rig.Responder.Change(world => world.SetFilterEnabled(mic.Uuid, "Gate", true));

            await EnabledAsync(rig, "Mic", "Gate", true);

            await ModelRig.EventuallyAsync(() => gate.IsEnabled, "the filter to be enabled");
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => ReferenceEquals(change.Entity, gate) && change.PropertyName == nameof(ObsFilter.IsEnabled)), "the record of the change");
            var record = rig.Changes.Single(change => ReferenceEquals(change.Entity, gate) && change.PropertyName == nameof(ObsFilter.IsEnabled));
            Assert.Equal(ObsChangeKind.PropertyChanged, record.Kind);
            Assert.Equal(false, record.OldValue);
            Assert.Equal(true, record.NewValue);
            Assert.Equal("filter 'Gate' of input 'Mic': IsEnabled False -> True", record.ToString());
            await ModelRig.EventuallyAsync(() => { lock (properties) { return properties.Contains(nameof(ObsFilter.IsEnabled)); } }, "the notification of the filter");
            Assert.Same(gate, mic.Filters["Gate"]);
        }

        [Fact]
        public async Task AFilterThatIsMadeInOBSStudio_IsAdded_OnTopOfTheOthers()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var before = mic.Filters;
            var properties = new List<string>();
            mic.PropertyChanged += (_, e) =>
            {
                lock (properties)
                {
                    properties.Add(e.PropertyName ?? string.Empty);
                }
            };
            rig.Responder.Change(world => world.AddFilter(mic.Uuid, "Blur", "obs_blur", enabled: false));

            await CreatedAsync(rig, "Mic", "Blur", 3);

            await ModelRig.EventuallyAsync(() => mic.Filters.Contains("Blur"), "the filter to be added");
            var blur = mic.Filters["Blur"];
            Assert.Equal(3, blur.Index);
            Assert.False(blur.IsEnabled);
            Assert.Equal("obs_blur", blur.Kind);
            Assert.Equal(new[] { "Gain", "Gate", "Limiter", "Blur" }, Names(mic));
            Assert.Equal(3, before.Count);
            Assert.NotSame(before, mic.Filters);
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => change.Kind == ObsChangeKind.Added && ReferenceEquals(change.Entity, blur)), "the record that the filter was added");
            Assert.Equal("Added: filter 'Blur' of input 'Mic'", rig.Changes.Single(change => change.Kind == ObsChangeKind.Added && ReferenceEquals(change.Entity, blur)).ToString());
            await ModelRig.EventuallyAsync(() => { lock (properties) { return properties.Contains(nameof(ObsSource.Filters)); } }, "the notification of the source");
        }

        [Fact]
        public async Task AFilterThatIsRemovedInOBSStudio_IsGone_AndItsObjectSaysSo()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gain = mic.Filters["Gain"];
            var gate = mic.Filters["Gate"];
            rig.Responder.Change(world => world.RemoveFilter(mic.Uuid, "Gain"));

            await RemovedAsync(rig, "Mic", "Gain");

            await ModelRig.EventuallyAsync(() => gain.IsRemoved, "the filter to be removed");
            Assert.Equal(new[] { "Gate", "Limiter" }, Names(mic));
            await ModelRig.EventuallyAsync(() => gate.Index == 0, "the filters behind it to move up");
            Assert.False(gate.IsRemoved);
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => change.Kind == ObsChangeKind.Removed && ReferenceEquals(change.Entity, gain)), "the record that the filter was removed");
            Assert.Equal("Removed: filter 'Gain' of input 'Mic'", rig.Changes.Single(change => change.Kind == ObsChangeKind.Removed && ReferenceEquals(change.Entity, gain)).ToString());
        }

        [Fact]
        public async Task AFilterThatIsRenamedInOBSStudio_IsTheSameObject_WithTheNewName()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            rig.Responder.Change(world => world.RenameFilter(mic.Uuid, "Gate", "Noise gate"));

            await RenamedAsync(rig, "Mic", "Gate", "Noise gate");

            await ModelRig.EventuallyAsync(() => gate.Name == "Noise gate", "the filter to be renamed");
            Assert.Same(gate, mic.Filters["Noise gate"]);
            Assert.False(mic.Filters.Contains("Gate"));
            Assert.Equal(1, gate.Index);
            Assert.False(gate.IsRemoved);
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => ReferenceEquals(change.Entity, gate) && change.PropertyName == nameof(ObsFilter.Name)), "the record of the rename");
            var record = rig.Changes.Single(change => ReferenceEquals(change.Entity, gate) && change.PropertyName == nameof(ObsFilter.Name));
            Assert.Equal("Gate", record.OldValue);
            Assert.Equal("Noise gate", record.NewValue);
        }

        [Fact]
        public async Task TheFilters_AreMovedInOBSStudio_AndTheCollectionSaysWhichOrder()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var limiter = mic.Filters["Limiter"];
            var before = mic.Filters;
            rig.Responder.Change(world => world.MoveFilter(mic.Uuid, "Limiter", 0));

            await ReindexedAsync(rig, "Mic", "Limiter", "Gain", "Gate");

            await ModelRig.EventuallyAsync(() => limiter.Index == 0, "the filter to move");
            await ModelRig.EventuallyAsync(() => Names(mic).SequenceEqual(["Limiter", "Gain", "Gate"]), "the collection to be in the new order");
            Assert.NotSame(before, mic.Filters);
            Assert.Equal(new[] { 0, 1, 2 }, mic.Filters.Select(filter => filter.Index).ToArray());
            Assert.Same(limiter, mic.Filters[0]);
        }

        [Fact]
        public async Task AnOldCollection_IsNotChangedWhileYouLookAtIt()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var snapshot = mic.Filters;
            rig.Responder.Change(world => world.RemoveFilter(mic.Uuid, "Gain"));

            await RemovedAsync(rig, "Mic", "Gain");

            await ModelRig.EventuallyAsync(() => mic.Filters.Count == 2, "the filter to be removed");
            Assert.Equal(3, snapshot.Count);
            Assert.Equal(new[] { "Gain", "Gate", "Limiter" }, snapshot.Select(filter => filter.Name).ToArray());
        }

        [Fact]
        public async Task TheFiltersOfAnInputThatOBSStudioDestroyed_AreRemovedWithIt()
        {
            await using var rig = await StartAsync();
            var camera = rig.Model.Inputs["Camera"];
            rig.Responder.Change(world => world.AddFilter(camera.Uuid, "Blur", "obs_blur"));
            await CreatedAsync(rig, "Camera", "Blur", 0);
            await ModelRig.EventuallyAsync(() => camera.Filters.Contains("Blur"), "the filter to be added");
            var blur = camera.Filters["Blur"];

            rig.Responder.Change(world => world.Inputs.RemoveAll(input => input.Uuid == camera.Uuid));
            await rig.Responder.SendEventAsync("InputRemoved", new { inputName = "Camera", inputUuid = camera.Uuid }, 8);

            await ModelRig.EventuallyAsync(() => camera.State == ObsObjectState.Gone, "the input to be gone");
            await ModelRig.EventuallyAsync(() => blur.IsRemoved, "the filter to be removed with it");
            Assert.Empty(camera.Filters);
        }

        [Fact]
        public async Task AFilterOfAnInputThatOBSStudioRemovedAndKeepsAlive_StaysWithIt()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gain = mic.Filters["Gain"];

            // A scene item of the scene "Gaming" shows the input, so OBS Studio keeps it alive.
            await rig.Responder.SendEventAsync("InputRemoved", new { inputName = "Mic", inputUuid = mic.Uuid }, 8);

            await ModelRig.EventuallyAsync(() => mic.State == ObsObjectState.Detached, "the input to be detached");
            Assert.Equal(new[] { "Gain", "Gate", "Limiter" }, Names(mic));
            Assert.False(gain.IsRemoved);
        }

        [Fact]
        public async Task TheFilters_AreReadAgain_WhenTheConnectionIsRestored()
        {
            await using var rig = await ModelRig.ConnectedAsync(
                world =>
                {
                    var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    world.AddFilter(mic, "Gain", "gain_filter");
                    world.AddFilter(mic, "Gate", "noise_gate_filter");
                },
                new ObsModelOptions { AutoReconnect = true, ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromMilliseconds(600) });
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];

            // A filter is taken away and another one is made while nobody is listening, and the model reconnects by itself.
            rig.Responder.Change(world =>
            {
                world.RemoveFilter(mic.Uuid, "Gain");
                world.AddFilter(mic.Uuid, "Compressor", "compressor_filter");
            });
            rig.Responder.AbortConnection();

            await ModelRig.EventuallyAsync(() => mic.Filters.Contains("Compressor") && !mic.Filters.Contains("Gain"), "the filters to be read again", Timeout);
            Assert.Same(gate, mic.Filters["Gate"]);
            Assert.False(gate.IsRemoved);
            Assert.Equal(new[] { 0, 1 }, mic.Filters.Select(filter => filter.Index).ToArray());
        }

        [Fact]
        public async Task AFilterThatWasMadeAgainWithAnotherKind_WhileThereWasNoConnection_IsAnotherObject_AndTheOldOneIsRemoved()
        {
            await using var rig = await ModelRig.ConnectedAsync(
                world =>
                {
                    var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    world.AddFilter(mic, "Gate", "noise_gate_filter");
                },
                new ObsModelOptions { AutoReconnect = true, ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromMilliseconds(600) });
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];

            // The filter is removed and another one with the same name is made while nobody is listening, and the model reconnects by itself.
            rig.Responder.Change(world =>
            {
                world.RemoveFilter(mic.Uuid, "Gate");
                world.AddFilter(mic.Uuid, "Gate", "compressor_filter");
            });
            rig.Responder.AbortConnection();

            await ModelRig.EventuallyAsync(() => gate.IsRemoved && mic.Filters.Find("Gate") is { } made && !ReferenceEquals(made, gate), "the old filter to be removed and the new one to be there", Timeout);
            var again = mic.Filters["Gate"];
            Assert.NotSame(gate, again);
            Assert.Equal("compressor_filter", again.Kind);
            Assert.Equal("noise_gate_filter", gate.Kind);
            Assert.False(again.IsRemoved);
        }

        [Fact]
        public async Task EnablingAFilter_ReturnsWhenTheModelShowsIt()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            JsonElement? sent = null;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSourceFilterEnabled")
                {
                    return null;
                }

                sent = data!.Value.Clone();
                rig.Responder.Change(world => world.SetFilterEnabled(mic.Uuid, "Gate", true));
                Later(() => EnabledAsync(rig, "Mic", "Gate", true));
                return (100, null);
            };
            var reads = Count(rig, "GetSourceFilterList");

            await gate.SetEnabledAsync(true).WithTimeoutAsync(Timeout);

            Assert.True(gate.IsEnabled);
            Assert.Equal(1, Count(rig, "SetSourceFilterEnabled"));
            Assert.Equal(mic.Uuid.ToString(), Text(sent, "sourceUuid"), ignoreCase: true);
            Assert.Equal("Gate", Text(sent, "filterName"));
            Assert.True(sent!.Value.GetProperty("filterEnabled").GetBoolean());
            Assert.Equal(reads, Count(rig, "GetSourceFilterList"));
        }

        [Fact]
        public async Task AnEnable_WhoseEventDoesNotCome_IsShownByARead()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSourceFilterEnabled")
                {
                    return null;
                }

                rig.Responder.Change(world => world.SetFilterEnabled(mic.Uuid, "Gate", true));
                return (100, null);
            };
            var reads = Count(rig, "GetSourceFilterList");

            await gate.SetEnabledAsync(true).WithTimeoutAsync(Timeout);

            Assert.True(gate.IsEnabled);
            Assert.True(Count(rig, "GetSourceFilterList") > reads);
        }

        [Fact]
        public async Task AChangeThatOBSStudioAnsweredAndDidNotMake_EndsWithWhatOBSStudioHolds()
        {
            await using var rig = await StartAsync();
            var gate = rig.Model.Inputs["Mic"].Filters["Gate"];
            rig.Responder.OnRequest = (type, data) => type == "SetSourceFilterEnabled" ? (100, null) : null;

            await gate.SetEnabledAsync(true).WithTimeoutAsync(Timeout);

            Assert.False(gate.IsEnabled);
        }

        [Fact]
        public async Task TogglingAFilter_AsksOBSStudio_AndSetsTheOpposite_BothWays()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gain = mic.Filters["Gain"];
            var enabled = true;
            var sent = new List<bool>();
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "GetSourceFilter")
                {
                    return (100, new { filterEnabled = enabled, filterIndex = 0, filterKind = "gain_filter", filterSettings = new { } });
                }

                if (type != "SetSourceFilterEnabled")
                {
                    return null;
                }

                enabled = data!.Value.GetProperty("filterEnabled").GetBoolean();
                sent.Add(enabled);
                rig.Responder.Change(world => world.SetFilterEnabled(mic.Uuid, "Gain", enabled));
                Later(() => EnabledAsync(rig, "Mic", "Gain", enabled));
                return (100, null);
            };

            var first = await gain.ToggleEnabledAsync().WithTimeoutAsync(Timeout);
            Assert.False(first);
            Assert.False(gain.IsEnabled);

            var second = await gain.ToggleEnabledAsync().WithTimeoutAsync(Timeout);
            Assert.True(second);
            Assert.True(gain.IsEnabled);
            Assert.Equal(new[] { false, true }, sent);
            Assert.Equal(2, Count(rig, "GetSourceFilter"));
        }

        [Fact]
        public async Task AToggle_ThatOBSStudioDoesNotAnswerTheQuestionOf_ChangesNothing()
        {
            await using var rig = await StartAsync();
            var gain = rig.Model.Inputs["Mic"].Filters["Gain"];
            rig.Responder.OnRequest = (type, data) => type == "GetSourceFilter" ? (0, null) : null;

            await Assert.ThrowsAsync<ObsNotReadyException>(() => gain.ToggleEnabledAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(0, Count(rig, "SetSourceFilterEnabled"));
            Assert.True(gain.IsEnabled);
        }

        [Fact]
        public async Task AChangeThatOBSStudioRefuses_IsAResponseException_WithItsCode()
        {
            await using var rig = await StartAsync();
            var gain = rig.Model.Inputs["Mic"].Filters["Gain"];
            rig.Responder.OnRequest = (type, data) => type == "SetSourceFilterEnabled" ? (604, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => gain.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidResourceState, exception.ErrorCode);
            Assert.True(gain.IsEnabled);
        }

        [Fact]
        public async Task AFilterThatOBSStudioDoesNotKnow_IsARemovedException_AndTheModelLetsGoOfIt()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];

            // Somebody removed it, and the event was lost: OBS Studio says 600, and the model is read again.
            rig.Responder.Change(world => world.RemoveFilter(mic.Uuid, "Gate"));
            rig.Responder.OnRequest = (type, data) => type == "SetSourceFilterEnabled" ? (600, null) : null;

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => gate.SetEnabledAsync(true).WithTimeoutAsync(Timeout));

            Assert.Contains("filter 'Gate' of input 'Mic'", exception.Description);
            Assert.True(gate.IsRemoved);
            Assert.Equal(new[] { "Gain", "Limiter" }, Names(mic));
        }

        [Fact]
        public async Task AChangeOfAFilterThatWasRemoved_IsARemovedException_AndNothingIsSent()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gain = mic.Filters["Gain"];
            rig.Responder.Change(world => world.RemoveFilter(mic.Uuid, "Gain"));
            await RemovedAsync(rig, "Mic", "Gain");
            await ModelRig.EventuallyAsync(() => gain.IsRemoved, "the filter to be removed");

            await Assert.ThrowsAsync<ObsRemovedException>(() => gain.SetEnabledAsync(false).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => gain.ToggleEnabledAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => gain.RenameAsync("Other").WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => gain.SetIndexAsync(0).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => gain.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(0, Count(rig, "SetSourceFilterEnabled") + Count(rig, "SetSourceFilterName") + Count(rig, "SetSourceFilterIndex") + Count(rig, "RemoveSourceFilter"));
        }

        [Fact]
        public async Task ARename_ReturnsWhenTheModelShowsTheNewName_AndTheObjectIsTheSame()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            JsonElement? sent = null;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSourceFilterName")
                {
                    return null;
                }

                sent = data!.Value.Clone();
                rig.Responder.Change(world => world.RenameFilter(mic.Uuid, "Gate", "Noise gate"));
                Later(() => RenamedAsync(rig, "Mic", "Gate", "Noise gate"));
                return (100, null);
            };

            await gate.RenameAsync("Noise gate").WithTimeoutAsync(Timeout);

            Assert.Equal("Noise gate", gate.Name);
            Assert.Same(gate, mic.Filters["Noise gate"]);
            Assert.Equal("Gate", Text(sent, "filterName"));
            Assert.Equal("Noise gate", Text(sent, "newFilterName"));
        }

        [Fact]
        public async Task AChange_ThatIsAskedForRightAfterARename_GoesToTheFilterByItsNewName()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            var enabledName = string.Empty;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "SetSourceFilterName")
                {
                    rig.Responder.Change(world => world.RenameFilter(mic.Uuid, "Gate", "Noise gate"));
                    Later(() => RenamedAsync(rig, "Mic", "Gate", "Noise gate"));
                    return (100, null);
                }

                if (type == "SetSourceFilterEnabled")
                {
                    enabledName = Text(data, "filterName");
                    rig.Responder.Change(world => world.SetFilterEnabled(mic.Uuid, "Noise gate", true));
                    Later(() => EnabledAsync(rig, "Mic", "Noise gate", true));
                    return (100, null);
                }

                return null;
            };

            var rename = gate.RenameAsync("Noise gate");
            var enable = gate.SetEnabledAsync(true);
            await rename.WithTimeoutAsync(Timeout);
            await enable.WithTimeoutAsync(Timeout);

            Assert.Equal("Noise gate", enabledName);
            Assert.True(gate.IsEnabled);
        }

        [Fact]
        public async Task ARename_WhoseEventComesBeforeItsAnswer_AndWhoseCallWasGivenUp_LeavesNoNameBehind()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            using var giveUp = new CancellationTokenSource();
            var enabledNames = new List<string>();
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "SetSourceFilterName")
                {
                    // OBS Studio makes the change and tells it, and then it answers: the model shows the new name before the answer is handled, and the caller has given up by then.
                    rig.Responder.Change(world => world.RenameFilter(mic.Uuid, "Gate", "Noise gate"));
                    RenamedAsync(rig, "Mic", "Gate", "Noise gate").GetAwaiter().GetResult();
                    SpinWait.SpinUntil(() => gate.Name == "Noise gate", Timeout);
                    giveUp.Cancel();
                    return (100, null);
                }

                if (type == "SetSourceFilterEnabled")
                {
                    var source = Text(data, "filterName");
                    var enabled = data!.Value.GetProperty("filterEnabled").GetBoolean();
                    lock (enabledNames)
                    {
                        enabledNames.Add(source);
                    }

                    rig.Responder.Change(world => world.SetFilterEnabled(mic.Uuid, source, enabled));
                    Later(() => EnabledAsync(rig, "Mic", source, enabled));
                    return (100, null);
                }

                return null;
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.RenameAsync("Noise gate", giveUp.Token).WithTimeoutAsync(Timeout));

            // A change of another filter has its turn when the answer to the rename has been handled.
            await mic.Filters["Gain"].SetEnabledAsync(false).WithTimeoutAsync(Timeout);

            // Somebody else renames the filter in OBS Studio: what is sent for it from now on has to use that name, and not the one that the rename of the application gave it.
            rig.Responder.Change(world => world.RenameFilter(mic.Uuid, "Noise gate", "Gate again"));
            await RenamedAsync(rig, "Mic", "Noise gate", "Gate again");
            await ModelRig.EventuallyAsync(() => gate.Name == "Gate again", "the model to show the other rename");
            await gate.SetEnabledAsync(true).WithTimeoutAsync(Timeout);

            lock (enabledNames)
            {
                Assert.Equal(new[] { "Gain", "Gate again" }, enabledNames.ToArray());
            }
        }

        [Fact]
        public async Task AChange_AskedForRightAfterARename_IsNotBuiltBeforeTheModelShowsTheNewName()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            var renameIsTold = false;
            var whenAsked = string.Empty;
            var askedFor = string.Empty;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "SetSourceFilterName")
                {
                    rig.Responder.Change(world => world.RenameFilter(mic.Uuid, "Gate", "Noise gate"));

                    // OBS Studio answers first, and what it did is told a moment after.
                    Later(() =>
                    {
                        Volatile.Write(ref renameIsTold, true);
                        return RenamedAsync(rig, "Mic", "Gate", "Noise gate");
                    });
                    return (100, null);
                }

                if (type == "SetSourceFilterEnabled")
                {
                    askedFor = Text(data, "filterName");
                    whenAsked = Volatile.Read(ref renameIsTold) ? "after the model was told" : "before the model was told";
                    rig.Responder.Change(world => world.SetFilterEnabled(mic.Uuid, "Noise gate", true));
                    Later(() => EnabledAsync(rig, "Mic", "Noise gate", true));
                    return (100, null);
                }

                return null;
            };

            var rename = gate.RenameAsync("Noise gate");
            var enable = gate.SetEnabledAsync(true);
            await rename.WithTimeoutAsync(Timeout);
            await enable.WithTimeoutAsync(Timeout);

            // A filter has no id: the change that follows a rename is built from the name that the model shows, so it waits until the model has been told, and no name is kept that could go stale.
            Assert.Equal("after the model was told", whenAsked);
            Assert.Equal("Noise gate", askedFor);
        }

        [Fact]
        public async Task ARenameToANameThatIsTaken_IsANameInUseException_AndNothingIsChanged()
        {
            await using var rig = await StartAsync();
            var gate = rig.Model.Inputs["Mic"].Filters["Gate"];
            rig.Responder.OnRequest = (type, data) => type == "SetSourceFilterName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => gate.RenameAsync("Gain").WithTimeoutAsync(Timeout));

            Assert.Equal("Gain", exception.Name);
            Assert.Contains("input 'Mic' has a filter named 'Gain' already", exception.Message);
            Assert.Null(exception.HeldBy);
            Assert.Equal("Gate", gate.Name);
        }

        [Fact]
        public async Task AFilterIsMoved_AndTheCallReturnsWhenTheModelShowsTheNewPosition()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var limiter = mic.Filters["Limiter"];
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSourceFilterIndex")
                {
                    return null;
                }

                Assert.Equal(0, data!.Value.GetProperty("filterIndex").GetInt32());
                rig.Responder.Change(world => world.MoveFilter(mic.Uuid, "Limiter", 0));
                Later(() => ReindexedAsync(rig, "Mic", "Limiter", "Gain", "Gate"));
                return (100, null);
            };

            await limiter.SetIndexAsync(0).WithTimeoutAsync(Timeout);

            Assert.Equal(0, limiter.Index);
            Assert.Equal(new[] { "Limiter", "Gain", "Gate" }, Names(mic));
        }

        [Fact]
        public async Task APositionThatIsNotOneOfTheSource_IsAnArgumentOutOfRangeException_AndNothingIsSent()
        {
            await using var rig = await StartAsync();
            var gain = rig.Model.Inputs["Mic"].Filters["Gain"];

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = gain.SetIndexAsync(-1); });
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => gain.SetIndexAsync(3).WithTimeoutAsync(Timeout));

            Assert.Contains("has 3 filters", exception.Message);
            Assert.Equal(0, Count(rig, "SetSourceFilterIndex"));
        }

        [Fact]
        public async Task AFilterIsRemoved_AndTheCallReturnsWhenTheModelShowsIt()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gate = mic.Filters["Gate"];
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "RemoveSourceFilter")
                {
                    return null;
                }

                rig.Responder.Change(world => world.RemoveFilter(mic.Uuid, "Gate"));
                Later(() => RemovedAsync(rig, "Mic", "Gate"));
                return (100, null);
            };

            await gate.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.True(gate.IsRemoved);
            Assert.Equal(new[] { "Gain", "Limiter" }, Names(mic));
            Assert.Equal(new[] { 0, 1 }, mic.Filters.Select(filter => filter.Index).ToArray());
        }

        [Fact]
        public async Task AFilterIsMade_AndTheCallReturnsItWhenTheModelShowsIt()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            JsonElement? sent = null;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateSourceFilter")
                {
                    return null;
                }

                sent = data!.Value.Clone();
                rig.Responder.Change(world => world.AddFilter(mic.Uuid, "Blur", "obs_blur"));
                Later(() => CreatedAsync(rig, "Mic", "Blur", 3));
                return (100, null);
            };

            var blur = await mic.CreateFilterAsync("Blur", "obs_blur", new { size = 5 }).WithTimeoutAsync(Timeout);

            Assert.Same(blur, mic.Filters["Blur"]);
            Assert.Equal(3, blur.Index);
            Assert.True(blur.IsEnabled);
            Assert.Equal("obs_blur", blur.Kind);
            Assert.Equal(mic.Uuid.ToString(), Text(sent, "sourceUuid"), ignoreCase: true);
            Assert.Equal("Blur", Text(sent, "filterName"));
            Assert.Equal("obs_blur", Text(sent, "filterKind"));
            Assert.Equal(5, sent!.Value.GetProperty("filterSettings").GetProperty("size").GetInt32());
        }

        [Fact]
        public async Task AFilterIsMadeInAScene_WithoutSettings()
        {
            await using var rig = await StartAsync();
            var gaming = rig.Model.Scenes["Gaming"];
            JsonElement? sent = null;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateSourceFilter")
                {
                    return null;
                }

                sent = data!.Value.Clone();
                rig.Responder.Change(world => world.AddFilter(gaming.Uuid, "Sharpen", "sharpness_filter_v2"));
                Later(() => CreatedAsync(rig, "Gaming", "Sharpen", 1));
                return (100, null);
            };

            var sharpen = await gaming.CreateFilterAsync("Sharpen", "sharpness_filter_v2").WithTimeoutAsync(Timeout);

            Assert.Equal(new[] { "Color", "Sharpen" }, Names(gaming));
            Assert.Same(sharpen, gaming.Filters[1]);
            Assert.False(sent!.Value.TryGetProperty("filterSettings", out _));
        }

        [Fact]
        public async Task AFilterWithANameThatIsTaken_IsANameInUseException_AndNothingIsChanged()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            rig.Responder.OnRequest = (type, data) => type == "CreateSourceFilter" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => mic.CreateFilterAsync("Gain", "gain_filter").WithTimeoutAsync(Timeout));

            Assert.Equal("Gain", exception.Name);
            Assert.Contains("creating filter 'Gain' on input 'Mic'", exception.Message);
            Assert.Equal(3, mic.Filters.Count);
        }

        [Fact]
        public async Task AFilterOfAKindThatOBSStudioDoesNotKnow_IsAResponseException()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            rig.Responder.OnRequest = (type, data) => type == "CreateSourceFilter" ? (607, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => mic.CreateFilterAsync("Odd", "no_such_filter").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidFilterKind, exception.ErrorCode);
            Assert.False(mic.Filters.Contains("Odd"));
        }

        [Fact]
        public async Task TheArgumentsOfAFilterThatIsMade_AreChecked_BeforeAnythingIsSent()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];

            Assert.Throws<ArgumentException>(() => { _ = mic.CreateFilterAsync(string.Empty, "gain_filter"); });
            Assert.Throws<ArgumentException>(() => { _ = mic.CreateFilterAsync("Name", string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = mic.CreateFilterAsync("Name", "gain_filter", "not an object"); });
            Assert.Throws<ArgumentException>(() => { _ = mic.CreateFilterAsync("Name", "gain_filter", CancellationToken.None); });
            Assert.Throws<ArgumentException>(() => { _ = mic.Filters["Gate"].RenameAsync(string.Empty); });

            Assert.Equal(0, Count(rig, "CreateSourceFilter"));
        }

        [Fact]
        public async Task AFilterOfASourceThatWasDestroyed_CanNotBeMade()
        {
            await using var rig = await StartAsync();
            var camera = rig.Model.Inputs["Camera"];
            rig.Responder.Change(world => world.Inputs.RemoveAll(input => input.Uuid == camera.Uuid));
            await rig.Responder.SendEventAsync("InputRemoved", new { inputName = "Camera", inputUuid = camera.Uuid }, 8);
            await ModelRig.EventuallyAsync(() => camera.State == ObsObjectState.Gone, "the input to be gone");

            await Assert.ThrowsAsync<ObsRemovedException>(() => camera.CreateFilterAsync("Blur", "obs_blur").WithTimeoutAsync(Timeout));

            Assert.Equal(0, Count(rig, "CreateSourceFilter"));
        }

        [Fact]
        public async Task AChangeOfAFilter_WithoutAConnection_IsANotConnectedException_AndNothingIsSent()
        {
            await using var rig = await StartAsync();
            var mic = rig.Model.Inputs["Mic"];
            var gain = mic.Filters["Gain"];
            await rig.Model.DisconnectAsync();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => gain.SetEnabledAsync(false).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => mic.CreateFilterAsync("Blur", "obs_blur").WithTimeoutAsync(Timeout));

            Assert.Equal(0, Count(rig, "SetSourceFilterEnabled") + Count(rig, "CreateSourceFilter"));
            Assert.Equal(new[] { "Gain", "Gate", "Limiter" }, Names(mic));
        }

        [Fact]
        public async Task TheModel_SubscribesToTheEventsOfTheFilters_WhateverTheApplicationAsksFor()
        {
            await using var rig = new ModelSimRig(options: new ObsModelOptions { AdditionalEvents = EventSubscriptions.None });

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            Assert.NotNull(rig.Server.EventSubscriptions);
            Assert.Equal(EventSubscriptions.Filters, (EventSubscriptions)rig.Server.EventSubscriptions!.Value & EventSubscriptions.Filters);
        }

        [Fact]
        public async Task AnOBSStudioThatDoesNotKnowTheFilterRequests_GivesSourcesWithoutFilters_AndTheModelSettles()
        {
            // The simulation of OBS Studio answers 204 (not a request that it knows) to the list of filters.
            await using var rig = new ModelSimRig();
            rig.World(world => world.CreateScene("Gaming"));

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.Model.WaitUntilSettledAsync(Timeout);

            Assert.Empty(rig.Model.Scenes["Gaming"].Filters);
        }
    }
}
