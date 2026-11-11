namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// The canvases of a scene collection. OBS Studio saves them in the file of the collection (obs_save_canvas, which leaves out the ephemeral ones), and unloads them (the ones that the frontend made or loaded) when it
    /// leaves the collection, so they are as much part of it as its scenes are. The main canvas is always there and is not kept here.
    /// </summary>
    /// <remarks>
    /// Read from the source of OBS Studio (obs.c, obs-canvas.c and the frontend's OBSBasic_Canvases.cpp, at commit fe1ed7955f) and obs-websocket 5.7.6 (the canvas signals of EventHandler_Canvases.cpp), and checked on
    /// OBS Studio 33.0.0 with a script that makes, renames, removes and releases canvases (tools/live/canvas-driver.lua, LiveCanvasModelTests). Measured: <c>obs_enum_canvases</c> walks a hash table that keeps the order in which the
    /// canvases were added, so the list is in the order of creation (the main canvas first, which OBS Studio makes when it starts), and a canvas that is renamed is taken out and added again, which puts it last. The name of a canvas
    /// is made unique (<c>obs_context_deduplicate_name</c>: "Name 2", "Name 3", and so on, when it is made or renamed). A canvas that was removed is listed until the last reference on it is released, and one <c>CanvasRemoved</c> says it.
    /// A canvas that a scene collection saved comes back with its UUID and its flags, and a canvas that was made without a video mix (a loaded one, or one that a script made without) lists its video settings as null.
    /// Only read: a name that is empty or the same is ignored when a canvas is renamed, and the main canvas can be neither renamed nor removed (both are silently ignored); the frontend calls AddCanvas only for
    /// obs_frontend_add_canvas, which a plugin calls, so the user interface of OBS Studio makes no canvas. Scenes of other canvases are not simulated: obs-websocket announces the scenes of the main canvas only.
    /// </remarks>
    internal sealed partial class SimWorld
    {
        // The canvases that are not the main one, in the order that obs_enum_canvases lists them. A canvas that was removed stays here for as long as something holds it (see SimCanvas.ListedUntil).
        private readonly List<SimCanvas> _canvases = [];

        /// <summary>Gets the canvases that are alive and are not the main one, in the order that OBS Studio lists them.</summary>
        public IReadOnlyList<SimCanvas> Canvases => _canvases.Where(canvas => !canvas.Removed).ToArray();

        /// <summary>
        /// Lists the canvases as <c>GetCanvasList</c> lists them: the main canvas, the ones that are alive, and the ones that were removed and that OBS Studio still holds (see <see cref="SimQuirks.CanvasRemovedLinger"/>).
        /// </summary>
        public IReadOnlyList<SimCanvas> ListedCanvases()
        {
            PruneCanvases();
            return [MainCanvas(), .. _canvases];
        }

        // The main canvas has the video of the profile, so it is made again whenever it is asked for.
        // Measured: OBS Studio 33.0.0 lists the main canvas with MAIN, ACTIVATE, MIX_AUDIO and SCENE_REF and without EPHEMERAL (the capture of the tests), which is what obs_create_main_canvas makes (MAIN | PROGRAM).
        private SimCanvas MainCanvas()
        {
            return new SimCanvas(Guid.Parse(MainCanvasUuid), MainCanvasName, SimCanvasFlags.Main | SimCanvasFlags.Program, _obs.Video);
        }

        // A canvas that was removed is destroyed when the last reference on it is released: it leaves the list, and its name is free again, without a word.
        private void PruneCanvases()
        {
            var now = Now;
            _canvases.RemoveAll(canvas => canvas.Removed && (canvas.ListedUntil is not { } until || until <= now));
        }

        /// <summary>
        /// Creates a canvas (obs_canvas_create, which a plugin calls through obs_frontend_add_canvas) and announces it. The flag of the main canvas is taken off (libobs does not let anyone make a main canvas), and a name
        /// that is taken is made unique.
        /// </summary>
        /// <param name="name">The name of the canvas.</param>
        /// <param name="flags">The flags of the canvas.</param>
        /// <param name="video">The video settings of the canvas, or null when it has no video mix.</param>
        public SimCanvas CreateCanvas(string name, int flags, SimVideo? video)
        {
            // ASSUMPTION: a plugin does not make a canvas without a name (libobs would call it "__unnamedNNNN").
            if (name.Length == 0)
            {
                throw new SimFailure(SimStatus.RequestFieldEmpty, "A name cannot be empty.");
            }

            PruneCanvases();
            var canvas = new SimCanvas(_obs.Random.NextGuid(), UniqueCanvasName(name), flags & ~SimCanvasFlags.Main, video);
            _canvases.Add(canvas);
            Emit("CanvasCreated", Obj(("canvasName", canvas.Name), ("canvasUuid", canvas.UuidText)));
            return canvas;
        }

        /// <summary>
        /// Removes a canvas (obs_canvas_remove) and announces it. A canvas that is removed, and the main canvas, are left alone and nothing is announced. OBS Studio goes on listing a canvas that something holds a reference on
        /// until it is released (see <see cref="SimQuirks.CanvasRemovedLinger"/>), and says nothing when that happens.
        /// </summary>
        public void RemoveCanvas(SimCanvas canvas)
        {
            if (canvas.IsMain || canvas.Removed)
            {
                return;
            }

            canvas.Removed = true;
            var linger = _obs.Quirks.CanvasRemovedLinger.Sample(_obs.Random);
            if (linger > TimeSpan.Zero)
            {
                canvas.ListedUntil = Now + linger;
                _obs.AddDeadline(Now + linger);
            }
            else
            {
                _canvases.Remove(canvas);
            }

            Emit("CanvasRemoved", Obj(("canvasName", canvas.Name), ("canvasUuid", canvas.UuidText)));
        }

        /// <summary>
        /// Renames a canvas (obs_canvas_set_name) and announces it. An empty name, the name that it has, and the main canvas, are ignored and nothing is announced. The canvas that is renamed is taken out of the list and added
        /// again, so it is last, and a name that is taken is made unique (the event says the name that the canvas has).
        /// </summary>
        public void RenameCanvas(SimCanvas canvas, string newName)
        {
            if (canvas.IsMain || newName.Length == 0 || string.Equals(newName, canvas.Name, StringComparison.Ordinal))
            {
                return;
            }

            PruneCanvases();
            var oldName = canvas.Name;
            _canvases.Remove(canvas);
            canvas.Name = UniqueCanvasName(newName);
            _canvases.Add(canvas);
            Emit("CanvasNameChanged", Obj(("canvasName", canvas.Name), ("canvasUuid", canvas.UuidText), ("oldCanvasName", oldName)));
        }

        /// <summary>Finds a canvas that is alive by its UUID: the main canvas, or one that a plugin made.</summary>
        public SimCanvas? FindCanvas(Guid uuid)
        {
            return uuid == Guid.Parse(MainCanvasUuid) ? MainCanvas() : _canvases.FirstOrDefault(canvas => !canvas.Removed && canvas.Uuid == uuid);
        }

        // obs_context_deduplicate_name: a name that the hash table of the canvases has gets " 2" (and " 3", and so on) until it is free. The table holds the main canvas, and the canvases that were removed and are not destroyed.
        private string UniqueCanvasName(string name)
        {
            bool Taken(string candidate)
            {
                return string.Equals(candidate, MainCanvasName, StringComparison.Ordinal) || _canvases.Any(canvas => string.Equals(canvas.Name, candidate, StringComparison.Ordinal));
            }

            if (!Taken(name))
            {
                return name;
            }

            var suffix = 2;
            string next;
            do
            {
                next = name + " " + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                suffix++;
            }
            while (Taken(next));
            return next;
        }

        /// <summary>What <c>GetCanvasList</c> says of a canvas.</summary>
        public static JsonObject CanvasJson(SimCanvas canvas)
        {
            var video = canvas.Video;
            return Obj(
                ("canvasFlags", Obj(
                    ("ACTIVATE", (canvas.Flags & SimCanvasFlags.Activate) != 0),
                    ("EPHEMERAL", (canvas.Flags & SimCanvasFlags.Ephemeral) != 0),
                    ("MAIN", (canvas.Flags & SimCanvasFlags.Main) != 0),
                    ("MIX_AUDIO", (canvas.Flags & SimCanvasFlags.MixAudio) != 0),
                    ("SCENE_REF", (canvas.Flags & SimCanvasFlags.SceneRef) != 0))),
                ("canvasName", canvas.Name),
                ("canvasUuid", canvas.UuidText),
                ("canvasVideoSettings", Obj(
                    ("baseHeight", Whole(video?.BaseHeight)),
                    ("baseWidth", Whole(video?.BaseWidth)),
                    ("fpsDenominator", Whole(video?.FpsDenominator)),
                    ("fpsNumerator", Whole(video?.FpsNumerator)),
                    ("outputHeight", Whole(video?.OutputHeight)),
                    ("outputWidth", Whole(video?.OutputWidth)))));
        }

        // A number that a canvas without a video mix does not have: null.
        private static JsonNode? Whole(int? value)
        {
            return value is { } number ? JsonValue.Create(number) : null;
        }

        private IReadOnlyList<SimCanvasSnapshot> SnapshotCanvases()
        {
            var canvases = new List<SimCanvasSnapshot> { new(Guid.Parse(MainCanvasUuid), MainCanvasName, SimCanvasFlags.Main | SimCanvasFlags.Program) };
            canvases.AddRange(_canvases.Where(canvas => !canvas.Removed).Select(canvas => new SimCanvasSnapshot(canvas.Uuid, canvas.Name, canvas.Flags)));
            return canvases;
        }
    }
}
