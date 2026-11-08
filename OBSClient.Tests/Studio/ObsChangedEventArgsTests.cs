namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio;

    public class ObsChangedEventArgsTests
    {
        [Fact]
        public async Task TheTextOfAChange_ShowsAListByTheNamesInIt_AndNotByTheNameOfItsType()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddInput("Mic"));
            var created = Guid.NewGuid();
            rig.Responder.Change(world =>
            {
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
                world.AddItem(created, world.Inputs[0].Uuid, "Mic");
            });

            await rig.Responder.SendEventAsync("SceneCreated", new { sceneName = "New", sceneUuid = created, isGroup = false }, 4);
            await StudioRig.EventuallyAsync(() => rig.Changes.Any(change => change.PropertyName == "Scenes"), "the change of the list of scenes");
            await StudioRig.EventuallyAsync(() => rig.Changes.Any(change => change.PropertyName == "Items" && change.Entity is ObsScene { Name: "New" }), "the change of the scene items of the new scene");

            var scenes = rig.Changes.First(change => change.PropertyName == "Scenes").ToString();
            var items = rig.Changes.First(change => change.PropertyName == "Items" && change.Entity is ObsScene { Name: "New" }).ToString();

            Assert.Equal("the studio: Scenes [Scene] -> [New, Scene]", scenes);
            Assert.Equal("scene 'New': Items [] -> [Mic]", items);
        }

        [Fact]
        public async Task TheTextOfAChange_SaysThatThereWasNothing_WhenAValueWasNull()
        {
            await using var rig = await StudioRig.ConnectedAsync();
            var change = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, null, nameof(ObsStudio.ProgramScene), null, rig.Studio.ProgramScene);

            Assert.Equal("the studio: ProgramScene (nothing) -> Scene 'Scene'", change.ToString());
        }
    }
}
