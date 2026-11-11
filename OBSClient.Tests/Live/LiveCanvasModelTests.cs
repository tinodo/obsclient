namespace OBSStudioClient.Tests.Live
{
    using System.Globalization;
    using OBSStudioClient.Events;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// What the model does with the canvases against a real OBS Studio. The main canvas is there in every OBS Studio, and is compared with what the plain client lists. OBS Studio makes a canvas only for a plugin or a script
    /// that asks (in the source of OBS Studio its frontend calls AddCanvas only for obs_frontend_add_canvas, and obs-websocket has no request that makes one), so the live runner makes a second scene collection that saves a
    /// canvas and loads a script (tools/live/canvas-driver.lua), which makes, renames and removes canvases when this test writes a command into a file: what OBS Studio then lists, and says with its events, is what the
    /// model has to follow. The test loads that collection and puts the first one back.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveCanvasModelTests(ITestOutputHelper output)
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

        private static readonly Version CanvasesSince = new(5, 7, 0);

        [LiveFact]
        public async Task TheMainCanvas_IsShown_WithTheFlagsAndTheVideoThatOBSStudioSays()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            if (live.WebSocketVersion < CanvasesSince)
            {
                // An obs-websocket before 5.7.0 does not know canvases: the model has none to show, and it is synchronized all the same.
                Assert.Empty(model.Canvases.All);
                Assert.Null(model.Canvases.Main);
                Assert.Equal(ObsModelState.Synchronized, model.State);
                return;
            }

            var listed = await live.Client.GetCanvasList();
            foreach (var canvas in listed)
            {
                output.WriteLine($"OBS Studio lists '{canvas.CanvasName}' {canvas.CanvasUuid:D}: MAIN {canvas.CanvasFlags.Main}, ACTIVATE {canvas.CanvasFlags.Activate}, MIX_AUDIO {canvas.CanvasFlags.MixAudio}, SCENE_REF {canvas.CanvasFlags.SceneRef}, EPHEMERAL {canvas.CanvasFlags.Ephemeral}; video {canvas.CanvasVideoSettings.BaseWidth}x{canvas.CanvasVideoSettings.BaseHeight}, output {canvas.CanvasVideoSettings.OutputWidth}x{canvas.CanvasVideoSettings.OutputHeight}, {canvas.CanvasVideoSettings.FpsNumerator}/{canvas.CanvasVideoSettings.FpsDenominator}");
            }

            var main = model.Canvases.Main;
            Assert.NotNull(main);
            Assert.True(main.IsMain);
            Assert.Equal("Main", main.Name);
            Assert.Equal(new Guid("6c69626f-6273-4c00-9d88-c5136d61696e"), main.Uuid);
            Assert.Equal(ObsCanvasFlags.Main | ObsCanvasFlags.Activate | ObsCanvasFlags.MixAudio | ObsCanvasFlags.SceneRef, main.Flags);
            Assert.Same(main, model.Canvases.All[0]);
            Assert.Equal(listed.Select(canvas => canvas.CanvasUuid), model.Canvases.All.Select(canvas => canvas.Uuid));

            // The video of the main canvas is the video of OBS Studio (the profile that is current).
            var video = await model.GetVideoSettingsAsync().WaitAsync(Wait);
            var mainVideo = await main.GetVideoSettingsAsync().WaitAsync(Wait);
            Assert.NotNull(mainVideo);
            Assert.Equal((video.BaseWidth, video.BaseHeight, video.OutputWidth, video.OutputHeight, video.FpsNumerator, video.FpsDenominator), (mainVideo.BaseWidth, mainVideo.BaseHeight, mainVideo.OutputWidth, mainVideo.OutputHeight, mainVideo.FpsNumerator, mainVideo.FpsDenominator));
            await live.AssertModelEqualsObsAsync("with the canvases that OBS Studio starts with");
        }

        [LiveCanvasFact]
        public async Task TheCanvasesOfASceneCollection_AreLoadedWithIt_AndTheOnesThatAScriptMakesRenamesAndRemoves_AreFollowed()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var client = live.Client;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            if (live.WebSocketVersion < CanvasesSince)
            {
                return;
            }

            var original = (await client.GetSceneCollectionList()).CurrentSceneCollectionName;
            var seeded = LiveObs.SeededCanvas!.Value;
            var driver = new Driver();
            Assert.Equal(["Main"], Names(model));

            try
            {
                await model.SceneCollections.SetCurrentAsync(LiveObs.CanvasCollection).WaitAsync(Wait);
                await live.AssertModelEqualsObsAsync("after the scene collection with a canvas was loaded");

                // The canvas that the collection saves: the same UUID, the same flags, and no video (a canvas that is loaded has no video mix).
                Assert.Equal(["Main", seeded.Name], Names(model));
                var vertical = model.Canvases[seeded.Name];
                Assert.Equal(seeded.Uuid, vertical.Uuid);
                Assert.Equal((ObsCanvasFlags)seeded.Flags, vertical.Flags);
                var loaded = (await client.GetCanvasList()).Single(canvas => canvas.CanvasUuid == seeded.Uuid);
                output.WriteLine($"the canvas that the collection saves: '{loaded.CanvasName}', video {loaded.CanvasVideoSettings.BaseWidth}x{loaded.CanvasVideoSettings.BaseHeight}, {loaded.CanvasVideoSettings.FpsNumerator}/{loaded.CanvasVideoSettings.FpsDenominator}");
                Assert.Null(loaded.CanvasVideoSettings.BaseWidth);
                Assert.Null(loaded.CanvasVideoSettings.FpsNumerator);
                Assert.Null(await vertical.GetVideoSettingsAsync().WaitAsync(Wait));

                var ping = await driver.SendAsync("ping hello", output);
                Assert.EndsWith("ok", ping, StringComparison.Ordinal);

                // A canvas that a script makes, as a plugin does: the model follows the event, and the list is the one of OBS Studio.
                var overlay = Driver.Parse(await driver.SendAsync("create overlay 14 Overlay", output));
                await WaitForAsync(model, overlay.Uuid, present: true);
                await live.AssertModelEqualsObsAsync("after a script made a canvas");
                Assert.Equal(["Main", seeded.Name, "Overlay"], Names(model));
                Assert.Equal((ObsCanvasFlags)14, model.Canvases[overlay.Uuid].Flags);
                Assert.Null(await model.Canvases["Overlay"].GetVideoSettingsAsync().WaitAsync(Wait));
                Assert.Contains(live.Raw.Events, entry => entry.Name == "CanvasCreated" && entry.Arguments is CanvasCreatedEventArgs created && created.CanvasUuid == overlay.Uuid);

                // OBS Studio makes the name of a canvas unique.
                var twin = Driver.Parse(await driver.SendAsync("create twin 14 Overlay", output));
                await WaitForAsync(model, twin.Uuid, present: true);
                output.WriteLine($"a second canvas that was asked to be named 'Overlay' is named '{twin.Name}'");
                Assert.Equal("Overlay 2", twin.Name);
                Assert.Equal(["Main", seeded.Name, "Overlay", "Overlay 2"], Names(model));
                await live.AssertModelEqualsObsAsync("after a second canvas with the same name was made");

                // A canvas that is renamed is last in the list of OBS Studio, and a name that is taken is made unique.
                await driver.SendAsync("rename overlay Renamed", output);
                await EventuallyAsync(() => Names(model).SequenceEqual(["Main", seeded.Name, "Overlay 2", "Renamed"]), "the model to show the canvas that was renamed, last");
                await live.AssertModelEqualsObsAsync("after a canvas was renamed");
                Assert.Equal(overlay.Uuid, model.Canvases["Renamed"].Uuid);
                Assert.Contains(live.Raw.Events, entry => entry.Name == "CanvasNameChanged" && entry.Arguments is CanvasNameChangedEventArgs renamed && renamed.CanvasUuid == overlay.Uuid && renamed.OldCanvasName == "Overlay" && renamed.CanvasName == "Renamed");
                var taken = Driver.Parse(await driver.SendAsync("rename twin Renamed", output));
                output.WriteLine($"a canvas that was renamed to the name that is taken is named '{taken.Name}'");
                Assert.Equal("Renamed 2", taken.Name);
                await EventuallyAsync(() => Names(model).SequenceEqual(["Main", seeded.Name, "Renamed", "Renamed 2"]), "the model to show the canvas that was renamed to a name that was taken");
                await live.AssertModelEqualsObsAsync("after a canvas was renamed to a name that is taken");

                // A canvas that is removed leaves the model at once. What OBS Studio goes on listing while something holds on to the canvas (here the script, which has not released it) is what OBS Studio does,
                // and what it lists is not what the model shows.
                var removed = Driver.Parse(await driver.SendAsync("remove overlay", output));
                await WaitForAsync(model, removed.Uuid, present: false);
                var stillListed = (await client.GetCanvasList()).Any(canvas => canvas.CanvasUuid == removed.Uuid);
                output.WriteLine($"after obs_canvas_remove, while the script holds a reference, OBS Studio {(stillListed ? "still lists" : "no longer lists")} the canvas");
                Assert.Equal(["Main", seeded.Name, "Renamed 2"], Names(model));
                Assert.Single(live.Raw.Events, entry => entry.Name == "CanvasRemoved" && entry.Arguments is CanvasRemovedEventArgs gone && gone.CanvasUuid == removed.Uuid);
                await live.AssertModelEqualsObsAsync("after a canvas was removed while something holds on to it");

                // When the last reference is released OBS Studio lets go of the canvas, and says nothing more: the model does not change.
                await driver.SendAsync("release overlay", output);
                await Task.Delay(500);
                var listedAfterRelease = (await client.GetCanvasList()).Any(canvas => canvas.CanvasUuid == removed.Uuid);
                output.WriteLine($"after the script released the canvas, OBS Studio {(listedAfterRelease ? "still lists" : "no longer lists")} it");
                Assert.False(listedAfterRelease);
                Assert.Single(live.Raw.Events, entry => entry.Name == "CanvasRemoved" && entry.Arguments is CanvasRemovedEventArgs gone && gone.CanvasUuid == removed.Uuid);
                Assert.Equal(["Main", seeded.Name, "Renamed 2"], Names(model));
                await live.AssertModelEqualsObsAsync("after the canvas was let go of");

                // A canvas that is removed and released at once.
                await driver.SendAsync("remove twin", output);
                await driver.SendAsync("release twin", output);
                await EventuallyAsync(() => Names(model).SequenceEqual(["Main", seeded.Name]), "the model to show the canvas that was removed and released");
                await live.AssertModelEqualsObsAsync("after a canvas was removed and released");
            }
            finally
            {
                // The collection that the run started with is put back (the script removes what it made when its collection is unloaded).
                await model.SceneCollections.SetCurrentAsync(original).WaitAsync(Wait);
            }

            Assert.Equal(["Main"], Names(model));
            await live.AssertModelEqualsObsAsync("after the first scene collection was loaded again");
        }

        private static string[] Names(ObsModel model)
        {
            return model.Canvases.All.Select(canvas => canvas.Name).ToArray();
        }

        private static Task WaitForAsync(ObsModel model, Guid uuid, bool present)
        {
            return EventuallyAsync(() => model.Canvases.TryGet(uuid, out _) == present, $"the model to {(present ? "show" : "drop")} the canvas {uuid:D}");
        }

        private static Task EventuallyAsync(Func<bool> condition, string what)
        {
            return Model.ModelRig.EventuallyAsync(condition, what, Wait);
        }

        // The script of the canvases (tools/live/canvas-driver.lua): the test writes a command to a line of a file, and the script, which runs inside OBS Studio, answers in a second file.
        private sealed class Driver
        {
            private readonly string _path = LiveObs.CanvasCommands;
            private int _sent;

            public Driver()
            {
                File.Delete(this._path);
                File.Delete(this._path + ".out");
            }

            public static (string Name, Guid Uuid, long Flags) Parse(string answer)
            {
                var name = answer.IndexOf(" name=", StringComparison.Ordinal);
                var uuid = answer.IndexOf(" uuid=", StringComparison.Ordinal);
                var flags = answer.IndexOf(" flags=", StringComparison.Ordinal);
                Assert.True(name >= 0 && uuid > name && flags > uuid, $"The script did not say what it did: {answer}");
                var end = answer.IndexOf(' ', flags + 7);

                // The flags that libobs keeps are an unsigned number, and one of them (REMOVED, the top bit) is not one that obs-websocket tells.
                return (answer[(name + 6)..uuid], Guid.Parse(answer[(uuid + 6)..flags]), long.Parse(answer[(flags + 7)..(end < 0 ? answer.Length : end)], CultureInfo.InvariantCulture));
            }

            public async Task<string> SendAsync(string command, ITestOutputHelper output)
            {
                var number = Interlocked.Increment(ref this._sent);
                await AppendAsync(this._path, command + "\n");
                var until = DateTime.UtcNow + Wait;
                while (DateTime.UtcNow < until)
                {
                    foreach (var line in ReadAnswers(this._path + ".out"))
                    {
                        if (line.StartsWith(number.ToString(CultureInfo.InvariantCulture) + " ", StringComparison.Ordinal))
                        {
                            output.WriteLine($"script: {command} -> {line}");
                            Assert.DoesNotContain("failed", line, StringComparison.Ordinal);
                            Assert.DoesNotContain("unknown", line, StringComparison.Ordinal);
                            return line;
                        }
                    }

                    await Task.Delay(50);
                }

                throw new TimeoutException($"The script of the canvases did not answer '{command}' within {Wait.TotalSeconds:0} seconds. Is it loaded (the log of OBS Studio says)? Does this OBS Studio have scripting?");
            }

            // The test and the script have each other's files open at times. A file that is open for reading without sharing it for writing makes the open for appending of the other fail (File.ReadAllLines does that),
            // and the script could then not write its answer: both programs share the files for writing.
            private static async Task AppendAsync(string path, string text)
            {
                await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(text);
            }

            private static string[] ReadAnswers(string path)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        return [];
                    }

                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    var lines = new List<string>();
                    while (reader.ReadLine() is { } line)
                    {
                        lines.Add(line);
                    }

                    return [.. lines];
                }
                catch (IOException)
                {
                    // The file is not there any more, or is busy: the next read has the line.
                    return [];
                }
            }
        }
    }
}
