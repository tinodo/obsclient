namespace OBSStudioClient.Tests.Live
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// What the model does with whether the inputs are on the air and shown (<see cref="ObsHighVolume.Activity"/>) against a real OBS Studio: the program scene (a cut and a fade), a scene in a scene, scene items that
    /// are hidden, added and removed, an input that is made or removed, and studio mode with its preview scene and its transition. After each step the model has to equal what OBS Studio holds, which includes what
    /// <c>GetSourceActive</c> says of every input. Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again, and studio mode and the current transition are put back.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveActivityModelTests(ITestOutputHelper output)
    {
        private static object ColorSettings => new { color = 4278190335L, width = 320, height = 180 };

        [LiveUuidChangesFact]
        public async Task TheActivityOfTheInputs_FollowsOBSStudio_InEveryWayThatItChanges_AndTheModelEqualsWhatObsStudioHolds()
        {
            await using var live = await LiveModel.ConnectAsync(output, ObsHighVolume.Activity);
            var model = live.Model;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            await live.AssertModelEqualsObsAsync("at connect");
            var kind = await live.ColorKindAsync();
            var client = live.Client;

            var told = new List<(string Input, string Property, object? Old, object? New)>();
            model.Changed += (_, e) =>
            {
                if (e.Kind == ObsChangeKind.PropertyChanged && e.Entity is ObsInput input && e.PropertyName is nameof(ObsInput.IsActive) or nameof(ObsInput.IsShowing))
                {
                    lock (told)
                    {
                        told.Add((input.Name, e.PropertyName!, e.OldValue, e.NewValue));
                    }
                }
            };

            var originalStudio = await client.GetStudioModeEnabled();
            var originalTransition = await client.GetCurrentSceneTransition();
            try
            {
                // The scenes: A shows ia, B shows ib, C shows ic and the scene A. The program scene is a scene of the user, so none of them is on the air, and a cut puts a scene on the air at once.
                await client.SetCurrentSceneTransition("Cut");
                var a = await model.CreateSceneAsync(LiveObs.UniqueName("act-a"));
                var b = await model.CreateSceneAsync(LiveObs.UniqueName("act-b"));
                var c = await model.CreateSceneAsync(LiveObs.UniqueName("act-c"));
                var ia = await a.CreateInputAsync(LiveObs.UniqueName("act-ia"), kind, ColorSettings);
                var ib = await b.CreateInputAsync(LiveObs.UniqueName("act-ib"), kind, ColorSettings);
                var ic = await c.CreateInputAsync(LiveObs.UniqueName("act-ic"), kind, ColorSettings);
                var nesting = await c.AddAsync(a);
                await live.AssertModelEqualsObsAsync("after the scenes and the inputs were made");
                Assert.All([ia, ib, ic], input =>
                {
                    Assert.False(input.IsActive);
                    Assert.False(input.IsShowing);
                });

                // The program scene: the inputs of the scene that goes on the air are on the air, and the old ones are not when the transition has ended.
                await a.SwitchToAsync();
                await live.AssertModelEqualsObsAsync("after the program scene became A");
                Assert.True(ia.IsActive);
                await b.SwitchToAsync();
                await live.AssertModelEqualsObsAsync("after the program scene became B");
                Assert.True(ib.IsActive);

                // The inputs of the scene that went off the air are taken off it a moment after the transition has ended: OBS Studio and the model can both still say that they are on it.
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ia.IsActive == false, TimeSpan.FromSeconds(10)), "The input of the old scene did not go off the air.");
                await live.AssertModelEqualsObsAsync("after the input of the old scene went off the air");

                // C shows ic and the scene A, which shows ia.
                await c.SwitchToAsync();
                await live.AssertModelEqualsObsAsync("after the program scene became C, which nests A");
                Assert.True(ic.IsActive);
                Assert.True(ia.IsActive);
                await nesting.SetEnabledAsync(false);
                await live.AssertModelEqualsObsAsync("after the scene item of A in C was hidden");
                Assert.False(ia.IsActive);
                Assert.False(ia.IsShowing);
                await nesting.SetEnabledAsync(true);
                await live.AssertModelEqualsObsAsync("after the scene item of A in C was shown");
                Assert.True(ia.IsActive);

                // Items and inputs that come and go in the scene that is on the air.
                await a.SwitchToAsync();
                await live.AssertModelEqualsObsAsync("after the program scene became A again");
                var iaItem = a.Items[ia.Name];
                await iaItem.SetEnabledAsync(false);
                await live.AssertModelEqualsObsAsync("after the scene item of ia was hidden");
                Assert.False(ia.IsActive);
                await iaItem.SetEnabledAsync(true);
                await live.AssertModelEqualsObsAsync("after the scene item of ia was shown");
                var id = await a.CreateInputAsync(LiveObs.UniqueName("act-id"), kind, ColorSettings);
                await live.AssertModelEqualsObsAsync("after an input was made in the scene that is on the air");
                Assert.True(id.IsActive);
                var ibItem = await a.AddAsync(ib);
                await live.AssertModelEqualsObsAsync("after ib was added to the scene that is on the air");
                Assert.True(ib.IsActive);
                await ibItem.RemoveAsync();
                await live.AssertModelEqualsObsAsync("after the scene item of ib was removed from the scene that is on the air");
                Assert.False(ib.IsActive);

                // Studio mode with a preview scene that is not the program scene: the preview shows its inputs, and does not put them on the air. A copy of the program scene is on the air.
                await model.SetStudioModeEnabledAsync(true);
                await live.AssertModelEqualsObsAsync("after studio mode was switched on");
                Assert.True(ia.IsActive);
                await c.SwitchToPreviewAsync();
                await live.AssertModelEqualsObsAsync("after the preview scene became C");
                Assert.False(ic.IsActive);
                Assert.True(ic.IsShowing);
                await iaItem.SetEnabledAsync(false);
                await live.AssertModelEqualsObsAsync("after the scene item of ia in the program scene was hidden in studio mode");
                Assert.True(ia.IsActive);
                await iaItem.SetEnabledAsync(true);
                await live.AssertModelEqualsObsAsync("after the scene item of ia in the program scene was shown in studio mode");

                // The transition button: the preview scene goes on the air, and the scene that was is the preview scene when the transition has ended.
                await b.SwitchToPreviewAsync();
                await live.AssertModelEqualsObsAsync("after the preview scene became B");
                Assert.True(ib.IsShowing);
                await model.Transitions.TriggerAsync();
                await live.AssertModelEqualsObsAsync("after the transition of studio mode");
                Assert.True(ib.IsActive);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ia.IsActive == false, TimeSpan.FromSeconds(10)), "The input of the scene that was on the air did not go off it.");
                await live.AssertModelEqualsObsAsync("after the input of the old scene went off the air");
                Assert.True(ia.IsShowing);

                // The program scene is the preview scene as well, and what is done to the scene is not done to what is on the air (the copy), while the preview shows the scene itself.
                await model.SetStudioModeEnabledAsync(false);
                await live.AssertModelEqualsObsAsync("after studio mode was switched off");
                await a.SwitchToAsync();
                await live.AssertModelEqualsObsAsync("after the program scene became A before studio mode");
                var iy = await a.CreateInputAsync(LiveObs.UniqueName("act-iy"), kind, ColorSettings, enabled: false);
                await live.AssertModelEqualsObsAsync("after a hidden input was made in the program scene");
                Assert.False(iy.IsActive);
                await model.SetStudioModeEnabledAsync(true);
                await live.AssertModelEqualsObsAsync("after studio mode was switched on, with A as the program scene and the preview scene");
                var iyItem = a.Items[iy.Name];
                await iyItem.SetEnabledAsync(true);
                await live.AssertModelEqualsObsAsync("after the scene item of iy was shown in studio mode");
                Assert.False(iy.IsActive);
                Assert.True(iy.IsShowing);
                var iz = await a.CreateInputAsync(LiveObs.UniqueName("act-iz"), kind, ColorSettings);
                await live.AssertModelEqualsObsAsync("after an input was made in the program scene in studio mode");
                Assert.False(iz.IsActive);
                Assert.True(iz.IsShowing);
                await b.SwitchToPreviewAsync();
                await live.AssertModelEqualsObsAsync("after the preview scene became B, and the program scene is only a copy");
                Assert.False(iz.IsShowing);
                await model.SetStudioModeEnabledAsync(false);
                await live.AssertModelEqualsObsAsync("after studio mode was switched off, and the program scene is the scene itself");
                Assert.True(iz.IsActive);
                Assert.True(iy.IsActive);
                await iz.RemoveAsync();
                await live.AssertModelEqualsObsAsync("after iz was removed while it is on the air");

                // A fade: both scenes are on the air while it runs.
                await b.SwitchToAsync();
                await live.AssertModelEqualsObsAsync("after the program scene became B before the fade");
                await client.SetCurrentSceneTransition("Fade");
                await client.SetCurrentSceneTransitionDuration(800f);
                await Task.Delay(300);
                await a.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ia.IsActive == true && ib.IsActive == true, TimeSpan.FromSeconds(10)), "The inputs of both scenes were not on the air while the fade ran.");
                await live.AssertModelEqualsObsAsync("after the fade to A");
                Assert.True(ia.IsActive);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ib.IsActive == false, TimeSpan.FromSeconds(10)), "The input of the scene that faded out did not go off the air.");
                await live.AssertModelEqualsObsAsync("after the input of the old scene went off the air, after the fade");

                // Removals: an input that OBS Studio destroys is not announced, and the program scene that is removed puts the scene that takes its place on the air.
                await client.SetCurrentSceneTransition("Cut");
                await Task.Delay(300);
                await c.RemoveAsync();
                await live.AssertModelEqualsObsAsync("after the scene C was removed while it was not on the air");
                await a.RemoveAsync();
                await live.AssertModelEqualsObsAsync("after the scene A was removed while it was the program scene");

                // The changes that were told: each input was told when it went on and off the air.
                lock (told)
                {
                    output.WriteLine($"{told.Count} changes of the activity were told.");
                    Assert.Contains(told, change => change.Input == ia.Name && change.Property == nameof(ObsInput.IsActive) && change.New is true);
                    Assert.Contains(told, change => change.Input == ia.Name && change.Property == nameof(ObsInput.IsActive) && change.New is false);
                    Assert.Contains(told, change => change.Input == ic.Name && change.Property == nameof(ObsInput.IsShowing) && change.New is true);
                }
            }
            finally
            {
                try
                {
                    if (await client.GetStudioModeEnabled() != originalStudio)
                    {
                        await client.SetStudioModeEnabled(originalStudio);
                    }

                    await client.SetCurrentSceneTransition(originalTransition.TransitionName);
                    if (originalTransition.TransitionDuration is { } duration)
                    {
                        await client.SetCurrentSceneTransitionDuration((float)duration);
                    }
                }
                catch (Exceptions.ObsResponseException exception)
                {
                    output.WriteLine($"Could not put back what the test changed: {exception.Message}");
                }
            }
        }

        [LiveUuidChangesFact]
        public async Task TheActivity_IsNotAskedFor_WhenTheModelIsNotAskedToFollowIt()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            Assert.Equal(ObsHighVolume.None, model.HighVolume);
            var kind = await live.ColorKindAsync();

            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("act"));
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("act-i"), kind, ColorSettings);
            await scene.SwitchToAsync();
            await Task.Delay(500);

            Assert.Null(input.IsActive);
            Assert.Null(input.IsShowing);

            // What OBS Studio says when it is asked.
            var asked = await input.GetActivityAsync();
            Assert.True(asked.VideoActive);
            Assert.True(asked.VideoShowing);
            await live.AssertModelEqualsObsAsync("after the input was put on the air, and not followed");
        }
    }
}
