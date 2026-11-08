namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>The JSON of what a simulated OBS Studio reports: the lists of requests and the payloads of events.</summary>
    internal sealed partial class SimWorld
    {
        /// <summary>
        /// Lists the scenes. A list that is read (GetSceneList) lags (fact 4): a scene is not in it until its lag after its creation, and
        /// is still in it until its lag after its removal. The list in an event (SceneListChanged) holds the new scenes at once, and the
        /// removed scenes only with <see cref="SimQuirks.StaleSceneListChangedPayload"/> (fact 10).
        /// </summary>
        /// <param name="forEvent">Whether the list is that of an event.</param>
        public JsonArray SceneListJson(bool forEvent)
        {
            var now = Now;
            var stale = _obs.Quirks.StaleSceneListChangedPayload;
            var list = new JsonArray();
            var index = 0;
            foreach (var scene in _uiList)
            {
                bool listed;
                if (forEvent)
                {
                    listed = !scene.Removed || (stale && now < scene.RemovedAt!.Value + scene.Lag);
                }
                else
                {
                    listed = now >= scene.CreatedAt + scene.Lag && (!scene.Removed || now < scene.RemovedAt!.Value + scene.Lag);
                }

                if (listed)
                {
                    list.Add(Obj(("sceneIndex", index++), ("sceneName", scene.Name), ("sceneUuid", scene.UuidText)));
                }
            }

            return list;
        }

        /// <summary>Lists the items of a scene, in the order of their index.</summary>
        /// <param name="scene">The scene.</param>
        /// <param name="basic">Whether to list only the ID and the index of each item, as SceneItemListReindexed does.</param>
        public JsonArray ItemListJson(SimScene scene, bool basic = false)
        {
            var list = new JsonArray();
            foreach (var item in scene.Items)
            {
                list.Add(ItemJson(item, basic));
            }

            return list;
        }

        /// <summary>Lists the inputs that are alive (GetInputList), in the order of their creation.</summary>
        /// <param name="kind">The kind to list, or null to list all.</param>
        public JsonArray InputListJson(string? kind)
        {
            var list = new JsonArray();
            foreach (var input in _inputs)
            {
                if (kind is not null && !string.Equals(input.Kind.Id, kind, StringComparison.Ordinal))
                {
                    continue;
                }

                list.Add(Obj(
                    ("inputKind", input.Kind.Id),
                    ("inputKindCaps", input.Kind.Caps),
                    ("inputName", input.Name),
                    ("inputUuid", input.UuidText),
                    ("unversionedInputKind", input.Kind.UnversionedId)));
            }

            return list;
        }

        private static JsonObject ItemJson(SimItem item, bool basic)
        {
            if (basic)
            {
                return Obj(("sceneItemId", item.Id), ("sceneItemIndex", item.Index));
            }

            var source = item.Source;
            return Obj(
                ("inputKind", source is SimInput input ? JsonValue.Create(input.Kind.Id) : null),
                ("isGroup", source.IsScene ? JsonValue.Create(false) : null),
                ("sceneItemBlendMode", "OBS_BLEND_NORMAL"),
                ("sceneItemEnabled", item.Enabled),
                ("sceneItemId", item.Id),
                ("sceneItemIndex", item.Index),
                ("sceneItemLocked", item.Locked),
                ("sceneItemTransform", TransformJson(source)),
                ("sourceName", source.Name),
                ("sourceType", source.IsScene ? "OBS_SOURCE_TYPE_SCENE" : "OBS_SOURCE_TYPE_INPUT"),
                ("sourceUuid", source.UuidText));
        }

        // The transform of an item that was not changed. The size of a source is that of the canvas for a scene, the width and height of its
        // settings for a color source, and not known for the other kinds (their size depends on what they show).
        private static JsonObject TransformJson(SimSource source)
        {
            var (width, height) = SourceSize(source);
            return Obj(
                ("alignment", 5),
                ("boundsAlignment", 0),
                ("boundsHeight", 0.0),
                ("boundsType", "OBS_BOUNDS_NONE"),
                ("boundsWidth", 0.0),
                ("cropBottom", 0),
                ("cropLeft", 0),
                ("cropRight", 0),
                ("cropToBounds", false),
                ("cropTop", 0),
                ("height", height),
                ("positionX", 0.0),
                ("positionY", 0.0),
                ("rotation", 0.0),
                ("scaleX", 1.0),
                ("scaleY", 1.0),
                ("sourceHeight", height),
                ("sourceWidth", width),
                ("width", width));
        }

        private static (double Width, double Height) SourceSize(SimSource source)
        {
            if (source is SimScene)
            {
                return (1920.0, 1080.0);
            }

            if (source is SimInput { Kind.Id: "color_source_v3" } input)
            {
                var defaults = input.Kind.MakeDefaultSettings();
                return (ReadSize(input.Settings, defaults, "width"), ReadSize(input.Settings, defaults, "height"));
            }

            return (0.0, 0.0);
        }

        private static double ReadSize(JsonObject settings, JsonObject defaults, string name)
        {
            return SimJson.TryGetNumber(settings[name] ?? defaults[name], out var size) ? size : 0.0;
        }
    }
}
