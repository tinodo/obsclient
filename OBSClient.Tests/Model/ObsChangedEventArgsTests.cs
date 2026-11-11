namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;

    public class ObsChangedEventArgsTests
    {
        [Fact]
        public async Task TheTextOfAChange_ShowsAListByTheNamesInIt_AndNotByTheNameOfItsType()
        {
            await using var rig = await ModelRig.ConnectedAsync(world => world.AddInput("Mic"));
            var created = Guid.NewGuid();
            rig.Responder.Change(world =>
            {
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
                world.AddItem(created, world.Inputs[0].Uuid, "Mic");
            });

            await rig.Responder.SendEventAsync("SceneCreated", new { sceneName = "New", sceneUuid = created, isGroup = false }, 4);
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => change.PropertyName == "Scenes"), "the change of the list of scenes");
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => change.PropertyName == "Items" && change.Entity is ObsScene { Name: "New" }), "the change of the scene items of the new scene");

            var scenes = rig.Changes.First(change => change.PropertyName == "Scenes").ToString();
            var items = rig.Changes.First(change => change.PropertyName == "Items" && change.Entity is ObsScene { Name: "New" }).ToString();

            Assert.Equal("the model: Scenes [Scene] -> [New, Scene]", scenes);
            Assert.Equal("scene 'New': Items [] -> [Mic]", items);
        }

        [Fact]
        public async Task TheTextOfAChange_SaysThatThereWasNothing_WhenAValueWasNull()
        {
            await using var rig = await ModelRig.ConnectedAsync();
            var change = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, null, nameof(ObsModel.ProgramScene), null, rig.Model.ProgramScene);

            Assert.Equal("the model: ProgramScene (nothing) -> Scene 'Scene'", change.ToString());
        }

        [Fact]
        public async Task TheTextOfAChangeOfSettings_ShowsTheJson_CutOffWhenItIsLong()
        {
            await using var rig = await ModelRig.ConnectedAsync(world => world.AddInput("Backdrop"));
            var backdrop = rig.Model.Inputs["Backdrop"];
            var small = System.Text.Json.JsonDocument.Parse("""{"width":640}""").RootElement;
            var long_ = System.Text.Json.JsonDocument.Parse("{\"text\":\"" + new string('x', 300) + "\"}").RootElement;

            var changed = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, backdrop, nameof(ObsInput.Settings), small, long_).ToString();
            var nothing = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, backdrop, nameof(ObsInput.Settings), default(System.Text.Json.JsonElement), small).ToString();

            Assert.StartsWith("""input 'Backdrop': Settings {"width":640} -> {"text":"xxx""", changed);
            Assert.EndsWith("...", changed);
            Assert.True(changed.Length < 200);
            Assert.Equal("""input 'Backdrop': Settings (nothing) -> {"width":640}""", nothing);
        }
    }
}
