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
            ["GetSceneItemTransform"] = static (self, request) => self.GetSceneItemTransform(request),
            ["GetSceneItemBlendMode"] = static (self, request) => self.GetSceneItemBlendMode(request),
            ["GetCurrentProgramScene"] = static (self, request) => self.GetCurrentProgramScene(request),
            ["GetCurrentPreviewScene"] = static (self, request) => self.GetCurrentPreviewScene(request),
            ["GetStudioModeEnabled"] = static (self, request) => self.GetStudioModeEnabled(request),
            ["SetStudioModeEnabled"] = static (self, request) => self.SetStudioModeEnabled(request),
            ["SetCurrentPreviewScene"] = static (self, request) => self.SetCurrentPreviewScene(request),
            ["TriggerStudioModeTransition"] = static (self, request) => self.TriggerStudioModeTransition(request),
            ["GetCurrentSceneTransition"] = static (self, request) => self.GetCurrentSceneTransition(request),
            ["SetCurrentSceneTransition"] = static (self, request) => self.SetCurrentSceneTransition(request),
            ["SetCurrentSceneTransitionDuration"] = static (self, request) => self.SetCurrentSceneTransitionDuration(request),
            ["GetInputMute"] = static (self, request) => self.GetInputMute(request),
            ["GetInputVolume"] = static (self, request) => self.GetInputVolume(request),
            ["GetInputSettings"] = static (self, request) => self.GetInputSettings(request),
            ["GetInputAudioBalance"] = static (self, request) => self.GetInputAudioBalance(request),
            ["GetInputAudioSyncOffset"] = static (self, request) => self.GetInputAudioSyncOffset(request),
            ["GetInputAudioMonitorType"] = static (self, request) => self.GetInputAudioMonitorType(request),
            ["GetInputAudioTracks"] = static (self, request) => self.GetInputAudioTracks(request),
            ["GetMediaInputStatus"] = static (self, request) => self.GetMediaInputStatus(request),
            ["GetSourceActive"] = static (self, request) => self.GetSourceActive(request),
            ["SetMediaInputCursor"] = static (self, request) => self.SetMediaInputCursor(request),
            ["OffsetMediaInputCursor"] = static (self, request) => self.OffsetMediaInputCursor(request),
            ["TriggerMediaInputAction"] = static (self, request) => self.TriggerMediaInputAction(request),
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
            ["SetSceneItemTransform"] = static (self, request) => self.SetSceneItemTransform(request),
            ["SetSceneItemBlendMode"] = static (self, request) => self.SetSceneItemBlendMode(request),
            ["DuplicateSceneItem"] = static (self, request) => self.DuplicateSceneItem(request),
            ["GetSceneCollectionList"] = static (self, request) => self.GetSceneCollectionList(request),
            ["SetCurrentSceneCollection"] = static (self, request) => self.SetCurrentSceneCollection(request),
            ["CreateSceneCollection"] = static (self, request) => self.CreateSceneCollection(request),
            ["GetProfileList"] = static (self, request) => self.GetProfileList(request),
            ["SetCurrentProfile"] = static (self, request) => self.SetCurrentProfile(request),
            ["CreateProfile"] = static (self, request) => self.CreateProfile(request),
            ["RemoveProfile"] = static (self, request) => self.RemoveProfile(request),
            ["GetVideoSettings"] = static (self, request) => self.GetVideoSettings(request),
            ["SetVideoSettings"] = static (self, request) => self.SetVideoSettings(request),
            ["GetStreamServiceSettings"] = static (self, request) => self.GetStreamServiceSettings(request),
            ["SetStreamServiceSettings"] = static (self, request) => self.SetStreamServiceSettings(request),
            ["GetRecordDirectory"] = static (self, request) => self.GetRecordDirectory(request),
            ["SetRecordDirectory"] = static (self, request) => self.SetRecordDirectory(request),
            ["GetProfileParameter"] = static (self, request) => self.GetProfileParameter(request),
            ["SetProfileParameter"] = static (self, request) => self.SetProfileParameter(request),
            ["GetPersistentData"] = static (self, request) => self.GetPersistentData(request),
            ["SetPersistentData"] = static (self, request) => self.SetPersistentData(request),
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

            // The program scene is read at once, and the list of scenes lags (facts 2 and 4). There is a preview scene with studio mode on.
            var preview = _world.StudioMode ? _world.Preview : null;
            return SimResult.Ok(Obj(
                ("currentPreviewSceneName", preview?.Name),
                ("currentPreviewSceneUuid", preview?.UuidText),
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

        private SimResult GetSceneItemTransform(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            return item is null ? Failed(request) : SimResult.Ok(Obj(("sceneItemTransform", SimWorld.TransformJson(item))));
        }

        private SimResult GetSceneItemBlendMode(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            return item is null ? Failed(request) : SimResult.Ok(Obj(("sceneItemBlendMode", item.BlendMode)));
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
            // OBS Studio refuses while studio mode is off (506), with no comment (measured with OBS Studio 33.0.0).
            if (!_world.StudioMode)
            {
                return SimResult.Fail(SimStatus.StudioModeNotActive);
            }

            return SimResult.Ok(_world.PreviewSceneAnswerJson());
        }

        private SimResult GetStudioModeEnabled(SimRequest request)
        {
            return SimResult.Ok(Obj(("studioModeEnabled", _world.StudioMode)));
        }

        private SimResult SetStudioModeEnabled(SimRequest request)
        {
            if (!request.RequireBoolean("studioModeEnabled", out var enabled))
            {
                return Failed(request);
            }

            _world.SetStudioMode(enabled);
            return SimResult.Ok();
        }

        private SimResult SetCurrentPreviewScene(SimRequest request)
        {
            var scene = request.AcquireScene();
            if (scene is null)
            {
                return Failed(request);
            }

            _world.SwitchPreview(scene);
            return SimResult.Ok();
        }

        private SimResult TriggerStudioModeTransition(SimRequest request)
        {
            _world.TriggerTransition();
            return SimResult.Ok();
        }

        private SimResult GetCurrentSceneTransition(SimRequest request)
        {
            return SimResult.Ok(_world.CurrentTransitionJson());
        }

        private SimResult SetCurrentSceneTransition(SimRequest request)
        {
            if (!request.RequireString("transitionName", out var name))
            {
                return Failed(request);
            }

            _world.SetCurrentTransition(name);
            return SimResult.Ok();
        }

        private SimResult SetCurrentSceneTransitionDuration(SimRequest request)
        {
            if (!request.RequireNumber("transitionDuration", out var duration, 50, 20000))
            {
                return Failed(request);
            }

            _world.SetTransitionDuration((int)duration);
            return SimResult.Ok();
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

        // Whether a source (an input or a scene) is on the air and whether it is shown (GetSourceActive).
        private SimResult GetSourceActive(SimRequest request)
        {
            var source = request.AcquireSource("sourceName", "sourceUuid");
            if (source is null)
            {
                return Failed(request);
            }

            var (active, showing) = _world.ActivityOf(source);
            return SimResult.Ok(Obj(("videoActive", active), ("videoShowing", showing)));
        }

        // The state of the media of an input, and, while it plays or is paused, its length and its position (GetMediaInputStatus). An input that is no media input answers with the state none
        // (measured with a color source), and so does a media input that was not opened.
        private SimResult GetMediaInputStatus(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null)
            {
                return Failed(request);
            }

            var timed = input.MediaState is "OBS_MEDIA_STATE_PLAYING" or "OBS_MEDIA_STATE_PAUSED";
            return SimResult.Ok(Obj(
                ("mediaCursor", timed ? input.MediaCursor : null),
                ("mediaDuration", timed ? SimWorld.MediaDuration : null),
                ("mediaState", input.MediaState)));
        }

        private SimResult SetMediaInputCursor(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireNumber("mediaCursor", out var cursor, 0))
            {
                return Failed(request);
            }

            _world.MoveMedia(input, (long)cursor, relative: false);
            return SimResult.Ok();
        }

        private SimResult OffsetMediaInputCursor(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireNumber("mediaCursorOffset", out var offset))
            {
                return Failed(request);
            }

            _world.MoveMedia(input, (long)offset, relative: true);
            return SimResult.Ok();
        }

        private static readonly string[] MediaActions =
        [
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT",
            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS",
        ];

        private SimResult TriggerMediaInputAction(SimRequest request)
        {
            var input = request.AcquireInput();
            if (input is null || !request.RequireString("mediaAction", out var action))
            {
                return Failed(request);
            }

            if (!MediaActions.Contains(action))
            {
                return SimResult.Fail(SimStatus.InvalidRequestField, "You have specified an invalid media input action.");
            }

            _world.TriggerMedia(input, action);
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

        // The members of the transform are checked one by one, in the order of RequestHandler_SceneItems.cpp of obs-websocket 5.7.6 (read from its source), with the ranges that it checks, and the first member that is not right ends the
        // request with nothing changed. The rotation, the crop and the size of the bounds were also measured with OBS Studio 33.0.0 (LiveGeometryTests).
        private static bool ReadTransform(SimRequest fields, SimTransformUpdate update, SimItem item)
        {
            const double Limit = 90001.0;
            const double MaxAlignment = 4294967295.0;
            var (sourceWidth, sourceHeight) = SimWorld.SourceSize(item.Source);

            float Number(string key, out bool ok, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
            {
                ok = fields.OptionalNumber(key, out var value, min, max);
                return (float)value;
            }

            bool ok;
            if (fields.Contains("positionX"))
            {
                update.PositionX = Number("positionX", out ok, -Limit, Limit);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("positionY"))
            {
                update.PositionY = Number("positionY", out ok, -Limit, Limit);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("rotation"))
            {
                update.Rotation = Number("rotation", out ok, -360, 360);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("scaleX"))
            {
                update.ScaleX = Number("scaleX", out ok);
                if (!ok)
                {
                    return false;
                }

                var finalWidth = (float)sourceWidth * update.ScaleX.Value;
                if (!(finalWidth > -Limit && finalWidth < Limit))
                {
                    return fields.Refuse(SimStatus.RequestFieldOutOfRange, "The field `scaleX` is too small or large for the current source resolution.");
                }
            }

            if (fields.Contains("scaleY"))
            {
                update.ScaleY = Number("scaleY", out ok, -Limit, Limit);
                if (!ok)
                {
                    return false;
                }

                var finalHeight = (float)sourceHeight * update.ScaleY.Value;
                if (!(finalHeight > -Limit && finalHeight < Limit))
                {
                    return fields.Refuse(SimStatus.RequestFieldOutOfRange, "The field `scaleY` is too small or large for the current source resolution.");
                }
            }

            if (fields.Contains("alignment"))
            {
                update.Alignment = (int)(uint)Number("alignment", out ok, 0, MaxAlignment);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("boundsType"))
            {
                if (!fields.OptionalString("boundsType", out var boundsType))
                {
                    return false;
                }

                if (!SimWorld.BoundsTypes.Contains(boundsType))
                {
                    return fields.Refuse(SimStatus.InvalidRequestField, "The field `boundsType` has an invalid value.");
                }

                update.BoundsType = boundsType;
            }

            if (fields.Contains("boundsAlignment"))
            {
                update.BoundsAlignment = (int)(uint)Number("boundsAlignment", out ok, 0, MaxAlignment);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("boundsWidth"))
            {
                update.BoundsWidth = Number("boundsWidth", out ok, 1, Limit);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("boundsHeight"))
            {
                update.BoundsHeight = Number("boundsHeight", out ok, 1, Limit);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("cropLeft"))
            {
                update.CropLeft = (int)Number("cropLeft", out ok, 0, 100000);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("cropRight"))
            {
                update.CropRight = (int)Number("cropRight", out ok, 0, 100000);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("cropTop"))
            {
                update.CropTop = (int)Number("cropTop", out ok, 0, 100000);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("cropBottom"))
            {
                update.CropBottom = (int)Number("cropBottom", out ok, 0, 100000);
                if (!ok)
                {
                    return false;
                }
            }

            if (fields.Contains("cropToBounds"))
            {
                if (!fields.OptionalBoolean("cropToBounds", out var cropToBounds))
                {
                    return false;
                }

                update.CropToBounds = cropToBounds;
            }

            return true;
        }

        private SimResult SetSceneItemTransform(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null || !request.ValidateBasic("sceneItemTransform") || !request.OptionalObject("sceneItemTransform", out var given, allowEmpty: false))
            {
                return Failed(request);
            }

            var fields = new SimRequest(_world, given);
            var update = new SimTransformUpdate();
            if (!ReadTransform(fields, update, item))
            {
                return SimResult.Fail(fields.Failure!);
            }

            // An object of members that this request does not know changes nothing, and is refused (read from the source of obs-websocket 5.7.6).
            if (!update.HasInfo && !update.HasCrop)
            {
                return SimResult.Fail(SimStatus.CannotAct, "You have not provided any valid transform changes.");
            }

            _world.SetItemTransform(item, update);
            return SimResult.Ok();
        }

        private SimResult SetSceneItemBlendMode(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null || !request.RequireString("sceneItemBlendMode", out var mode))
            {
                return Failed(request);
            }

            // Read from the source of obs-websocket 5.7.6: a name that is no blend mode is refused (the comment has no quotes around the name of the field there).
            if (!SimWorld.BlendModes.Contains(mode))
            {
                return SimResult.Fail(SimStatus.InvalidRequestField, "The field sceneItemBlendMode has an invalid value.");
            }

            _world.SetItemBlendMode(item, mode);
            return SimResult.Ok();
        }

        // Read from the source of obs-websocket 5.7.6: the destination is only read when the request has destinationSceneName, and then a UUID that is there wins over the name. A request with only
        // destinationSceneUuid makes the copy in the scene of the original, which OBS Studio 33.0.0 did (measured, LiveGeometryTests).
        private SimResult DuplicateSceneItem(SimRequest request)
        {
            var item = request.AcquireSceneItem();
            if (item is null)
            {
                return Failed(request);
            }

            var destination = item.Scene;
            if (request.Contains("destinationSceneName"))
            {
                var source = request.AcquireSource("destinationSceneName", "destinationSceneUuid");
                if (source is null)
                {
                    return Failed(request);
                }

                if (source is not SimScene scene)
                {
                    return SimResult.Fail(SimStatus.InvalidResourceType, "The specified source is not a scene.");
                }

                destination = scene;
            }

            var copy = _world.DuplicateItem(item, destination);
            return SimResult.Ok(Obj(("sceneItemId", copy.Id)));
        }

        private SimResult GetSceneCollectionList(SimRequest request)
        {
            return SimResult.Ok(Obj(
                ("currentSceneCollectionName", _obs.CurrentCollection),
                ("sceneCollections", Arr(_obs.Collections.Select(name => (JsonNode?)name)))));
        }

        // Measured with OBS Studio 33.0.0 (LiveProfilesTests) and read from the source of obs-websocket 5.7.6 (RequestHandler_Config.cpp). A name that is not a collection is
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

        // Measured with OBS Studio 33.0.0 (LiveProfilesTests). A name that is a collection is answered with 601 and no comment. The
        // collection that is made has one scene, as OBS Studio makes it for a collection that is new (a global audio device is not made).
        private SimResult CreateSceneCollection(SimRequest request)
        {
            if (!request.RequireString("sceneCollectionName", out var name))
            {
                return Failed(request);
            }

            return _obs.Collections.Contains(name) ? SimResult.Fail(SimStatus.ResourceAlreadyExists) : SimResult.Switching(name, create: true);
        }

        private SimResult GetProfileList(SimRequest request)
        {
            return SimResult.Ok(Obj(
                ("currentProfileName", _obs.CurrentProfile),
                ("profiles", Arr(_obs.Profiles.Select(name => (JsonNode?)name)))));
        }

        // Measured with OBS Studio 33.0.0 (LiveProfilesTests). A name that is not a profile is answered with 600 and no comment. The request is answered when the switch is done, with
        // both events; the profile that is the current one is switched to without anything happening (no event).
        private SimResult SetCurrentProfile(SimRequest request)
        {
            if (!request.RequireString("profileName", out var name))
            {
                return Failed(request);
            }

            if (!_obs.Profiles.Contains(name))
            {
                return SimResult.Fail(SimStatus.ResourceNotFound);
            }

            if (!string.Equals(name, _obs.CurrentProfile, StringComparison.Ordinal))
            {
                _obs.SwitchProfile(name);
            }

            return SimResult.Ok();
        }

        // Measured with OBS Studio 33.0.0 (LiveProfilesTests). A name that is a profile is answered with 601 and no comment. OBS Studio answers before it has made the profile: its user interface
        // makes it a moment later, and switches to it.
        private SimResult CreateProfile(SimRequest request)
        {
            if (!request.RequireString("profileName", out var name))
            {
                return Failed(request);
            }

            if (_obs.Profiles.Contains(name))
            {
                return SimResult.Fail(SimStatus.ResourceAlreadyExists);
            }

            _obs.ScheduleUi(_obs.Quirks.ProfileCreateDelay.Sample(_obs.Random), () => _obs.MakeProfile(name));
            return SimResult.Ok();
        }

        // Measured with OBS Studio 33.0.0 (LiveProfilesTests) and read from the source of obs-websocket 5.7.6 and OBS Studio 33.0.0. A name that is not a profile is answered with 600 and no comment,
        // and one that is the only profile with 603. A profile that is not the current one is removed a moment after the answer. The current one is not: OBS Studio asks the user in its window
        // whether to remove it (OBSBasic::DeleteProfile), and answers at once that it did.
        private SimResult RemoveProfile(SimRequest request)
        {
            if (!request.RequireString("profileName", out var name))
            {
                return Failed(request);
            }

            if (!_obs.Profiles.Contains(name))
            {
                return SimResult.Fail(SimStatus.ResourceNotFound);
            }

            if (_obs.Profiles.Count < 2)
            {
                return SimResult.Fail(SimStatus.NotEnoughResources);
            }

            if (!string.Equals(name, _obs.CurrentProfile, StringComparison.Ordinal))
            {
                _obs.ScheduleUi(_obs.Quirks.ProfileRemoveDelay.Sample(_obs.Random), () => _obs.DropProfile(name));
            }

            return SimResult.Ok();
        }

        // Read from the source of obs-websocket 5.7.6 (RequestHandler_Config.cpp), and measured with OBS Studio 33.0.0 (LiveProfileSettingsModelTests): what OBS Studio renders with, which is not
        // what the configuration of the profile says when somebody wrote a parameter of the video to it.
        private SimResult GetVideoSettings(SimRequest request)
        {
            var video = _obs.Video;
            return SimResult.Ok(Obj(
                ("baseHeight", video.BaseHeight),
                ("baseWidth", video.BaseWidth),
                ("fpsDenominator", video.FpsDenominator),
                ("fpsNumerator", video.FpsNumerator),
                ("outputHeight", video.OutputHeight),
                ("outputWidth", video.OutputWidth)));
        }

        // The checks of obs-websocket 5.7.6, in its order (the refusal while an output runs comes first, and the simulation has no output): a pair of fields counts when both are there, a pair that has one is ignored,
        // and a request with no pair is refused with 300 (measured with OBS Studio 33.0.0: the frame rate in parts of 1 or more, the sizes from 8 to 4096; the protocol document says 1).
        private SimResult SetVideoSettings(SimRequest request)
        {
            double fpsNumerator = 0, fpsDenominator = 0, baseWidth = 0, baseHeight = 0, outputWidth = 0, outputHeight = 0;
            var changeFps = request.Contains("fpsNumerator") && request.Contains("fpsDenominator");
            if (changeFps && !(request.OptionalNumber("fpsNumerator", out fpsNumerator, min: 1) && request.OptionalNumber("fpsDenominator", out fpsDenominator, min: 1)))
            {
                return Failed(request);
            }

            var changeBase = request.Contains("baseWidth") && request.Contains("baseHeight");
            if (changeBase && !(request.OptionalNumber("baseWidth", out baseWidth, min: 8, max: 4096) && request.OptionalNumber("baseHeight", out baseHeight, min: 8, max: 4096)))
            {
                return Failed(request);
            }

            var changeOutput = request.Contains("outputWidth") && request.Contains("outputHeight");
            if (changeOutput && !(request.OptionalNumber("outputWidth", out outputWidth, min: 8, max: 4096) && request.OptionalNumber("outputHeight", out outputHeight, min: 8, max: 4096)))
            {
                return Failed(request);
            }

            if (!changeFps && !changeBase && !changeOutput)
            {
                return SimResult.Fail(SimStatus.MissingRequestField, "You must specify at least one video-changing pair.");
            }

            _obs.SetVideo(new SimVideo(ToInt(fpsNumerator), ToInt(fpsDenominator), ToInt(baseWidth), ToInt(baseHeight), ToInt(outputWidth), ToInt(outputHeight)), changeFps, changeBase, changeOutput);
            return SimResult.Ok();
        }

        // A number of the request that is written to the configuration as an unsigned integer (config_set_uint): the part after the point is cut off.
        private static int ToInt(double number) => (int)Math.Clamp(Math.Truncate(number), 0, int.MaxValue);

        // Measured with OBS Studio 33.0.0 (LiveProfileSettingsModelTests): a profile that is new has the service rtmp_common, and no settings.
        private SimResult GetStreamServiceSettings(SimRequest request)
        {
            return SimResult.Ok(Obj(("streamServiceSettings", _obs.ServiceSettings), ("streamServiceType", _obs.ServiceType)));
        }

        // Read from the source of obs-websocket 5.7.6, and measured with OBS Studio 33.0.0: the settings can not be empty (403), and a type that OBS Studio has no service of is refused with 700.
        private SimResult SetStreamServiceSettings(SimRequest request)
        {
            if (!request.RequireString("streamServiceType", out var type) || !request.ValidateBasic("streamServiceSettings") || !request.OptionalObject("streamServiceSettings", out var settings, allowEmpty: false))
            {
                return Failed(request);
            }

            return _obs.SetService(type, settings)
                ? SimResult.Ok()
                : SimResult.Fail(SimStatus.ResourceCreationFailed, "Failed to create the stream service with the requested streamServiceType. It may be an invalid type.");
        }

        private SimResult GetRecordDirectory(SimRequest request)
        {
            return SimResult.Ok(Obj(("recordDirectory", _obs.RecordDirectory)));
        }

        private SimResult SetRecordDirectory(SimRequest request)
        {
            if (!request.RequireString("recordDirectory", out var directory))
            {
                return Failed(request);
            }

            _obs.SetRecordDirectory(directory);
            return SimResult.Ok();
        }

        // A parameter that has a default says its value (what was set, or else the default) and the default; one that was set and has none says its value and no default; a parameter that nobody has
        // says neither, and that is no error (the capture of a real OBS Studio shows each of these).
        private SimResult GetProfileParameter(SimRequest request)
        {
            if (!request.RequireString("parameterCategory", out var category) || !request.RequireString("parameterName", out var name))
            {
                return Failed(request);
            }

            var (value, fallback) = _obs.Parameter(category, name);
            return SimResult.Ok(Obj(("defaultParameterValue", fallback), ("parameterValue", value)));
        }

        // A value that is missing or null removes the parameter, and the request is refused with 600 when it had no value that was set (measured with OBS Studio 33.0.0). A value that is no string is refused with 401.
        private SimResult SetProfileParameter(SimRequest request)
        {
            if (!request.RequireString("parameterCategory", out var category) || !request.RequireString("parameterName", out var name))
            {
                return Failed(request);
            }

            if (!request.Contains("parameterValue"))
            {
                return _obs.SetParameter(category, name, null)
                    ? SimResult.Ok()
                    : SimResult.Fail(SimStatus.ResourceNotFound, "There are no existing instances of that profile parameter.");
            }

            if (request.Data["parameterValue"] is JsonValue node && node.TryGetValue<string>(out var value))
            {
                _obs.SetParameter(category, name, value);
                return SimResult.Ok();
            }

            return SimResult.Fail(SimStatus.InvalidRequestFieldType, "The field `parameterValue` must be a string.");
        }

        // The data of a slot that nobody put anything in is null (measured with OBS Studio 33.0.0).
        private SimResult GetPersistentData(SimRequest request)
        {
            if (!request.RequireString("realm", out var realm) || !request.RequireString("slotName", out var slot))
            {
                return Failed(request);
            }

            if (!TryGetRealm(realm, out var global))
            {
                return SimResult.Fail(SimStatus.ResourceNotFound, "You have specified an invalid persistent data realm.");
            }

            return SimResult.Ok(Obj(("slotValue", _obs.PersistentData(global, slot))));
        }

        // A slot value that is null counts as a field that is missing (measured with OBS Studio 33.0.0: "Your request is missing the `slotValue` field."), so a slot can not be emptied: it holds what it was last given.
        private SimResult SetPersistentData(SimRequest request)
        {
            if (!request.RequireString("realm", out var realm) || !request.RequireString("slotName", out var slot) || !request.ValidateBasic("slotValue"))
            {
                return Failed(request);
            }

            if (!TryGetRealm(realm, out var global))
            {
                return SimResult.Fail(SimStatus.ResourceNotFound, "You have specified an invalid persistent data realm.");
            }

            _obs.SetPersistentData(global, slot, request.Data["slotValue"]!);
            return SimResult.Ok();
        }

        private static bool TryGetRealm(string realm, out bool global)
        {
            global = string.Equals(realm, "OBS_WEBSOCKET_DATA_REALM_GLOBAL", StringComparison.Ordinal);
            return global || string.Equals(realm, "OBS_WEBSOCKET_DATA_REALM_PROFILE", StringComparison.Ordinal);
        }
    }
}
