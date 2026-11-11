namespace OBSStudioClient.Tests.Live
{
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// What the model does with the profiles and the scene collections against a real OBS Studio: it shows the profile that is current and the names of all in the order of OBS Studio, follows a switch that someone else
    /// makes, and the calls that switch, make and remove a profile, and that make and load a scene collection, return when the model shows it (for a collection: when the model has read it). The profiles that this makes are
    /// removed again and the profile and the scene collection that were current are put back. OBS Studio has no request that removes a scene collection: the one that this makes stays, in the copy of OBS Studio that the
    /// live runner makes for the run and deletes afterwards.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveProfilesModelTests(ITestOutputHelper output)
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

        private static async Task RestoreAsync(ObsClient client, string profile, string? made)
        {
            try
            {
                if ((await client.GetProfileList()).CurrentProfileName != profile)
                {
                    await client.SetCurrentProfile(profile);
                    await Task.Delay(1000);
                }

                if (made is not null && (await client.GetProfileList()).Profiles.Contains(made))
                {
                    await client.RemoveProfile(made);
                    await Task.Delay(500);
                }
            }
            catch (ObsClientException exception)
            {
                // Nothing more can be done for it here.
                _ = exception;
            }
        }

        [LiveUuidChangesFact]
        public async Task TheProfiles_AreShownAndFollowed_AndTheCallsReturnWhenTheModelShowsThem()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var client = live.Client;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            var list = await client.GetProfileList();
            var original = list.CurrentProfileName;
            var made = LiveObs.UniqueName("pa");

            // What OBS Studio holds, in its order.
            Assert.Equal(original, model.Profiles.Current);
            Assert.Equal(list.Profiles, model.Profiles.Names.ToArray());

            try
            {
                await model.Profiles.CreateAsync(made).WaitAsync(Wait);
                output.WriteLine($"made '{made}': current is '{model.Profiles.Current}', names {string.Join(", ", model.Profiles.Names)}");
                Assert.Equal(made, model.Profiles.Current);
                Assert.Contains(made, model.Profiles.Names);
                var afterCreate = await client.GetProfileList();
                Assert.Equal(made, afterCreate.CurrentProfileName);
                Assert.Equal(afterCreate.Profiles, model.Profiles.Names.ToArray());

                await model.Profiles.SetCurrentAsync(original).WaitAsync(Wait);
                Assert.Equal(original, model.Profiles.Current);
                Assert.Equal(original, (await client.GetProfileList()).CurrentProfileName);

                // Someone else switches (another client, or the user): the model follows.
                await client.SetCurrentProfile(made);
                await Model.ModelRig.EventuallyAsync(() => model.Profiles.Current == made, "the model to follow a switch of the profile", Wait);
                await model.Profiles.SetCurrentAsync(original).WaitAsync(Wait);

                // OBS Studio does nothing when the profile is current already.
                await model.Profiles.SetCurrentAsync(original).WaitAsync(Wait);
                Assert.Equal(original, model.Profiles.Current);

                // What it refuses, in the terms of the model. The current profile is not even asked for: OBS Studio would ask the user in its window.
                var taken = await Assert.ThrowsAsync<ObsNameInUseException>(() => model.Profiles.CreateAsync(made));
                Assert.Equal(made, taken.Name);
                var unknown = await Assert.ThrowsAsync<ObsNotFoundException>(() => model.Profiles.SetCurrentAsync(LiveObs.UniqueName("nope")));
                Assert.Equal("profile", unknown.Kind);
                await Assert.ThrowsAsync<InvalidOperationException>(() => model.Profiles.RemoveAsync(original));
                Assert.Equal(original, (await client.GetProfileList()).CurrentProfileName);

                await model.Profiles.RemoveAsync(made).WaitAsync(Wait);
                Assert.DoesNotContain(made, model.Profiles.Names);
                Assert.Equal(list.Profiles, model.Profiles.Names.ToArray());
                Assert.Equal((await client.GetProfileList()).Profiles, model.Profiles.Names.ToArray());
            }
            finally
            {
                await RestoreAsync(client, original, made);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheSceneCollections_AreShownAndFollowed_AndTheCallsReturnWhenTheModelHasReadThem()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var client = live.Client;
            var list = await client.GetSceneCollectionList();
            var original = list.CurrentSceneCollectionName;
            var made = LiveObs.UniqueName("ca");
            output.WriteLine($"OBS Studio {live.ObsVersionText}: the collection is '{original}', and '{made}' is made.");

            Assert.Equal(original, model.SceneCollections.Current);
            Assert.Equal(list.SceneCollections, model.SceneCollections.Names.ToArray());
            var originalScenes = model.Scenes.Select(scene => scene.Name).ToArray();
            var held = model.Scenes.FirstOrDefault();

            await model.SceneCollections.CreateAsync(made).WaitAsync(Wait);

            // The call returned when the model was complete: the collection is the one that OBS Studio loaded, with the one scene that it makes, and what the application held is of the collection that is left.
            Assert.Equal(ObsModelState.Synchronized, model.State);
            Assert.Equal(made, model.SceneCollections.Current);
            Assert.Contains(made, model.SceneCollections.Names);
            Assert.Equal(["Scene"], model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(made, (await client.GetSceneCollectionList()).CurrentSceneCollectionName);
            if (held is not null)
            {
                Assert.Equal(ObsObjectState.Gone, held.State);
            }

            var taken = await Assert.ThrowsAsync<ObsNameInUseException>(() => model.SceneCollections.CreateAsync(made));
            Assert.Equal(made, taken.Name);

            await model.SceneCollections.SetCurrentAsync(original).WaitAsync(Wait);
            Assert.Equal(ObsModelState.Synchronized, model.State);
            Assert.Equal(original, model.SceneCollections.Current);
            Assert.Equal(originalScenes, model.Scenes.Select(scene => scene.Name).ToArray());

            // Nothing happens when the collection is loaded already.
            await model.SceneCollections.SetCurrentAsync(original).WaitAsync(Wait);

            // Someone else loads the other one: the model follows, and has read it when it says so.
            await client.SetCurrentSceneCollection(made);
            await Model.ModelRig.EventuallyAsync(() => model.SceneCollections.Current == made && model.State == ObsModelState.Synchronized, "the model to follow a switch of the collection", Wait);
            await model.SceneCollections.SetCurrentAsync(original).WaitAsync(Wait);
            Assert.Equal(original, model.SceneCollections.Current);

            var differences = await live.DifferencesAsync();
            Assert.True(differences.Count == 0, string.Join("; ", differences));
            Assert.Equal((await client.GetSceneCollectionList()).SceneCollections, model.SceneCollections.Names.ToArray());
        }
    }
}
