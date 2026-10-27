namespace OBSStudioClient.Tests.TestAppTests
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.TestApp.Catalog;
    using System.Text.Json;

    /// <summary>
    /// The test app hands what the user entered to the generated catalog through an <see cref="ArgumentSet"/>; these tests check
    /// that it reads every kind of value back the way the catalog asks for it.
    /// </summary>
    public class ArgumentSetTests
    {
        private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

        [Fact]
        public void ARequiredValue_ThatWasNotEntered_IsRefused()
        {
            var arguments = new ArgumentSet();

            var exception = Assert.Throws<ArgumentException>(() => arguments.Text("name"));
            Assert.Contains("'name'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AValueOfAnotherType_IsRefused()
        {
            var arguments = new ArgumentSet();
            arguments.Set("count", "one");

            Assert.Throws<ArgumentException>(() => arguments.Int("count"));
        }

        [Fact]
        public void AnOptionalValue_ThatWasNotEntered_ReadsAsNull()
        {
            var arguments = new ArgumentSet();

            Assert.Null(arguments.OptionalText("a"));
            Assert.Null(arguments.OptionalInt("b"));
            Assert.Null(arguments.OptionalFloat("c"));
            Assert.Null(arguments.OptionalBool("d"));
            Assert.Null(arguments.OptionalUuid("e"));
            Assert.Null(arguments.OptionalScene("f"));
            Assert.Null(arguments.OptionalInput("g"));
            Assert.Null(arguments.OptionalSource("h"));
            Assert.Null(arguments.OptionalMap("i"));
            Assert.Null(arguments.OptionalElement("j"));
            Assert.Null(arguments.OptionalValue("k"));
            Assert.Null(arguments.OptionalJson<AudioTracks>("l"));
            Assert.Null(arguments.OptionalEnumValue<MixType>("m"));
        }

        [Fact]
        public void TheSimpleValues_AreReadBackAsEntered()
        {
            var uuid = Guid.NewGuid();
            var arguments = new ArgumentSet();
            arguments.Set("text", "hello");
            arguments.Set("whole", 42);
            arguments.Set("number", 0.5f);
            arguments.Set("flag", true);
            arguments.Set("uuid", uuid);
            arguments.Set("mix", MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW);

            Assert.Equal("hello", arguments.Text("text"));
            Assert.Equal(42, arguments.Int("whole"));
            Assert.Equal(0.5f, arguments.Float("number"));
            Assert.True(arguments.Bool("flag"));
            Assert.Equal(uuid, arguments.Uuid("uuid"));
            Assert.Equal(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW, arguments.EnumValue<MixType>("mix"));
            Assert.Equal(42, arguments.OptionalInt("whole"));
            Assert.Equal(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW, arguments.OptionalEnumValue<MixType>("mix"));
        }

        [Fact]
        public void AScene_IsReadBackByNameOrByUuid()
        {
            var uuid = Guid.NewGuid();
            var arguments = new ArgumentSet();
            arguments.Set("byName", new ReferenceValue("Game", null));
            arguments.Set("byUuid", new ReferenceValue(null, uuid));

            Assert.Equal(SceneRef.FromName("Game"), arguments.Scene("byName"));
            Assert.Equal(SceneRef.FromUuid(uuid), arguments.Scene("byUuid"));
            Assert.Equal(InputRef.FromName("Game"), arguments.Input("byName"));
            Assert.Equal(SourceRef.FromUuid(uuid), arguments.Source("byUuid"));
            Assert.Equal(SceneRef.FromName("Game"), arguments.OptionalScene("byName"));
        }

        [Fact]
        public void AJsonObject_IsReadBackAsAMap_AndAsATypedObject()
        {
            var arguments = new ArgumentSet();
            arguments.Set("settings", Json("""{"volume": 3, "name": "x"}"""));
            arguments.Set("transform", Json("""{"positionX": 10, "rotation": 90}"""));
            arguments.Set("tracks", Json("""{"1": true, "2": false, "3": true, "4": false, "5": false, "6": true}"""));

            var map = arguments.Map("settings");
            Assert.Equal(2, map.Count);
            Assert.Equal(3, ((JsonElement)map["volume"]).GetInt32());

            var transform = arguments.Json<SceneItemTransformUpdate>("transform");
            Assert.Equal(10f, transform.PositionX);
            Assert.Equal(90f, transform.Rotation);
            Assert.Null(transform.PositionY);

            var tracks = arguments.Json<AudioTracks>("tracks");
            Assert.True(tracks.Track1);
            Assert.False(tracks.Track2);
            Assert.True(tracks.Track6);
        }

        [Fact]
        public void AnyJsonValue_IsReadBackAsAnElement()
        {
            var arguments = new ArgumentSet();
            arguments.Set("data", Json("""[1, 2, 3]"""));

            Assert.Equal(JsonValueKind.Array, arguments.Element("data").ValueKind);
            Assert.Equal(JsonValueKind.Array, ((JsonElement)arguments.Value("data")).ValueKind);
        }

        [Fact]
        public void AJsonValueThatIsNotTheTypeItIsReadAs_IsRefusedWithAMessageAboutThatValue()
        {
            var arguments = new ArgumentSet();
            arguments.Set("settings", Json("[1, 2]"));

            var exception = Assert.Throws<ArgumentException>(() => arguments.Map("settings"));
            Assert.Contains("'settings'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TheValues_AreDescribedForTheStatusLine()
        {
            var arguments = new ArgumentSet();
            arguments.Set("name", "x");
            arguments.Set("scene", new ReferenceValue("Game", null));
            arguments.Set("count", 2);

            Assert.Equal("name: \"x\", scene: \"Game\", count: 2", arguments.ToString());
        }
    }
}
