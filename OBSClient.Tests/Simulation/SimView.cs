namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Json;

    /// <summary>
    /// The part of the state of a simulated OBS Studio that a client can see by asking, written as lines of text: what a snapshot holds
    /// (<see cref="Of"/>) and what the requests answer (<see cref="Read"/>). When OBS Studio has no lag left, the two are the same lines.
    /// </summary>
    internal static class SimView
    {
        /// <summary>Writes what a snapshot holds that a client can ask for: the program scene, the scenes with their items, and the inputs.</summary>
        /// <param name="snapshot">The snapshot.</param>
        public static List<string> Of(SimSnapshot snapshot)
        {
            var lines = new List<string> { "program " + snapshot.ProgramSceneUuid.ToString("D") };
            foreach (var scene in snapshot.Scenes)
            {
                lines.Add($"scene {scene.Index.ToString(CultureInfo.InvariantCulture)} {scene.Uuid:D} \"{scene.Name}\"");
                AddItems(lines, scene.Items);
            }

            foreach (var scene in snapshot.DetachedScenes)
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                AddItems(lines, scene.Items);
            }

            foreach (var input in snapshot.Inputs)
            {
                lines.Add($"input {input.Uuid:D} \"{input.Name}\" {input.Kind}{Audio(input)} settings={input.Settings}");
            }

            return lines;
        }

        private static string Audio(SimInputSnapshot input)
        {
            return input.HasAudio
                ? $" {(input.Muted ? "muted" : "unmuted")} mul={input.VolumeMul.ToString("R", CultureInfo.InvariantCulture)} balance={input.Balance.ToString("R", CultureInfo.InvariantCulture)} offset={input.SyncOffset.ToString(CultureInfo.InvariantCulture)} monitor={input.MonitorType} tracks={input.Tracks.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
        }

        /// <summary>
        /// Asks what a client can ask for, and writes it as <see cref="Of"/> does: the scenes of <c>GetSceneList</c> with the items of
        /// <c>GetSceneItemList</c>, the inputs of <c>GetInputList</c> with their settings (<c>GetInputSettings</c>) and, for those that have audio, <c>GetInputMute</c>,
        /// <c>GetInputVolume</c>, <c>GetInputAudioBalance</c>, <c>GetInputAudioSyncOffset</c>, <c>GetInputAudioMonitorType</c> and <c>GetInputAudioTracks</c>. A scene that was
        /// removed is not in the list of scenes, so those that are detached are asked for by the UUID that the snapshot gives.
        /// </summary>
        /// <param name="detached">The scenes that were removed and are still alive.</param>
        /// <param name="ask">Runs a request, waits for its response, and returns the data of the response. It fails when the request failed.</param>
        public static List<string> Read(IReadOnlyList<SimSceneSnapshot> detached, Func<string, object?, JsonElement> ask)
        {
            var lines = new List<string>();
            var sceneList = ask("GetSceneList", null);
            lines.Add("program " + sceneList.GetProperty("currentProgramSceneUuid").GetString());
            foreach (var scene in sceneList.GetProperty("scenes").EnumerateArray())
            {
                var uuid = scene.GetProperty("sceneUuid").GetString()!;
                lines.Add($"scene {scene.GetProperty("sceneIndex").GetInt32().ToString(CultureInfo.InvariantCulture)} {uuid} \"{scene.GetProperty("sceneName").GetString()}\"");
                AddItems(lines, ask("GetSceneItemList", new { sceneUuid = uuid }));
            }

            foreach (var scene in detached)
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                AddItems(lines, ask("GetSceneItemList", new { sceneUuid = scene.Uuid.ToString("D") }));
            }

            foreach (var input in ask("GetInputList", null).GetProperty("inputs").EnumerateArray())
            {
                var uuid = input.GetProperty("inputUuid").GetString()!;
                var audio = string.Empty;
                if ((input.GetProperty("inputKindCaps").GetInt32() & SimInputKind.AudioCap) != 0)
                {
                    var muted = ask("GetInputMute", new { inputUuid = uuid }).GetProperty("inputMuted").GetBoolean();
                    var mul = ask("GetInputVolume", new { inputUuid = uuid }).GetProperty("inputVolumeMul").GetDouble();
                    var balance = (float)ask("GetInputAudioBalance", new { inputUuid = uuid }).GetProperty("inputAudioBalance").GetDouble();
                    var offset = ask("GetInputAudioSyncOffset", new { inputUuid = uuid }).GetProperty("inputAudioSyncOffset").GetInt32();
                    var monitor = ask("GetInputAudioMonitorType", new { inputUuid = uuid }).GetProperty("monitorType").GetString();
                    var tracks = ask("GetInputAudioTracks", new { inputUuid = uuid }).GetProperty("inputAudioTracks");
                    var mask = Enumerable.Range(1, 6).Sum(track => tracks.GetProperty(track.ToString(CultureInfo.InvariantCulture)).GetBoolean() ? 1 << (track - 1) : 0);
                    audio = $" {(muted ? "muted" : "unmuted")} mul={mul.ToString("R", CultureInfo.InvariantCulture)} balance={balance.ToString("R", CultureInfo.InvariantCulture)} offset={offset.ToString(CultureInfo.InvariantCulture)} monitor={monitor} tracks={mask.ToString(CultureInfo.InvariantCulture)}";
                }

                var settings = SimJson.Serialize(System.Text.Json.Nodes.JsonNode.Parse(ask("GetInputSettings", new { inputUuid = uuid }).GetProperty("inputSettings").GetRawText())!);
                lines.Add($"input {uuid} \"{input.GetProperty("inputName").GetString()}\" {input.GetProperty("inputKind").GetString()}{audio} settings={settings}");
            }

            return lines;
        }

        private static void AddItems(List<string> lines, IReadOnlyList<SimItemSnapshot> items)
        {
            foreach (var item in items)
            {
                lines.Add(Item(item.Id, item.Index, item.IsScene, item.SourceUuid.ToString("D"), item.SourceName, item.Enabled, item.Locked));
            }
        }

        private static void AddItems(List<string> lines, JsonElement response)
        {
            foreach (var item in response.GetProperty("sceneItems").EnumerateArray())
            {
                lines.Add(Item(
                    item.GetProperty("sceneItemId").GetInt32(),
                    item.GetProperty("sceneItemIndex").GetInt32(),
                    item.GetProperty("sourceType").GetString() == "OBS_SOURCE_TYPE_SCENE",
                    item.GetProperty("sourceUuid").GetString()!,
                    item.GetProperty("sourceName").GetString()!,
                    item.GetProperty("sceneItemEnabled").GetBoolean(),
                    item.GetProperty("sceneItemLocked").GetBoolean()));
            }
        }

        private static string Item(int id, int index, bool isScene, string sourceUuid, string sourceName, bool enabled, bool locked)
        {
            return $"  item {id.ToString(CultureInfo.InvariantCulture)} at {index.ToString(CultureInfo.InvariantCulture)} {(isScene ? "scene" : "input")} {sourceUuid} \"{sourceName}\" {(enabled ? "enabled" : "disabled")} {(locked ? "locked" : "unlocked")}";
        }
    }
}
