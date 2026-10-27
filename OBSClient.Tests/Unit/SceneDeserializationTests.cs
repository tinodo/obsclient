namespace OBSStudioClient.Tests.Unit
{
    using System.Text.Json;
    using Xunit;

    /// <summary>
    /// Direct unit tests for <see cref="Classes.Scene"/> JSON deserialization, covering the main-canvas
    /// (ordered, non-null sceneIndex) and non-main-canvas (unordered, null sceneIndex) shapes OBS sends.
    /// </summary>
    public class SceneDeserializationTests
    {
        [Fact]
        public void Scene_FromMainCanvas_DeserializesWithIndexAndUuid()
        {
            var sceneUuid = Guid.NewGuid();
            var json = $$"""{"sceneName":"Gaming Scene","sceneUuid":"{{sceneUuid}}","sceneIndex":2}""";

            var scene = JsonSerializer.Deserialize<Classes.Scene>(json);

            Assert.NotNull(scene);
            Assert.Equal("Gaming Scene", scene!.SceneName);
            Assert.Equal(sceneUuid, scene.SceneUuid);
            Assert.Equal(2, scene.SceneIndex);
        }

        [Fact]
        public void Scene_FromNonMainCanvas_DeserializesWithNullIndex()
        {
            var sceneUuid = Guid.NewGuid();
            var json = $$"""{"sceneName":"Vertical Scene","sceneUuid":"{{sceneUuid}}","sceneIndex":null}""";

            var scene = JsonSerializer.Deserialize<Classes.Scene>(json);

            Assert.NotNull(scene);
            Assert.Null(scene!.SceneIndex);
        }
    }
}
