namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// The requests that the simulation answers, as obs-websocket 5.7.6 answers them: in the same order of checks, with the same statuses and
    /// comments, and with the response data of a real OBS Studio (the captures of the tests). Every other request is answered with 204.
    /// </summary>
    internal sealed class SimRequests
    {
        private static readonly string[] ImageFormats =
        [
            "bmp", "cur", "icns", "ico", "jfif", "jpeg", "jpg", "pbm", "pgm", "png", "ppm", "tif", "tiff", "wbmp", "webp", "xbm", "xpm",
        ];

        private static readonly Dictionary<string, Func<SimRequests, SimRequest, SimResult>> Handlers = new(StringComparer.Ordinal)
        {
            ["GetVersion"] = static (self, request) => self.GetVersion(request),
            ["GetSceneList"] = static (self, request) => self.GetSceneList(request),
            ["GetGroupList"] = static (self, request) => self.GetGroupList(request),
            ["GetInputList"] = static (self, request) => self.GetInputList(request),
            ["GetInputKindList"] = static (self, request) => self.GetInputKindList(request),
            ["GetSceneItemList"] = static (self, request) => self.GetSceneItemList(request),
            ["GetSceneItemId"] = static (self, request) => self.GetSceneItemId(request),
            ["GetSceneItemSource"] = static (self, request) => self.GetSceneItemSource(request),
            ["GetSceneItemEnabled"] = static (self, request) => self.GetSceneItemEnabled(request),
            ["GetSceneItemLocked"] = static (self, request) => self.GetSceneItemLocked(request),
            ["GetSceneItemIndex"] = static (self, request) => self.GetSceneItemIndex(request),
            ["GetCurrentProgramScene"] = static (self, request) => self.GetCurrentProgramScene(request),
            ["GetCurrentPreviewScene"] = static (self, request) => self.GetCurrentPreviewScene(request),
            ["GetStudioModeEnabled"] = static (self, request) => self.GetStudioModeEnabled(request),
            ["GetInputMute"] = static (self, request) => self.GetInputMute(request),
            ["GetInputVolume"] = static (self, request) => self.GetInputVolume(request),
            ["GetInputSettings"] = static (self, request) => self.GetInputSettings(request),
            ["GetInputAudioBalance"] = static (self, request) => self.GetInputAudioBalance(request),
            ["GetInputAudioSyncOffset"] = static (self, request) => self.GetInputAudioSyncOffset(request),
            ["GetInputAudioMonitorType"] = static (self, request) => self.GetInputAudioMonitorType(request),
            ["GetInputAudioTracks"] = static (self, request) => self.GetInputAudioTracks(request),
            ["SetInputSettings"] = static (self, request) => self.SetInputSettings(request),
            ["SetInputAudioBalance"] = static (self, request) => self.SetInputAudioBalance(request),
            ["SetInputAudioSyncOffset"] = static (self, request) => self.SetInputAudioSyncOffset(request),
            ["SetInputAudioMonitorType"] = static (self, request) => self.SetInputAudioMonitorType(request),
            ["SetInputAudioTracks"] = static (self, request) => self.SetInputAudioTracks(request),
            ["CreateScene"] = static (self, request) => self.CreateScene(request),
            ["RemoveScene"] = static (self, request) => self.RemoveScene(request),
            ["SetSceneName"] = static (self, request) => self.SetSceneName(request),
            ["SetCurrentProgramScene"] = static (self, request) => self.SetCurrentProgramScene(request),
            ["CreateInput"] = static (self, request) => self.CreateInput(request),
            ["RemoveInput"] = static (self, request) => self.RemoveInput(request),
            ["SetInputName"] = static (self, request) => self.SetInputName(request),
            ["SetInputMute"] = static (self, request) => self.SetInputMute(request),
            ["ToggleInputMute"] = static (self, request) => self.ToggleInputMute(request),
            ["SetInputVolume"] = static (self, request) => self.SetInputVolume(request),
            ["CreateSceneItem"] = static (self, request) => self.CreateSceneItem(request),
            ["RemoveSceneItem"] = static (self, request) => self.RemoveSceneItem(request),
            ["SetSceneItemEnabled"] = static (self, request) => self.SetSceneItemEnabled(request),
            ["SetSceneItemLocked"] = static (self, request) => self.SetSceneItemLocked(request),
            ["SetSceneItemIndex"] = static (self, request) => self.SetSceneItemIndex(request),
            ["GetSceneCollectionList"] = static (self, request) => self.GetSceneCollectionList(request),
            ["SetCurrentSceneCollection"] = static (self, request) => self.SetCurrentSceneCollection(request),
            ["CreateSceneCollection"] = static (self, request) => self.CreateSceneCollection(request),
        };

        private readonly SimObs _obs;
        private SimWorld _world;

        public SimRequests(SimObs obs, SimWorld world)
        {
            _obs = obs;
            _world = world;
        }

        /// <summary>Makes the requests work on another scene collection: the one that OBS Studio has loaded now.</summary>
        internal void Use(SimWorld world)
        {
            _world = world;
        }

        /// <summary>Gets the types of the requests that the simulation answers, in alphabetical order. <c>GetVersion</c> lists exactly these.</summary>
        public static IReadOnlyList<string> Implemented { get; } = Handlers.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        /// <summary>Runs a request (RequestHandler::ProcessRequest).</summary>
        public SimResult Handle(string requestType, SimRequest request)
        {
            if (requestType.Length == 0)
            {
                return SimResult.Fail(SimStatus.MissingRequestType, "Your request's `requestType` may not be empty.");
            }

            if (!Handlers.TryGetValue(requestType, out var handler))
            {
                return SimResult.Fail(SimStatus.UnknownRequestType, "Your request type is not valid.");
            }

            try
            {
                return handler(this, request);
            }
            catch (SimFailure failure)
            {
                return SimResult.Fail(failure);
            }
            catch (SimHang)
            {
                return SimResult.Hung;
            }
        }

        private static SimResult Failed(SimRequest request) => SimResult.Fail(request.Failure!);

        // Searches the items of a scene for a source by name, from the bottom: skips as many matches as the offset says, and for an offset
        // of -1 takes the last match, which is the one on top (SearchHelper::GetSceneItemByName).
        private static SimItem? SearchItem(SimScene scene, string name, int offset)
        {
            SimItem? found = null;
            foreach (var item in scene.Items)
            {
                if (!string.Equals(item.Source.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (offset > 0)
                {
                    offset--;
                    continue;
                }

                found = item;
                if (offset == 0)
                {
                    break;
                }
            }

            return found;
        }

        private SimResult GetVersion(SimRequest request)
        {
            return SimResult.Ok(Obj(
                ("availableRequests", Arr(Implemented.Select(name => (JsonNode?)name))),
                ("obsVersion", "33.0.0"),
                ("obsWebSocketVersion", "5.7.6"),
                ("platform", "windows"),
                ("platformDescription", "Windows 11 Version 26H2"),
                ("rpcVersion", 1),
                ("supportedImageFormats", Arr(ImageFormats.Select(format => (JsonNode?)format)))));
        }

        private SimResult GetSceneList(SimRequest request)
        {
            if (!request.AcquireCanvas())
            {
                return Failed(request);
            }

            // The program scene is read at once, and the list of scenes lags (facts 2 and 4). There is no preview scene without studio mode.
            return SimResult.Ok(Obj(
                ("currentPreviewSceneName", null),
                ("currentPreviewSceneUuid", null),
                ("currentProgramSceneName", _world.Program.Name),
                ("currentProgramSceneUuid", _world.Program.UuidText),
                ("scenes", _world.SceneListJson(forEvent: false))));
        }

        private SimResult GetGroupList(SimRequest request)
        {
            if (!request.AcquireCanvas())
            {
                return Failed(request);
            }

            return SimResult.Ok(Obj(("groups", new JsonArray())));
        }

        private SimResult GetInputList(SimRequest request)
        {
            string? kind = null;
            if (request.Contains("inputKind"))
            {
                if (!request.OptionalString("inputKind", out var value))
                {
                    return Failed(request);
                }

                kind = value;
            }

            return SimResult.Ok(Obj(("inputs", _world.InputListJson(kind))));
        }

        private SimResult GetInputKindList(SimRequest request)
        {
            var unversioned = false;
            if (request.Contains("unversioned") && !request.OptionalBoolean("unversioned", out unversioned))
            {
                return Failed(request);
            }

            return SimResult.Ok(Obj(("inputKinds", Arr(SimInputKinds.All.Select(kind => (JsonNode?)(unversioned ? kind.UnversionedId : kind.Id))))));
        }

        private SimResult GetSceneItemList(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null)
            {
                return Failed(request);
            }

            return SimResult.Ok(Obj(("sceneItems", _world.ItemListJson(scene))));
        }

        private SimResult GetSceneItemId(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null || !request.RequireString("sourceName", out var sourceName))
            {
                return Failed(request);
            }

            var offset = 0;
            if (request.Contains("searchOffset"))
            {
                if (!request.OptionalNumber("searchOffset", out var number, min: -1))
                {
                    return Failed(request);
                }

                offset = (int)number;
            }

            var item = SearchItem(scene, sourceName, offset);
            if (item is null)
            {
                return SimResult.Fail(SimStatus.ResourceNotFound, "No scene items were found in the specified scene by that name or offset.");
            }

            return SimResult.Ok(Obj(("sceneItemId", item.Id)));
        }

        private SimResult GetSceneItemSource(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null)
            {
                return Failed(request);
            }

            return SimResult.Ok(Obj(("sourceName", item.Source.Name), ("sourceUuid", item.Source.UuidText)));
        }

        private SimResult GetSceneItemEnabled(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            return item is null ? Failed(request) : SimResult.Ok(Obj(("sceneItemEnabled", item.Enabled)));
        }

        private SimResult GetSceneItemLocked(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            return item is null ? Failed(request) : SimResult.Ok(Obj(("sceneItemLocked", item.Locked)));
        }

        private SimResult GetSceneItemIndex(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            return item is null ? Failed(request) : SimResult.Ok(Obj(("sceneItemIndex", item.Index)));
        }

        private SimResult GetCurrentProgramScene(SimRequest request)
        {
            var program = _world.Program;
            return SimResult.Ok(Obj(
                ("currentProgramSceneName", program.Name),
                ("currentProgramSceneUuid", program.UuidText),
                ("sceneName", program.Name),
                ("sceneUuid", program.UuidText)));
        }

        private SimResult GetCurrentPreviewScene(SimRequest request)
        {
            // Studio mode is not simulated, so there is never a preview scene.
            return SimResult.Fail(SimStatus.StudioModeNotActive);
        }

        private SimResult GetStudioModeEnabled(SimRequest request)
        {
            return SimResult.Ok(Obj(("studioModeEnabled", false)));
        }

        private SimResult GetInputMute(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            return SimResult.Ok(Obj(("inputMuted", input.Muted)));
        }

        private SimResult GetInputVolume(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            return SimResult.Ok(Obj(("inputVolumeDb", SimWorld.MulToDb(input.Volume)), ("inputVolumeMul", (double)input.Volume)));
        }

        private SimResult GetInputSettings(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            return SimResult.Ok(Obj(("inputKind", input.Kind.Id), ("inputSettings", input.Settings.DeepClone())));
        }

        private SimResult GetInputAudioBalance(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            return SimResult.Ok(Obj(("inputAudioBalance", (double)input.Balance)));
        }

        private SimResult GetInputAudioSyncOffset(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            return SimResult.Ok(Obj(("inputAudioSyncOffset", input.SyncOffset)));
        }

        private SimResult GetInputAudioMonitorType(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            return SimResult.Ok(Obj(("monitorType", input.MonitorType)));
        }

        private SimResult GetInputAudioTracks(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            return SimResult.Ok(Obj(("inputAudioTracks", SimWorld.TracksJson(input.Tracks))));
        }

        private SimResult SetInputSettings(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.ValidateBasic("inputSettings") || !request.OptionalObject("inputSettings", out var settings, allowEmpty: true))
            {
                return Failed(request);
            }

            var overlay = true;
            if (request.Contains("overlay") && !request.OptionalBoolean("overlay", out overlay))
            {
                return Failed(request);
            }

            _world.SetSettings(input, settings, overlay);
            return SimResult.Ok();
        }

        private SimResult SetInputAudioBalance(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireNumber("inputAudioBalance", out var balance, 0.0, 1.0))
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            _world.SetBalance(input, (float)balance);
            return SimResult.Ok();
        }

        private SimResult SetInputAudioSyncOffset(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireNumber("inputAudioSyncOffset", out var offset, -950, 20000))
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            _world.SetSyncOffset(input, (int)offset);
            return SimResult.Ok();
        }

        private static readonly string[] MonitorTypes = ["OBS_MONITORING_TYPE_NONE", "OBS_MONITORING_TYPE_MONITOR_ONLY", "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT"];

        private SimResult SetInputAudioMonitorType(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireString("monitorType", out var type))
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            if (!MonitorTypes.Contains(type))
            {
                return SimResult.Fail(SimStatus.InvalidRequestField, $"Unknown monitor type: {type}");
            }

            _world.SetMonitorType(input, type);
            return SimResult.Ok();
        }

        private SimResult SetInputAudioTracks(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.ValidateBasic("inputAudioTracks") || !request.OptionalObject("inputAudioTracks", out var given, allowEmpty: false))
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            var tracks = input.Tracks;
            for (var track = 1; track <= 6; track++)
            {
                var name = track.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!given.TryGetPropertyValue(name, out var value) || value is null)
                {
                    continue;
                }

                if (value.GetValueKind() is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
                {
                    return SimResult.Fail(SimStatus.InvalidRequestFieldType, "The value of one of your tracks is not a boolean.");
                }

                tracks = value.GetValue<bool>() ? tracks | (1 << (track - 1)) : tracks & ~(1 << (track - 1));
            }

            _world.SetTracks(input, tracks);
            return SimResult.Ok();
        }

        private SimResult CreateScene(SimRequest request)
        {
            if (!request.RequireString("sceneName", out var name) || !request.AcquireCanvas())
            {
                return Failed(request);
            }

            var scene = _world.CreateScene(name);
            return SimResult.Ok(Obj(("sceneUuid", scene.UuidText)));
        }

        private SimResult RemoveScene(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null)
            {
                return Failed(request);
            }

            _world.RemoveScene(scene);
            return SimResult.Ok();
        }

        private SimResult SetSceneName(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null || !request.RequireString("newSceneName", out var newName))
            {
                return Failed(request);
            }

            _world.RenameScene(scene, newName);
            return SimResult.Ok();
        }

        private SimResult SetCurrentProgramScene(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null)
            {
                return Failed(request);
            }

            _world.SwitchProgram(scene);
            return SimResult.Ok();
        }

        private SimResult CreateInput(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null || !request.RequireString("inputName", out var name) || !request.RequireString("inputKind", out var kindName))
            {
                return Failed(request);
            }

            _world.EnsureInputNameFree(name, "A source already exists by that input name.");

            var kind = SimInputKinds.Find(kindName);
            if (kind is null)
            {
                return SimResult.Fail(
                    SimStatus.InvalidInputKind,
                    "Your specified input kind is not supported by OBS. Check that your specified kind is properly versioned and that any necessary plugins are loaded.");
            }

            JsonObject? settings = null;
            if (request.Contains("inputSettings"))
            {
                if (!request.OptionalObject("inputSettings", out var value, allowEmpty: true))
                {
                    return Failed(request);
                }

                settings = value;
            }

            var enabled = true;
            if (request.Contains("sceneItemEnabled") && !request.OptionalBoolean("sceneItemEnabled", out enabled))
            {
                return Failed(request);
            }

            var input = _world.CreateInput(scene, name, kind, settings, enabled, out var item);
            return SimResult.Ok(Obj(("inputUuid", input.UuidText), ("sceneItemId", item.Id)));
        }

        private SimResult RemoveInput(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            _world.RemoveInput(input);
            return SimResult.Ok();
        }

        private SimResult SetInputName(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireString("newInputName", out var newName))
            {
                return Failed(request);
            }

            _world.RenameInput(input, newName);
            return SimResult.Ok();
        }

        private SimResult SetInputMute(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireBoolean("inputMuted", out var muted))
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);
            _world.SetMuted(input, muted);
            return SimResult.Ok();
        }

        private SimResult ToggleInputMute(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            var muted = _world.ToggleMuted(input);
            return SimResult.Ok(Obj(("inputMuted", muted)));
        }

        private SimResult SetInputVolume(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            SimWorld.EnsureAudio(input);

            var hasMul = request.Contains("inputVolumeMul");
            var mul = 0.0;
            if (hasMul && !request.OptionalNumber("inputVolumeMul", out mul, 0, 20))
            {
                return Failed(request);
            }

            var hasDb = request.Contains("inputVolumeDb");
            var db = 0.0;
            if (hasDb && !request.OptionalNumber("inputVolumeDb", out db, -100, 26))
            {
                return Failed(request);
            }

            if (hasMul && hasDb)
            {
                return SimResult.Fail(SimStatus.TooManyRequestFields, "You may only specify one volume field.");
            }

            if (!hasMul && !hasDb)
            {
                return SimResult.Fail(SimStatus.MissingRequestField, "You must specify one volume field.");
            }

            _world.SetVolume(input, hasMul ? (float)mul : SimWorld.DbToMul(db));
            return SimResult.Ok();
        }

        private SimResult CreateSceneItem(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null)
            {
                return Failed(request);
            }

            var source = request.AcquireSource("sourceName", "sourceUuid");
            if (source is null)
            {
                return Failed(request);
            }

            if (ReferenceEquals(source, scene))
            {
                return SimResult.Fail(SimStatus.CannotAct, "You cannot create scene item of a scene within itself.");
            }

            var enabled = true;
            if (request.Contains("sceneItemEnabled") && !request.OptionalBoolean("sceneItemEnabled", out enabled))
            {
                return Failed(request);
            }

            var item = _world.AddItem(scene, source, enabled);
            return SimResult.Ok(Obj(("sceneItemId", item.Id)));
        }

        private SimResult RemoveSceneItem(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null)
            {
                return Failed(request);
            }

            _world.RemoveItem(item);
            return SimResult.Ok();
        }

        private SimResult SetSceneItemEnabled(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null || !request.RequireBoolean("sceneItemEnabled", out var enabled))
            {
                return Failed(request);
            }

            _world.SetItemEnabled(item, enabled);
            return SimResult.Ok();
        }

        private SimResult SetSceneItemLocked(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null || !request.RequireBoolean("sceneItemLocked", out var locked))
            {
                return Failed(request);
            }

            _world.SetItemLocked(item, locked);
            return SimResult.Ok();
        }

        private SimResult SetSceneItemIndex(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null || !request.RequireNumber("sceneItemIndex", out var index, min: 0, max: 8192))
            {
                return Failed(request);
            }

            _world.SetItemIndex(item, (int)index);
            return SimResult.Ok();
        }

        private SimResult GetSceneCollectionList(SimRequest request)
        {
            return SimResult.Ok(Obj(
                ("currentSceneCollectionName", _obs.CurrentCollection),
                ("sceneCollections", Arr(_obs.Collections.Select(name => (JsonNode?)name)))));
        }

        // ASSUMPTION: read from the source of obs-websocket 5.7.6 (RequestHandler_Config.cpp), and not measured. A name that is not a collection is
        // answered with 600 and no comment; the collection that is the current one is switched to without anything happening (no event).
        private SimResult SetCurrentSceneCollection(SimRequest request)
        {
            if (!request.RequireString("sceneCollectionName", out var name))
            {
                return Failed(request);
            }

            if (!_obs.Collections.Contains(name))
            {
                return SimResult.Fail(SimStatus.ResourceNotFound);
            }

            return string.Equals(name, _obs.CurrentCollection, StringComparison.Ordinal) ? SimResult.Ok() : SimResult.Switching(name, create: false);
        }

        // ASSUMPTION: read from the source of obs-websocket 5.7.6, and not measured. A name that is a collection is answered with 601 and no comment. The
        // collection that is made has one scene, as OBS Studio makes it for a collection that is new (a global audio device is not made).
        private SimResult CreateSceneCollection(SimRequest request)
        {
            if (!request.RequireString("sceneCollectionName", out var name))
            {
                return Failed(request);
            }

            return _obs.Collections.Contains(name) ? SimResult.Fail(SimStatus.ResourceAlreadyExists) : SimResult.Switching(name, create: true);
        }
    }
}
