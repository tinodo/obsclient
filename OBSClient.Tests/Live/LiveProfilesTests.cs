namespace OBSStudioClient.Tests.Live
{
    using System.Diagnostics;
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// The profiles, the scene collections and the settings of OBS Studio itself (the video settings, the service of the stream, the folder of the recording, the parameters of a profile and the data that OBS Studio keeps for
    /// an application) against a real OBS Studio: what each request answers and when, which events come and in which order, how long OBS Studio is not ready while it switches (it answers 207), what a refusal looks like, and what
    /// is kept for a profile and what is global. It prints what it sees, and checks nothing: what the simulation does is what this printed. The profiles that it makes are removed again, and the profile and the scene collection
    /// that were the current ones are put back (OBS Studio has no request that removes a scene collection: the one that this makes stays, and the private copy that the live runner makes is deleted with it).
    /// </summary>
    [Collection("Live OBS")]
    public class LiveProfilesTests(ITestOutputHelper output)
    {
        private static string Short(JsonElement? data) => data is { } value ? (value.GetRawText().Length > 200 ? value.GetRawText()[..200] + "..." : value.GetRawText()) : string.Empty;

        [LiveUuidChangesFact]
        public async Task Observation_ProfilesSceneCollectionsAndTheSettingsOfObsStudio()
        {
            await using var session = await LiveSession.ConnectAsync(EventSubscriptions.All, removeLeftovers: true);
            var client = session.Client;
            var version = await client.GetVersion();
            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");

            var clock = Stopwatch.StartNew();
            var originalProfile = (await client.GetProfileList()).CurrentProfileName;
            var originalCollection = (await client.GetSceneCollectionList()).CurrentSceneCollectionName;
            var madeProfiles = new List<string>();

            // What happens after an action: the events with the time (to 20 ms) at which they came, until nothing came for a while; and, with a probe, how long OBS Studio refused requests (207).
            async Task StepAsync(string what, Func<Task>? act = null, int quiet = 1000, bool probe = false)
            {
                var from = session.RawEvents().Count;
                var started = clock.ElapsedMilliseconds;
                using var stop = new CancellationTokenSource();
                var probes = new List<(long Ms, string Outcome)>();
                Task? prober = null;
                if (probe)
                {
                    prober = Task.Run(async () =>
                    {
                        var last = string.Empty;
                        while (!stop.IsCancellationRequested)
                        {
                            string outcome;
                            try
                            {
                                await client.GetSceneCollectionList(stop.Token);
                                outcome = "answered";
                            }
                            catch (ObsResponseException exception)
                            {
                                outcome = $"refused {(int)exception.ErrorCode}";
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }

                            if (outcome != last)
                            {
                                probes.Add((clock.ElapsedMilliseconds - started, outcome));
                                last = outcome;
                            }

                            await Task.Delay(15);
                        }
                    });
                }

                var result = "ok";
                try
                {
                    if (act is not null)
                    {
                        await act();
                    }
                }
                catch (ObsResponseException exception)
                {
                    result = $"refused {(int)exception.ErrorCode} {exception.ErrorCode}: {exception.ErrorMessage}";
                }

                var answered = clock.ElapsedMilliseconds - started;
                var stamps = new List<(long Ms, string Text)>();
                var count = from;
                var lastChange = clock.ElapsedMilliseconds;
                while (clock.ElapsedMilliseconds - lastChange < quiet && clock.ElapsedMilliseconds - started < 25000)
                {
                    await Task.Delay(20);
                    var all = session.RawEvents();
                    if (all.Count != count)
                    {
                        stamps.AddRange(all.Skip(count).Select(entry => (clock.ElapsedMilliseconds - started, $"{entry.Type} {Short(entry.Data)}".Trim())));
                        count = all.Count;
                        lastChange = clock.ElapsedMilliseconds;
                    }
                }

                stop.Cancel();
                if (prober is not null)
                {
                    await prober;
                }

                output.WriteLine($"{what}: {result} (answered after {answered} ms)");
                foreach (var (ms, text) in stamps)
                {
                    output.WriteLine($"      +{ms} ms {text}");
                }

                if (probe)
                {
                    output.WriteLine($"      GetSceneCollectionList meanwhile: {string.Join(", ", probes.Select(entry => $"+{entry.Ms} ms {entry.Outcome}"))}");
                }
            }

            async Task<string> ProfilesAsync()
            {
                var list = await client.GetProfileList();
                return $"profiles [{string.Join(", ", list.Profiles)}], current '{list.CurrentProfileName}'";
            }

            async Task<string> CollectionsAsync()
            {
                var list = await client.GetSceneCollectionList();
                return $"collections [{string.Join(", ", list.SceneCollections)}], current '{list.CurrentSceneCollectionName}'";
            }

            async Task<string> SettingsAsync()
            {
                var video = await client.GetVideoSettings();
                var service = await client.GetStreamServiceSettings();
                var folder = await client.GetRecordDirectory();
                var server = service.StreamServiceSettings is JsonElement { ValueKind: JsonValueKind.Object } settings && settings.TryGetProperty("server", out var found) ? found.GetString() : "?";
                return $"video {video.BaseWidth}x{video.BaseHeight} -> {video.OutputWidth}x{video.OutputHeight} at {video.FpsNumerator}/{video.FpsDenominator}; service '{service.StreamServiceType}' server '{server}'; record folder '{folder}'";
            }

            var profile = LiveObs.UniqueName("pa");
            try
            {
                output.WriteLine($"At the start: {await ProfilesAsync()}; {await CollectionsAsync()}");
                output.WriteLine($"Settings of the profile '{originalProfile}': {await SettingsAsync()}");
                await StepAsync("00 the persistent data of a slot that was never set (global, profile)", async () =>
                {
                    output.WriteLine($"      global: {JsonSerializer.Serialize(await client.GetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_GLOBAL, "obsclient-live-slot"))}");
                    output.WriteLine($"      profile: {JsonSerializer.Serialize(await client.GetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_PROFILE, "obsclient-live-slot"))}");
                });
                await StepAsync("01 a profile parameter that does not exist", async () =>
                {
                    var parameter = await client.GetProfileParameter("obsclient-live", "probe");
                    output.WriteLine($"      value {(parameter.ParameterValue is null ? "null" : $"'{parameter.ParameterValue}'")}, default {(parameter.DefaultParameterValue is null ? "null" : $"'{parameter.DefaultParameterValue}'")}");
                    var known = await client.GetProfileParameter("Video", "BaseCX");
                    output.WriteLine($"      Video/BaseCX: value {(known.ParameterValue is null ? "null" : $"'{known.ParameterValue}'")}, default {(known.DefaultParameterValue is null ? "null" : $"'{known.DefaultParameterValue}'")}");
                });

                await StepAsync("02 CreateProfile (OBS Studio switches to the new profile)", async () =>
                {
                    await client.CreateProfile(profile);
                    madeProfiles.Add(profile);
                });
                output.WriteLine($"      then: {await ProfilesAsync()}");
                await StepAsync("03 the same profile again", () => client.CreateProfile(profile));
                await StepAsync("04 SetCurrentProfile to the profile that is the current one", () => client.SetCurrentProfile(profile));

                output.WriteLine($"Settings of the new profile: {await SettingsAsync()}");
                await StepAsync("05 SetVideoSettings (1280x720 at 30/1) in the new profile", () => client.SetVideoSettings(30, 1, 1280, 720, 1280, 720));
                await StepAsync("06 SetVideoSettings with an fps that is not valid (0 as the numerator)", () => client.SetVideoSettings(fpsNumerator: 0));
                await StepAsync("07 SetStreamServiceSettings (rtmp_custom)", () => client.SetStreamServiceSettings("rtmp_custom", new { server = "rtmp://127.0.0.1/obsclient-live", key = "not-a-key" }));
                await StepAsync("08 SetStreamServiceSettings with a service type that does not exist", () => client.SetStreamServiceSettings("no_such_service", new { server = "x" }));
                var folder = Path.Combine(Path.GetTempPath(), "obsclient-live-rec-" + Guid.NewGuid().ToString("N")[..8]);
                await StepAsync("09 SetRecordDirectory", () => client.SetRecordDirectory(folder));
                await StepAsync("10 SetProfileParameter of a new category", () => client.SetProfileParameter("obsclient-live", "probe", "1"));
                await StepAsync("11 SetProfileParameter with a null value (it removes it?)", () => client.SetProfileParameter("obsclient-live", "probe", null));
                await StepAsync("12 the persistent data: set (global and profile), then read", async () =>
                {
                    await client.SetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_GLOBAL, "obsclient-live-slot", new { a = 1 });
                    await client.SetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_PROFILE, "obsclient-live-slot", new { b = "two" });
                    output.WriteLine($"      global: {JsonSerializer.Serialize(await client.GetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_GLOBAL, "obsclient-live-slot"))}");
                    output.WriteLine($"      profile: {JsonSerializer.Serialize(await client.GetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_PROFILE, "obsclient-live-slot"))}");
                });
                output.WriteLine($"Settings of the new profile now: {await SettingsAsync()}");

                await StepAsync("13 SetCurrentProfile back to the original profile", () => client.SetCurrentProfile(originalProfile), probe: true);
                output.WriteLine($"      then: {await ProfilesAsync()}");
                output.WriteLine($"Settings of the original profile: {await SettingsAsync()}");
                output.WriteLine($"      the persistent data of the slot: global {JsonSerializer.Serialize(await client.GetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_GLOBAL, "obsclient-live-slot"))}, profile {JsonSerializer.Serialize(await client.GetPersistentData(Realm.OBS_WEBSOCKET_DATA_REALM_PROFILE, "obsclient-live-slot"))}");
                await StepAsync("14 SetCurrentProfile to the new profile again", () => client.SetCurrentProfile(profile));
                await StepAsync("15 RemoveProfile of the current profile", () => client.RemoveProfile(profile));
                await StepAsync("16 SetCurrentProfile that does not exist", () => client.SetCurrentProfile("obsclient-live-no-such-profile"));
                await StepAsync("17 RemoveProfile that does not exist", () => client.RemoveProfile("obsclient-live-no-such-profile"));
                await StepAsync("18 SetCurrentProfile back, to remove the new profile", () => client.SetCurrentProfile(originalProfile));
                await StepAsync("19 RemoveProfile of a profile that is not current", async () =>
                {
                    await client.RemoveProfile(profile);
                    madeProfiles.Remove(profile);
                });
                output.WriteLine($"      then: {await ProfilesAsync()}");

                // The scene collections: a switch changes everything that the model holds.
                var collection = LiveObs.UniqueName("ca");
                await StepAsync("20 CreateSceneCollection (OBS Studio switches to the new, empty collection)", () => client.CreateSceneCollection(collection), probe: true, quiet: 1500);
                output.WriteLine($"      then: {await CollectionsAsync()}; scenes: [{string.Join(", ", (await client.GetSceneList()).Scenes.Select(scene => scene.SceneName))}]");
                await StepAsync("21 CreateSceneCollection with the same name", () => client.CreateSceneCollection(collection));
                await StepAsync("22 SetCurrentSceneCollection to the current collection", () => client.SetCurrentSceneCollection(collection), probe: true);
                await StepAsync("23 SetCurrentSceneCollection that does not exist", () => client.SetCurrentSceneCollection("obsclient-live-no-such-collection"));
                await StepAsync("24 SetCurrentSceneCollection back to the original collection", () => client.SetCurrentSceneCollection(originalCollection), probe: true, quiet: 1500);
                output.WriteLine($"      then: {await CollectionsAsync()}");
                await StepAsync("25 SetCurrentSceneCollection to the new collection and back, without waiting (two requests at once)", async () =>
                {
                    await client.SetCurrentSceneCollection(collection);
                    try
                    {
                        await client.SetCurrentSceneCollection(originalCollection);
                    }
                    catch (ObsResponseException exception)
                    {
                        output.WriteLine($"      the second request: refused {(int)exception.ErrorCode} {exception.ErrorCode}: {exception.ErrorMessage}");
                    }
                }, quiet: 2500);
                output.WriteLine($"      then: {await CollectionsAsync()}");
            }
            finally
            {
                try
                {
                    if ((await client.GetSceneCollectionList()).CurrentSceneCollectionName != originalCollection)
                    {
                        await client.SetCurrentSceneCollection(originalCollection);
                        await Task.Delay(2500);
                    }

                    if ((await client.GetProfileList()).CurrentProfileName != originalProfile)
                    {
                        await client.SetCurrentProfile(originalProfile);
                        await Task.Delay(1500);
                    }

                    foreach (var made in madeProfiles.ToArray())
                    {
                        await client.RemoveProfile(made);
                    }
                }
                catch (ObsResponseException exception)
                {
                    output.WriteLine($"Could not put back what the test changed: {exception.Message}");
                }

                await session.RemoveLeftoversAsync();
            }
        }
    }
}
