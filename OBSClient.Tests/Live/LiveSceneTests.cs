namespace OBSStudioClient.Tests.Live
{
    using System.Diagnostics;
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Tests.Conformance;
    using Xunit.Abstractions;

    /// <summary>
    /// What a real OBS Studio does when the client creates, changes, renames, switches to and removes a scene and an input: the answers,
    /// and the events that OBS Studio sends for it. Everything that is created carries the name prefix of <see cref="LiveObs.NamePrefix"/>
    /// and is removed again; the scene that was on the program output is put back.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveSceneTests(ITestOutputHelper output)
    {
        private static readonly EventSubscriptions WithTransforms = EventSubscriptions.All | EventSubscriptions.SceneItemTransformChanged;

        [LiveChangesFact]
        public async Task ASceneWithAnInput_CanBeCreatedChangedRenamedSwitchedToAndRemoved_AndEveryEventIsRaisedAsObsStudioSentIt()
        {
            await using var session = await LiveSession.ConnectAsync(WithTransforms, removeLeftovers: true);
            var client = session.Client;
            var version = await client.GetVersion();
            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");

            var sceneName = LiveObs.UniqueName("scene");
            var renamed = sceneName + "-renamed";
            var inputName = LiveObs.UniqueName("color");
            var original = await client.GetCurrentProgramScene();
            Guid? sceneUuid = null;
            string? createdInput = null;
            try
            {
                // A scene.
                sceneUuid = await client.CreateScene(sceneName);
                var scene = SceneRef.FromUuid(sceneUuid.Value);
                var sceneCreated = await session.WaitForEventAsync<SceneModifiedEventArgs>("SceneCreated", e => e.SceneUuid == sceneUuid);
                Assert.Equal(sceneName, sceneCreated.SceneName);
                Assert.False(sceneCreated.IsGroup);
                Assert.Contains((await client.GetSceneList()).Scenes, s => s.SceneUuid == sceneUuid && s.SceneName == sceneName);

                // An input in it, made with settings: and the settings that come back (the "settings" of issues 15 and 17).
                var kind = (await client.GetInputKindList())
                    .Where(candidate => candidate.StartsWith("color_source", StringComparison.Ordinal))
                    .OrderByDescending(candidate => candidate, StringComparer.Ordinal)
                    .FirstOrDefault();
                Assert.True(kind is not null, "OBS Studio offers no color source input.");
                var created = await client.CreateInput(
                    scene,
                    inputName,
                    kind,
                    new Dictionary<string, object> { ["color"] = 4278190335L, ["width"] = 640, ["height"] = 360 },
                    sceneItemEnabled: true);
                createdInput = inputName;
                var input = InputRef.FromUuid(created.InputUuid);
                var itemId = created.SceneItemId;

                var inputCreated = await session.WaitForEventAsync<InputCreatedEventArgs>("InputCreated", e => e.InputUuid == created.InputUuid);
                Assert.Equal(inputName, inputCreated.InputName);
                Assert.Equal(kind, inputCreated.InputKind);
                var itemCreated = await session.WaitForEventAsync<SceneItemCreatedEventArgs>("SceneItemCreated", e => e.SceneUuid == sceneUuid && e.SceneItemId == itemId);
                Assert.Equal(created.InputUuid, itemCreated.SourceUuid);
                Assert.Equal(inputName, itemCreated.SourceName);

                var items = await client.GetSceneItemList(scene);
                Assert.Contains(items, item => item.SceneItemId == itemId && item.SourceName == inputName);

                var settings = await client.GetInputSettings(input);
                Assert.Equal(kind, settings.InputKind);
                Assert.Equal(4278190335L, AsLong(settings.InputSettings["color"]));

                await client.SetInputSettings(input, new Dictionary<string, object> { ["color"] = 4294901760L }, overlay: true);
                var settingsChanged = await session.WaitForEventAsync<InputSettingsChangedEventArgs>("InputSettingsChanged", e => e.InputUuid == created.InputUuid);
                Assert.Equal(4294901760L, AsLong(settingsChanged.InputSettings["color"]));
                settings = await client.GetInputSettings(input);
                Assert.Equal(4294901760L, AsLong(settings.InputSettings["color"]));
                Assert.Equal(640L, AsLong(settings.InputSettings["width"]));

                // A partial change of the transform (issue 8): what is not given stays as it was.
                var initial = await client.GetSceneItemTransform(scene, itemId);
                await client.SetSceneItemTransform(scene, itemId, new SceneItemTransformUpdate { PositionX = 100f, PositionY = 50f });
                var moved = await client.GetSceneItemTransform(scene, itemId);
                Assert.Equal(100f, moved.PositionX);
                Assert.Equal(50f, moved.PositionY);
                Assert.Equal(initial.ScaleX, moved.ScaleX);
                Assert.Equal(initial.ScaleY, moved.ScaleY);
                Assert.Equal(initial.Rotation, moved.Rotation);
                var transformChanged = await session.WaitForEventAsync<SceneItemTransformChangedEventArgs>(
                    "SceneItemTransformChanged",
                    e => e.SceneUuid == sceneUuid && e.SceneItemId == itemId && e.SceneItemTransform.PositionX == 100f);
                Assert.Equal(50f, transformChanged.SceneItemTransform.PositionY);

                await client.SetSceneItemTransform(scene, itemId, new SceneItemTransformUpdate { ScaleX = 2f, ScaleY = 2f, Rotation = 90f });
                var scaled = await client.GetSceneItemTransform(scene, itemId);
                Assert.Equal(100f, scaled.PositionX);
                Assert.Equal(50f, scaled.PositionY);
                Assert.Equal(2f, scaled.ScaleX);
                Assert.Equal(2f, scaled.ScaleY);
                Assert.Equal(90f, scaled.Rotation);

                // What was read can be written back as it is, which OBS Studio only accepts when the bounds that do not apply are left out.
                await client.SetSceneItemTransform(scene, itemId, scaled);
                var written = await client.GetSceneItemTransform(scene, itemId);
                Assert.Equal(JsonSerializer.Serialize(scaled), JsonSerializer.Serialize(written));

                // Enabled and locked.
                await client.SetSceneItemEnabled(scene, itemId, false);
                var disabled = await session.WaitForEventAsync<SceneItemEnableStateChangedEventArgs>(
                    "SceneItemEnableStateChanged",
                    e => e.SceneUuid == sceneUuid && e.SceneItemId == itemId && !e.SceneItemEnabled);
                Assert.Equal(sceneName, disabled.SceneName);
                Assert.False(await client.GetSceneItemEnabled(scene, itemId));
                await client.SetSceneItemEnabled(scene, itemId, true);
                Assert.True(await client.GetSceneItemEnabled(scene, itemId));

                await client.SetSceneItemLocked(scene, itemId, true);
                await session.WaitForEventAsync<SceneItemLockStateChangedEventArgs>(
                    "SceneItemLockStateChanged",
                    e => e.SceneUuid == sceneUuid && e.SceneItemId == itemId && e.SceneItemLocked);
                Assert.True(await client.GetSceneItemLocked(scene, itemId));
                await client.SetSceneItemLocked(scene, itemId, false);
                Assert.False(await client.GetSceneItemLocked(scene, itemId));

                // A new name: the scene is the same one, by its UUID.
                await client.SetSceneName(scene, renamed);
                var nameChanged = await session.WaitForEventAsync<SceneNameChangedEventArgs>("SceneNameChanged", e => e.SceneUuid == sceneUuid);
                Assert.Equal(sceneName, nameChanged.OldSceneName);
                Assert.Equal(renamed, nameChanged.SceneName);
                var scenes = (await client.GetSceneList()).Scenes;
                Assert.DoesNotContain(scenes, s => s.SceneName == sceneName);
                Assert.Contains(scenes, s => s.SceneUuid == sceneUuid && s.SceneName == renamed);
                Assert.Contains(await client.GetSceneItemList(SceneRef.FromName(renamed)), item => item.SceneItemId == itemId);

                // Onto the program output and back (issue 13).
                await client.SetCurrentProgramScene(scene);
                var program = await session.WaitForEventAsync<SceneNameEventArgs>("CurrentProgramSceneChanged", e => e.SceneUuid == sceneUuid);
                Assert.Equal(renamed, program.SceneName);
                Assert.Equal(sceneUuid, (await client.GetCurrentProgramScene()).SceneUuid);
                await client.SetCurrentProgramScene(SceneRef.FromUuid(original.SceneUuid));
                await session.WaitForEventAsync<SceneNameEventArgs>("CurrentProgramSceneChanged", e => e.SceneUuid == original.SceneUuid);
                Assert.Equal(original.SceneUuid, (await client.GetCurrentProgramScene()).SceneUuid);

                // A second item for the same input: it can leave the scene (which is what SceneItemRemoved is for) while the input stays,
                // because the first item still holds it. An input whose last item is removed is gone with it.
                var second = await client.CreateSceneItem(scene, SourceRef.FromUuid(created.InputUuid));
                await session.WaitForEventAsync<SceneItemCreatedEventArgs>("SceneItemCreated", e => e.SceneUuid == sceneUuid && e.SceneItemId == second);
                await client.RemoveSceneItem(scene, second);
                var itemRemoved = await session.WaitForEventAsync<SceneItemRemovedEventArgs>("SceneItemRemoved", e => e.SceneUuid == sceneUuid && e.SceneItemId == second);
                Assert.Equal(inputName, itemRemoved.SourceName);
                var remaining = await client.GetSceneItemList(scene);
                Assert.DoesNotContain(remaining, item => item.SceneItemId == second);
                Assert.Contains(remaining, item => item.SceneItemId == itemId);

                await client.RemoveInput(input);
                createdInput = null;
                await session.WaitForEventAsync<InputNameEventArgs>("InputRemoved", e => e.InputUuid == created.InputUuid);
                var listedAtOnce = (await client.GetInputList()).Any(candidate => candidate.InputName == inputName);

                await client.RemoveScene(scene);
                var removedUuid = sceneUuid;
                sceneUuid = null;
                await session.WaitForEventAsync<SceneModifiedEventArgs>("SceneRemoved", e => e.SceneUuid == removedUuid);
                Assert.DoesNotContain((await client.GetSceneList()).Scenes, s => s.SceneUuid == removedUuid);

                // OBS Studio announces the removal of an input before the scenes that hold it have let go of it (a scene that is not on
                // an output only does so when it is removed itself), so the input can still be in the list for a while: InputRemoved is
                // what to go by. It is gone from the list once the scene that held it is.
                var stopwatch = Stopwatch.StartNew();
                var listed = true;
                while (listed && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
                {
                    listed = (await client.GetInputList()).Any(candidate => candidate.InputName == inputName);
                    if (listed)
                    {
                        await Task.Delay(50);
                    }
                }

                output.WriteLine($"Right after InputRemoved the input was {(listedAtOnce ? "still" : "no longer")} listed; {(listed ? "after 5 seconds it still was" : $"it was gone {stopwatch.Elapsed.TotalMilliseconds:N0} ms after its scene was removed")}.");
                Assert.False(listed, "The input that was removed is still in the list of inputs 5 seconds after the scene that held it was removed.");

                output.WriteLine(session.DescribeEvents());
                ConformanceAssert.NoProblems(
                    await session.CompareEventsWithWhatObsStudioSentAsync(),
                    $"The client does not raise the events of OBS Studio {version.ObsVersion} as OBS Studio sent them:");
            }
            finally
            {
                await RestoreAsync(client, original.SceneUuid, createdInput, sceneUuid);
            }
        }

        // What the settings of an input hold is not typed: a number is a JSON number, however the client gives it.
        private static long AsLong(object value) => JsonSerializer.SerializeToElement(value).GetInt64();

        // Best effort, each step on its own: what fails here must not hide why the test failed, nor keep the other steps from running.
        private static async Task RestoreAsync(ObsClient client, Guid programScene, string? input, Guid? scene)
        {
            await TryAsync(async () =>
            {
                if ((await client.GetCurrentProgramScene()).SceneUuid != programScene)
                {
                    await client.SetCurrentProgramScene(SceneRef.FromUuid(programScene));
                }
            });

            if (input is not null)
            {
                await TryAsync(() => client.RemoveInput(InputRef.FromName(input)));
            }

            if (scene is { } uuid)
            {
                await TryAsync(() => client.RemoveScene(SceneRef.FromUuid(uuid)));
            }
        }

        // The next run removes what is left (LiveSession.RemoveLeftoversAsync).
        private static async Task TryAsync(Func<Task> step)
        {
            try
            {
                await step();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Nothing more can be done about it here.
            }
        }
    }
}
