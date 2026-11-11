namespace OBSStudioClient.Tests.Live
{
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// The settings of inputs and filters, the details of the audio of an input, the kinds and their defaults, the choices of a list property, the buttons of the properties, the deinterlacing and the
    /// global audio inputs, against a real OBS Studio: they check what the model assumes where the stand-in of the unit tests only had what was read in the source of obs-websocket (what an event
    /// says, what a request refuses with, what a change of settings does to the fields that it does not name). Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/>
    /// and is removed again.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveSettingsTests(ITestOutputHelper output)
    {
        // The media source: it has audio, and its video comes from outside, so it can be deinterlaced. It is on every OBS Studio.
        private const string MediaKind = "ffmpeg_source";
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

        private async Task<ObsInput> MediaInputAsync(LiveModel live, ObsScene scene)
        {
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("media"), MediaKind, new { is_local_file = true });
            output.WriteLine($"media input: {input.Name}, audio {input.HasAudio}, settings {input.Settings.GetRawText()}");
            Assert.True(input.HasAudio, "The media source has no audio.");
            return input;
        }

        private static string[] Names(JsonElement settings)
        {
            return settings.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        }

        [LiveUuidChangesFact]
        public async Task TheSettingsOfAnInput_AreWhatOBSStudioHolds_WithoutTheDefaults_AndTheCallsChangeThem()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("settings"));
            var kind = await live.ColorKindAsync();

            var input = await scene.CreateInputAsync(LiveObs.UniqueName("color"), kind, new { color = 4278190335L, width = 640, height = 360 });
            output.WriteLine($"the settings after the creation: {input.Settings.GetRawText()}");
            var defaults = await model.GetDefaultInputSettingsAsync(kind);
            output.WriteLine($"the defaults of {kind}: {defaults.GetRawText()}");

            // What OBS Studio says are the settings that were set; the defaults of the kind are not among them.
            Assert.Equal(640, input.Settings.GetProperty("width").GetInt32());
            Assert.Equal(360, input.Settings.GetProperty("height").GetInt32());
            Assert.Equal(4278190335L, input.Settings.GetProperty("color").GetInt64());
            Assert.Equal(new[] { "color", "height", "width" }, Names(input.Settings));
            Assert.True(defaults.TryGetProperty("width", out _) && defaults.TryGetProperty("height", out _) && defaults.TryGetProperty("color", out _));
            await live.AssertModelEqualsObsAsync("after an input was made with settings");

            // A change of one field leaves the others.
            await input.SetSettingsAsync(new { width = 800 });
            Assert.Equal(800, input.Settings.GetProperty("width").GetInt32());
            Assert.Equal(360, input.Settings.GetProperty("height").GetInt32());
            await live.AssertModelEqualsObsAsync("after the width was changed");

            // Without an overlay the input has the given settings and no others.
            await input.SetSettingsAsync(new { width = 1024 }, overlay: false);
            output.WriteLine($"the settings after a reset: {input.Settings.GetRawText()}");
            Assert.Equal(new[] { "width" }, Names(input.Settings));
            Assert.Equal(1024, input.Settings.GetProperty("width").GetInt32());
            await live.AssertModelEqualsObsAsync("after a reset");

            // Someone else (the plain client) changes it, and the model follows.
            await live.Client.SetInputSettings(InputRef.FromUuid(input.Uuid), new Dictionary<string, object> { ["height"] = JsonDocument.Parse("200").RootElement.Clone() });
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => input.Settings.TryGetProperty("height", out var height) && height.GetInt32() == 200, Wait), "The model did not follow the settings that someone else changed.");
            await live.AssertModelEqualsObsAsync("after someone else changed the height");
        }

        [LiveUuidChangesFact]
        public async Task AFieldThatIsAnObject_AndAFieldThatIsNull_AreChangedAsOBSStudioDoesIt()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("nested"));
            var kinds = await model.GetInputKindsAsync();
            var textKind = kinds.FirstOrDefault(kind => kind.StartsWith("text_ft2_source", StringComparison.Ordinal)) ?? kinds.FirstOrDefault(kind => kind.StartsWith("text_gdiplus", StringComparison.Ordinal));
            Assert.True(textKind is not null, "OBS Studio offers no text source.");
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("text"), textKind!, new { text = "Hello", font = new { face = "Arial", size = 48 } });
            output.WriteLine($"a {textKind} with: {input.Settings.GetRawText()}");

            // A field that is an object, laid over the one that is there.
            await input.SetSettingsAsync(new { font = new { size = 72 } });
            output.WriteLine($"after the font {{size:72}} was laid over it: {input.Settings.GetRawText()}");
            var font = input.Settings.GetProperty("font");
            Assert.Equal(72, font.GetProperty("size").GetInt32());
            output.WriteLine($"the face is {(font.TryGetProperty("face", out var face) ? "kept: " + face.GetString() : "gone")}");
            await live.AssertModelEqualsObsAsync("after the font was laid over");

            // A field that is null.
            await input.SetSettingsAsync(new Dictionary<string, object?> { ["text"] = null });
            output.WriteLine($"after the text was set to null: {input.Settings.GetRawText()}");
            await live.AssertModelEqualsObsAsync("after the text was set to null");
        }

        [LiveUuidChangesFact]
        public async Task TheAudioDetailsOfAnInput_AreWhatOBSStudioHolds_AndTheCallsChangeThem()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var scene = await live.Model.CreateSceneAsync(LiveObs.UniqueName("audio"));
            var input = await this.MediaInputAsync(live, scene);

            // A new input has the middle, no offset, no monitoring and all six tracks.
            output.WriteLine($"a new input: balance {input.AudioBalance}, sync offset {input.SyncOffset.TotalMilliseconds} ms, monitor {input.MonitorType}, tracks {input.AudioTracks}");
            Assert.Equal(0.5f, input.AudioBalance);
            Assert.Equal(TimeSpan.Zero, input.SyncOffset);
            Assert.Equal(ObsMonitorType.Off, input.MonitorType);
            Assert.Equal(ObsAudioTracks.All, input.AudioTracks);
            await live.AssertModelEqualsObsAsync("after an input with audio was made");

            await input.SetAudioBalanceAsync(0.25f);
            Assert.Equal(0.25f, input.AudioBalance);
            await input.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(150));
            Assert.Equal(TimeSpan.FromMilliseconds(150), input.SyncOffset);
            await input.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(-200));
            Assert.Equal(TimeSpan.FromMilliseconds(-200), input.SyncOffset);
            await input.SetAudioTracksAsync(ObsAudioTracks.Track1 | ObsAudioTracks.Track3);
            Assert.Equal(ObsAudioTracks.Track1 | ObsAudioTracks.Track3, input.AudioTracks);
            await input.SetAudioTracksAsync(input.AudioTracks | ObsAudioTracks.Track6);
            Assert.Equal(ObsAudioTracks.Track1 | ObsAudioTracks.Track3 | ObsAudioTracks.Track6, input.AudioTracks);
            await live.AssertModelEqualsObsAsync("after the balance, the offset and the tracks were changed");

            // OBS Studio 33 has no monitor only: it holds monitor and output when it is asked for it (see ObsMonitorType). A computer without audio monitoring refuses.
            try
            {
                foreach (var type in new[] { ObsMonitorType.MonitorOnly, ObsMonitorType.MonitorAndOutput, ObsMonitorType.Off })
                {
                    await input.SetMonitorTypeAsync(type);
                    var raw = await live.Client.GetInputAudioMonitorType(InputRef.FromUuid(input.Uuid));
                    output.WriteLine($"monitor {type}: the model says {input.MonitorType}, OBS Studio says {raw}");
                    Assert.True(input.MonitorType == type || (type == ObsMonitorType.MonitorOnly && input.MonitorType == ObsMonitorType.MonitorAndOutput), $"Asked for {type}, and the model shows {input.MonitorType}.");
                }
            }
            catch (ObsResponseException refused)
            {
                output.WriteLine($"this OBS Studio can not monitor: {refused.ErrorCode}: {refused.Message}");
                Assert.Equal(RequestStatusCode.InvalidResourceState, refused.ErrorCode);
            }

            await live.AssertModelEqualsObsAsync("after the monitor type was changed");

            // The ends of the ranges are allowed, and OBS Studio refuses what is beyond them (the model does not send it: see the unit tests).
            await input.SetAudioBalanceAsync(0f);
            await input.SetAudioBalanceAsync(1f);
            await input.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(-950));
            await input.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(20000));
            Assert.Equal(TimeSpan.FromMilliseconds(20000), input.SyncOffset);
            await live.AssertModelEqualsObsAsync("after the ends of the ranges");
        }

        [LiveUuidChangesFact]
        public async Task OBSStudioTellsOfTheSettingsOfAnInput_WhenSomeoneElseChangesThem_AndNotOfTheSettingsOfAFilter()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("tells"));
            var color = await scene.CreateInputAsync(LiveObs.UniqueName("color"), await live.ColorKindAsync(), new { width = 64, height = 64 });
            var filterKind = (await model.GetFilterKindsAsync()).First(kind => kind.StartsWith("color_filter", StringComparison.Ordinal));
            var filter = await color.CreateFilterAsync(LiveObs.UniqueName("f"), filterKind, new { gamma = 0.5 });
            var inputEvents = 0;
            var filterEvents = 0;
            live.Client.InputSettingsChanged += (_, e) =>
            {
                if (e.InputUuid == color.Uuid)
                {
                    Interlocked.Increment(ref inputEvents);
                }
            };
            live.Client.SourceFilterSettingsChanged += (_, e) =>
            {
                if (e.FilterName == filter.Name)
                {
                    Interlocked.Increment(ref filterEvents);
                }
            };

            await live.Client.SetInputSettings(InputRef.FromUuid(color.Uuid), new Dictionary<string, object> { ["width"] = JsonDocument.Parse("80").RootElement.Clone() });
            await live.Client.SetSourceFilterSettings(SourceRef.FromUuid(color.Uuid), filter.Name, new Dictionary<string, object> { ["gamma"] = JsonDocument.Parse("-0.5").RootElement.Clone() });
            await Task.Delay(2000);

            // The model follows the settings of an input by the event, so the event is relied on. The one of a filter is in the protocol (since 5.4.0), and OBS Studio 33.0.0 does not send it:
            // libobs tells of the updates of public sources only, and a filter is private (see ObsFilter.GetSettingsAsync). What is seen is written down, and it does not fail the test: it is good news when it changes.
            output.WriteLine($"events after a change of someone else: InputSettingsChanged {Volatile.Read(ref inputEvents)}, SourceFilterSettingsChanged {Volatile.Read(ref filterEvents)}");
            Assert.True(Volatile.Read(ref inputEvents) >= 1, "OBS Studio did not tell of the change of the settings of an input.");
            Assert.Equal(80, color.Settings.GetProperty("width").GetInt32());
            var raw = await live.Client.GetSourceFilter(SourceRef.FromUuid(color.Uuid), filter.Name);
            output.WriteLine($"OBS Studio has {JsonSerializer.Serialize(raw.FilterSettings)} for the filter, and the model asked for them says {(await filter.GetSettingsAsync()).GetRawText()}");
        }

        [LiveUuidChangesFact]
        public async Task TheEventsOfTheSettingsAndTheAudioDetails_ComeForEveryCall_OrOnlyForAChange()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var scene = await live.Model.CreateSceneAsync(LiveObs.UniqueName("events"));
            var input = await this.MediaInputAsync(live, scene);
            var counts = new Dictionary<string, int>();
            void Count(string name)
            {
                lock (counts)
                {
                    counts[name] = counts.GetValueOrDefault(name) + 1;
                }
            }

            int Told(string name)
            {
                lock (counts)
                {
                    return counts.GetValueOrDefault(name);
                }
            }

            live.Client.InputSettingsChanged += (_, e) => { if (e.InputUuid == input.Uuid) { Count("settings"); } };
            live.Client.InputAudioBalanceChanged += (_, e) => { if (e.InputUuid == input.Uuid) { Count("balance"); } };
            live.Client.InputAudioSyncOffsetChanged += (_, e) => { if (e.InputUuid == input.Uuid) { Count("sync"); } };
            live.Client.InputAudioMonitorTypeChanged += (_, e) => { if (e.InputUuid == input.Uuid) { Count("monitor"); } };
            live.Client.InputAudioTracksChanged += (_, e) => { if (e.InputUuid == input.Uuid) { Count("tracks"); } };

            // Each is made twice, to the same value: does OBS Studio say it twice, or once?
            for (var time = 0; time < 2; time++)
            {
                await input.SetSettingsAsync(new { speed_percent = 90 });
                await input.SetAudioBalanceAsync(0.3f);
                await input.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(100));
                await input.SetAudioTracksAsync(ObsAudioTracks.Track2);
                try
                {
                    await input.SetMonitorTypeAsync(ObsMonitorType.MonitorOnly);
                }
                catch (ObsResponseException)
                {
                    // No monitoring on this computer.
                }
            }

            await Task.Delay(1500);
            output.WriteLine($"events for two equal calls of each: settings {Told("settings")}, balance {Told("balance")}, sync offset {Told("sync")}, monitor type {Told("monitor")}, tracks {Told("tracks")}");
            Assert.Equal(2, Told("settings"));
            Assert.Equal(2, Told("balance"));
            Assert.Equal(2, Told("sync"));
            Assert.Equal(1, Told("tracks"));
            Assert.InRange(Told("monitor"), 0, 1);
            await live.AssertModelEqualsObsAsync("after the same changes twice");
        }

        [LiveUuidChangesFact]
        public async Task TheDeinterlacing_OfAnAsyncInput_IsAskedForAndSet_AndOfAnInputThatIsNotAsync_IsRefused()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var scene = await live.Model.CreateSceneAsync(LiveObs.UniqueName("deinterlace"));
            var media = await this.MediaInputAsync(live, scene);
            var color = await scene.CreateInputAsync(LiveObs.UniqueName("color"), await live.ColorKindAsync(), new { width = 64, height = 64 });

            var mode = await media.GetDeinterlaceModeAsync();
            var order = await media.GetDeinterlaceFieldOrderAsync();
            output.WriteLine($"a new media input: {mode}, {order}");
            Assert.Equal(ObsDeinterlaceMode.Disabled, mode);
            Assert.Equal(ObsDeinterlaceFieldOrder.TopFieldFirst, order);

            await media.SetDeinterlaceModeAsync(ObsDeinterlaceMode.Blend2x);
            await media.SetDeinterlaceFieldOrderAsync(ObsDeinterlaceFieldOrder.BottomFieldFirst);
            Assert.Equal(ObsDeinterlaceMode.Blend2x, await media.GetDeinterlaceModeAsync());
            Assert.Equal(ObsDeinterlaceFieldOrder.BottomFieldFirst, await media.GetDeinterlaceFieldOrderAsync());
            foreach (var each in Enum.GetValues<ObsDeinterlaceMode>())
            {
                await media.SetDeinterlaceModeAsync(each);
                Assert.Equal(each, await media.GetDeinterlaceModeAsync());
            }

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => color.GetDeinterlaceModeAsync());
            output.WriteLine($"{refused.ErrorCode}: {refused.Message}");
            Assert.Equal(RequestStatusCode.InvalidResourceState, refused.ErrorCode);
            var refusedSet = await Assert.ThrowsAsync<ObsResponseException>(() => color.SetDeinterlaceFieldOrderAsync(ObsDeinterlaceFieldOrder.TopFieldFirst));
            Assert.Equal(RequestStatusCode.InvalidResourceState, refusedSet.ErrorCode);
        }

        [LiveUuidChangesFact]
        public async Task TheChoicesOfAListProperty_AreAskedFor_AndAButtonThatIsNotOneIsRefused()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("properties"));
            var kinds = await model.GetInputKindsAsync();
            var slideshow = kinds.FirstOrDefault(kind => kind.StartsWith("slideshow", StringComparison.Ordinal));
            Assert.True(slideshow is not null, "OBS Studio offers no slideshow.");
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("slides"), slideshow!, new { });

            var items = await input.GetPropertyItemsAsync("slide_mode");
            output.WriteLine($"the choices of slide_mode of a {slideshow}: {string.Join(", ", items.Select(item => $"{item.Name} = {item.Value?.GetRawText()}{(item.IsEnabled ? string.Empty : " (not enabled)")}"))}");
            Assert.NotEmpty(items);
            Assert.All(items, item => Assert.False(string.IsNullOrEmpty(item.Name)));

            var missing = await Assert.ThrowsAsync<ObsResponseException>(() => input.GetPropertyItemsAsync("no_such_property"));
            output.WriteLine($"{missing.ErrorCode}: {missing.Message}");
            Assert.Equal(RequestStatusCode.ResourceNotFound, missing.ErrorCode);
            Assert.False(input.IsRemoved);

            // A property that is there, and is not a list or not a button, is refused with another code than a property that is not there.
            var color = await scene.CreateInputAsync(LiveObs.UniqueName("color"), await live.ColorKindAsync(), new { width = 64, height = 64 });
            var notAList = await Assert.ThrowsAsync<ObsResponseException>(() => color.GetPropertyItemsAsync("width"));
            output.WriteLine($"{notAList.ErrorCode}: {notAList.Message}");
            Assert.Equal(RequestStatusCode.InvalidResourceType, notAList.ErrorCode);
            Assert.False(color.IsRemoved);

            var notAButton = await Assert.ThrowsAsync<ObsResponseException>(() => color.PressPropertyButtonAsync("width"));
            output.WriteLine($"{notAButton.ErrorCode}: {notAButton.Message}");
            Assert.Equal(RequestStatusCode.InvalidResourceType, notAButton.ErrorCode);
            var noButton = await Assert.ThrowsAsync<ObsResponseException>(() => color.PressPropertyButtonAsync("no_such_button"));
            output.WriteLine($"{noButton.ErrorCode}: {noButton.Message}");
            Assert.Equal(RequestStatusCode.ResourceNotFound, noButton.ErrorCode);
            Assert.False(color.IsRemoved);

            // The browser source has the button that reloads it; where there is none, the refusals above are all that there is to see.
            var browser = kinds.FirstOrDefault(kind => kind.StartsWith("browser_source", StringComparison.Ordinal));
            if (browser is null)
            {
                output.WriteLine("this OBS Studio has no browser source: the button is not pressed");
                return;
            }

            var page = await scene.CreateInputAsync(LiveObs.UniqueName("browser"), browser, new { url = "about:blank", width = 64, height = 64 });
            await page.PressPropertyButtonAsync("refreshnocache");
            await live.AssertModelEqualsObsAsync("after the button of a browser source was pressed");
        }

        [LiveUuidChangesFact]
        public async Task TheGlobalAudioInputs_AreTheInputsOfTheModel()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;

            var special = await model.GetSpecialInputsAsync();
            var raw = await live.Client.GetSpecialInputs();
            output.WriteLine($"{special}; OBS Studio says {raw.Desktop1}, {raw.Desktop2}, {raw.Mic1}, {raw.Mic2}, {raw.Mic3}, {raw.Mic4}");

            void Check(string? name, ObsInput? shown)
            {
                if (name is null)
                {
                    Assert.Null(shown);
                }
                else
                {
                    Assert.NotNull(shown);
                    Assert.Equal(name, shown!.Name);
                    Assert.Same(model.Inputs[name], shown);
                }
            }

            Check(raw.Desktop1, special.Desktop1);
            Check(raw.Desktop2, special.Desktop2);
            Check(raw.Mic1, special.Mic1);
            Check(raw.Mic2, special.Mic2);
            Check(raw.Mic3, special.Mic3);
            Check(raw.Mic4, special.Mic4);
        }

        [LiveUuidChangesFact]
        public async Task TheKindsOfInputsAndFilters_AndTheirDefaults_AreAskedFor()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;

            var versioned = await model.GetInputKindsAsync();
            var unversioned = await model.GetInputKindsAsync(unversioned: true);
            var rawVersioned = await live.Client.GetInputKindList(false);
            var rawUnversioned = await live.Client.GetInputKindList(true);
            var filters = await model.GetFilterKindsAsync();
            var rawFilters = await live.Client.GetSourceFilterKindList();
            output.WriteLine($"{versioned.Count} input kinds ({unversioned.Count} unversioned), {filters.Count} filter kinds");

            Assert.Equal(rawVersioned.Order(StringComparer.Ordinal), versioned.Order(StringComparer.Ordinal));
            Assert.Equal(rawFilters.Order(StringComparer.Ordinal), filters.Order(StringComparer.Ordinal));
            Assert.Contains(versioned, kind => kind.StartsWith("color_source", StringComparison.Ordinal));
            Assert.Contains(MediaKind, versioned);
            Assert.Contains(filters, kind => kind.StartsWith("color_filter", StringComparison.Ordinal));

            // What OBS Studio calls a kind without a version is whatever it says: the XSHM capture of OBS Studio 32 on Linux is "xshm_input_v2" there, so no rule about the name holds.
            Assert.Equal(rawUnversioned.Order(StringComparer.Ordinal), unversioned.Order(StringComparer.Ordinal));

            var colorKind = await live.ColorKindAsync();
            var inputDefaults = await model.GetDefaultInputSettingsAsync(colorKind);
            var rawInputDefaults = await live.Client.GetInputDefaultSettings(colorKind);
            Assert.Equal(LiveModel.Canonical(rawInputDefaults), LiveModel.Canonical(inputDefaults));
            var filterKind = filters.First(kind => kind.StartsWith("color_filter", StringComparison.Ordinal));
            var filterDefaults = await model.GetDefaultFilterSettingsAsync(filterKind);
            output.WriteLine($"the defaults of {filterKind}: {filterDefaults.GetRawText()}");
            Assert.True(filterDefaults.TryGetProperty("gamma", out _));

            var noInput = await Assert.ThrowsAsync<ObsResponseException>(() => model.GetDefaultInputSettingsAsync("no_such_kind"));
            var noFilter = await Assert.ThrowsAsync<ObsResponseException>(() => model.GetDefaultFilterSettingsAsync("no_such_kind"));
            output.WriteLine($"{noInput.ErrorCode}: {noInput.Message}; {noFilter.ErrorCode}: {noFilter.Message}");
            Assert.Equal(RequestStatusCode.InvalidInputKind, noInput.ErrorCode);
            Assert.Equal(RequestStatusCode.InvalidFilterKind, noFilter.ErrorCode);
        }

        [LiveUuidChangesFact]
        public async Task TheSettingsOfAFilter_AreAskedFor_AndTheCallsChangeThem()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("filter"));
            var filterKind = (await model.GetFilterKindsAsync()).First(kind => kind.StartsWith("color_filter", StringComparison.Ordinal));

            var filter = await scene.CreateFilterAsync(LiveObs.UniqueName("color"), filterKind, new { gamma = 0.5 });
            var made = await filter.GetSettingsAsync();
            output.WriteLine($"the settings of the filter after the creation: {made.GetRawText()}");
            Assert.Equal(0.5, made.GetProperty("gamma").GetDouble());
            await AssertSettingsAreWhatOBSStudioHoldsAsync(live, scene, filter, "after a filter was made with settings");

            await filter.SetSettingsAsync(new { contrast = 0.25 });
            var changed = await filter.GetSettingsAsync();
            Assert.Equal(0.5, changed.GetProperty("gamma").GetDouble());
            Assert.Equal(0.25, changed.GetProperty("contrast").GetDouble());
            await AssertSettingsAreWhatOBSStudioHoldsAsync(live, scene, filter, "after a field of the settings of the filter was changed");

            // A field that is an object is replaced as a whole, and a null is ignored: what it does for an input it does for a filter (the color filter has no field that is an object, so the null is the check).
            await filter.SetSettingsAsync(new Dictionary<string, object?> { ["gamma"] = null, ["hue_shift"] = 10 });
            var withNull = await filter.GetSettingsAsync();
            output.WriteLine($"the settings of the filter after a null field: {withNull.GetRawText()}");
            Assert.Equal(0.5, withNull.GetProperty("gamma").GetDouble());
            Assert.Equal(10.0, withNull.GetProperty("hue_shift").GetDouble());

            await filter.SetSettingsAsync(new { saturation = 0.75 }, overlay: false);
            var reset = await filter.GetSettingsAsync();
            output.WriteLine($"the settings of the filter after a reset: {reset.GetRawText()}");
            Assert.Equal(new[] { "saturation" }, Names(reset));
            await AssertSettingsAreWhatOBSStudioHoldsAsync(live, scene, filter, "after the settings of the filter were reset");

            // Someone else changes them: what is asked for after it is what OBS Studio holds, and there is no model state that could be behind.
            await live.Client.SetSourceFilterSettings(SourceRef.FromUuid(scene.Uuid), filter.Name, new Dictionary<string, object> { ["gamma"] = JsonDocument.Parse("-0.5").RootElement.Clone() });
            Assert.Equal(-0.5, (await filter.GetSettingsAsync()).GetProperty("gamma").GetDouble());
            await AssertSettingsAreWhatOBSStudioHoldsAsync(live, scene, filter, "after someone else changed the settings of the filter");
        }

        private static async Task AssertSettingsAreWhatOBSStudioHoldsAsync(LiveModel live, ObsSource source, ObsFilter filter, string when)
        {
            var asked = await filter.GetSettingsAsync();
            var raw = await live.Client.GetSourceFilter(SourceRef.FromUuid(source.Uuid), filter.Name);
            Assert.Equal(LiveModel.Canonical(raw.FilterSettings), LiveModel.Canonical(asked));
            await live.AssertModelEqualsObsAsync(when);
        }
    }
}
