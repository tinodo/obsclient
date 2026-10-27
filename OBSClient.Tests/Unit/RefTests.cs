namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;

    /// <summary>
    /// <see cref="SceneRef"/>, <see cref="InputRef"/> and <see cref="SourceRef"/> identify an object by name or by UUID, and
    /// convert from what a caller has at hand.
    /// </summary>
    public class RefTests
    {
        [Fact]
        public void SceneRef_IdentifiesByNameOrByUuid() =>
            AssertBehaves<SceneRef>(SceneRef.FromName, SceneRef.FromUuid, name => name, uuid => uuid, @ref => @ref.Name, @ref => @ref.Uuid, @ref => @ref.IsSpecified, (@ref, parameter) => @ref.EnsureSpecified(parameter), default);

        [Fact]
        public void InputRef_IdentifiesByNameOrByUuid() =>
            AssertBehaves<InputRef>(InputRef.FromName, InputRef.FromUuid, name => name, uuid => uuid, @ref => @ref.Name, @ref => @ref.Uuid, @ref => @ref.IsSpecified, (@ref, parameter) => @ref.EnsureSpecified(parameter), default);

        [Fact]
        public void SourceRef_IdentifiesByNameOrByUuid() =>
            AssertBehaves<SourceRef>(SourceRef.FromName, SourceRef.FromUuid, name => name, uuid => uuid, @ref => @ref.Name, @ref => @ref.Uuid, @ref => @ref.IsSpecified, (@ref, parameter) => @ref.EnsureSpecified(parameter), default);

        [Fact]
        public void AScene_IsIdentifiedByItsUuid()
        {
            var scene = new Scene(0, "Game", Guid.NewGuid());

            SceneRef reference = scene;

            Assert.Equal(SceneRef.FromUuid(scene.SceneUuid), reference);
        }

        [Fact]
        public void ASceneWithoutAUuid_IsIdentifiedByItsName()
        {
            var scene = new Scene(0, "Game", Guid.Empty);

            SceneRef reference = scene;

            Assert.Equal(SceneRef.FromName("Game"), reference);
        }

        [Fact]
        public void AnInput_IsIdentifiedByItsUuid_WhenObsStudioReportedOne()
        {
            var uuid = Guid.NewGuid();
            var input = new Input("kind", "Microphone", "kind") { InputUuid = uuid };

            InputRef reference = input;

            Assert.Equal(InputRef.FromUuid(uuid), reference);
        }

        [Fact]
        public void AnInput_IsIdentifiedByItsName_WhenObsStudioReportedNoUuid()
        {
            var input = new Input("kind", "Microphone", "kind");

            InputRef reference = input;

            Assert.Equal(InputRef.FromName("Microphone"), reference);
        }

        [Fact]
        public void ASource_CanBeAnInput_AScene_OrWhatASceneItemShows()
        {
            var uuid = Guid.NewGuid();
            var input = new Input("kind", "Microphone", "kind") { InputUuid = uuid };
            var scene = new Scene(0, "Game", Guid.NewGuid());
            var item = new SceneItem(null, null, BlendMode.OBS_BLEND_NORMAL, true, 1, 0, false, Transform(), "Webcam", SourceType.OBS_SOURCE_TYPE_INPUT) { SourceUuid = uuid };
            var itemWithoutUuid = new SceneItem(null, null, BlendMode.OBS_BLEND_NORMAL, true, 2, 1, false, Transform(), "Webcam", SourceType.OBS_SOURCE_TYPE_INPUT);

            SourceRef fromInput = input;
            SourceRef fromScene = scene;
            SourceRef fromItem = item;
            SourceRef fromItemWithoutUuid = itemWithoutUuid;
            SourceRef fromInputRef = InputRef.FromName("Microphone");
            SourceRef fromSceneRef = SceneRef.FromUuid(scene.SceneUuid);

            Assert.Equal(SourceRef.FromUuid(uuid), fromInput);
            Assert.Equal(SourceRef.FromUuid(scene.SceneUuid), fromScene);
            Assert.Equal(SourceRef.FromUuid(uuid), fromItem);
            Assert.Equal(SourceRef.FromName("Webcam"), fromItemWithoutUuid);
            Assert.Equal(SourceRef.FromName("Microphone"), fromInputRef);
            Assert.Equal(SourceRef.FromUuid(scene.SceneUuid), fromSceneRef);
        }

        [Fact]
        public void ASourceFromAnUnspecifiedRef_IsUnspecified()
        {
            SourceRef fromInputRef = default(InputRef);
            SourceRef fromSceneRef = default(SceneRef);

            Assert.False(fromInputRef.IsSpecified);
            Assert.False(fromSceneRef.IsSpecified);
        }

        [Fact]
        public void ANullObject_ConvertsToNoRef()
        {
            Assert.Throws<ArgumentNullException>(() => (SceneRef)(Scene)null!);
            Assert.Throws<ArgumentNullException>(() => (InputRef)(Input)null!);
            Assert.Throws<ArgumentNullException>(() => (SourceRef)(SceneItem)null!);
        }

        // The three types are written alike, so what is expected of one is expected of each.
        private static void AssertBehaves<T>(
            Func<string, T> fromName,
            Func<Guid, T> fromUuid,
            Func<string, T> convertName,
            Func<Guid, T> convertUuid,
            Func<T, string?> name,
            Func<T, Guid?> uuid,
            Func<T, bool> isSpecified,
            Action<T, string> ensureSpecified,
            T unspecified)
            where T : struct, IEquatable<T>
        {
            var id = Guid.NewGuid();
            var byName = fromName("Game");
            var byUuid = fromUuid(id);

            Assert.Equal("Game", name(byName));
            Assert.Null(uuid(byName));
            Assert.True(isSpecified(byName));

            Assert.Null(name(byUuid));
            Assert.Equal(id, uuid(byUuid));
            Assert.True(isSpecified(byUuid));

            // A name and a UUID convert implicitly.
            Assert.Equal(byName, convertName("Game"));
            Assert.Equal(byUuid, convertUuid(id));

            // The same name or the same UUID is the same reference; names are case sensitive, like OBS Studio's.
            Assert.Equal(byName.GetHashCode(), fromName("Game").GetHashCode());
            Assert.NotEqual(byName, fromName("game"));
            Assert.NotEqual(byName, byUuid);
            Assert.NotEqual(byUuid, fromUuid(Guid.NewGuid()));

            Assert.Equal("Game", byName.ToString());
            Assert.Equal(id.ToString(), byUuid.ToString());
            Assert.Equal(string.Empty, unspecified.ToString());

            // Nothing identifies nothing.
            Assert.False(isSpecified(unspecified));
            var refusal = Assert.Throws<ArgumentException>(() => ensureSpecified(unspecified, "parameter"));
            Assert.Equal("parameter", refusal.ParamName);
            ensureSpecified(byName, "parameter");
            ensureSpecified(byUuid, "parameter");

            Assert.Throws<ArgumentNullException>(() => fromName(null!));
            Assert.Throws<ArgumentException>(() => fromName(string.Empty));
            Assert.Throws<ArgumentException>(() => fromUuid(Guid.Empty));
        }

        private static SceneItemTransform Transform() =>
            new(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 1, 2, 3, 4, 100f, 10f, 20f, 30f, 1.5f, 2.5f, 100f, 100f, 100f);
    }
}
