namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// Whether the inputs are on the air and shown: <c>GetSourceActive</c> answers it, and <c>InputShowStateChanged</c> and <c>InputActiveStateChanged</c> announce its changes. Measured with OBS Studio 33.0.0
    /// (LiveActivityTests): what an input is, is a function of what is on the air (the program scene, and the scene that was on the air before while a transition runs) and what is shown (that, and the preview scene of
    /// studio mode), through the scene items that are enabled and the scenes that are nested. Whenever something that this depends on changes, the simulation works out what the inputs are, and announces each input that
    /// is not what it was: first whether it is shown, and then whether it is on the air (activation and deactivation alike). OBS Studio says nothing of an input that it destroys.
    /// </summary>
    internal sealed partial class SimWorld
    {
        // What is on the air for a scene: the scene itself while studio mode is off, and a copy of its scene items as they were when it went on the air while studio mode is on (OBS Studio shows a private copy of the
        // scene then, so that what is done to the scene in the preview is not done to what is on the air: measured, hiding an item of the program scene in studio mode changed nothing of what GetSourceActive says of its
        // input). The copy holds the same sources, and the scenes that it shows are the scenes themselves.
        private sealed class OnAir
        {
            public OnAir(SimScene scene, (SimSource Source, bool Enabled)[]? copy)
            {
                Scene = scene;
                Copy = copy;
            }

            public SimScene Scene { get; }

            public (SimSource Source, bool Enabled)[]? Copy { get; }
        }

        /// <summary>
        /// Says whether a source is on the air and whether it is shown, as <c>GetSourceActive</c> does. An input says what OBS Studio last announced of it, which is what its scene items and the scenes make it. A scene
        /// is on the air when it is the program scene (while studio mode is off), or when something that is on the air shows it; it is shown when it is on the air, or is the preview scene, or something that is shown shows it.
        /// </summary>
        /// <remarks>
        /// ASSUMPTION: for a scene this is worked out and not announced (no event is about a scene). OBS Studio 33.0.0 said odd things of scenes in studio mode (the program scene was not on the air, the preview scene was only
        /// shown), which this follows only as far as the copy that studio mode puts on the air does.
        /// </remarks>
        public (bool Active, bool Showing) ActivityOf(SimSource source)
        {
            if (source is SimInput input)
            {
                return (input.Active, input.Showing);
            }

            var (active, showing) = ReachableSources();
            return (active.Contains(source), showing.Contains(source));
        }

        // The scene that goes on the air: its items are copied while studio mode is on.
        private OnAir MakeOnAir(SimScene scene)
        {
            return new OnAir(scene, _studioMode ? scene.Items.Select(item => (item.Source, item.Enabled)).ToArray() : null);
        }

        // The sources that are on the air, and those that are shown.
        private (HashSet<SimSource> Active, HashSet<SimSource> Showing) ReachableSources()
        {
            var active = new HashSet<SimSource>();
            Reach(_programAir, active);
            if (_transition?.From is { } from)
            {
                Reach(from, active);
            }

            var showing = new HashSet<SimSource>(active);
            if (_studioMode && _preview is { } preview)
            {
                Reach(new OnAir(preview, null), showing);
            }

            return (active, showing);
        }

        private static void Reach(OnAir air, HashSet<SimSource> into)
        {
            if (air.Copy is null)
            {
                Reach(air.Scene, into);
                return;
            }

            foreach (var (source, enabled) in air.Copy)
            {
                if (enabled)
                {
                    ReachSource(source, into);
                }
            }
        }

        private static void Reach(SimScene scene, HashSet<SimSource> into)
        {
            if (!into.Add(scene))
            {
                return;
            }

            foreach (var item in scene.Items)
            {
                if (item.Enabled)
                {
                    ReachSource(item.Source, into);
                }
            }
        }

        private static void ReachSource(SimSource source, HashSet<SimSource> into)
        {
            if (source.Destroyed)
            {
                return;
            }

            if (source is SimScene nested)
            {
                Reach(nested, into);
            }
            else
            {
                into.Add(source);
            }
        }

        // Works out what the inputs are now, and announces the inputs that are not what they were: for each of them first whether it is shown, and then whether it is on the air. An input that OBS Studio
        // destroyed is not in the list any more, and nothing is said of it.
        private void UpdateActivity()
        {
            var (active, showing) = ReachableSources();
            foreach (var input in _inputs.ToArray())
            {
                var nowShowing = showing.Contains(input);
                if (input.Showing != nowShowing)
                {
                    input.Showing = nowShowing;
                    Emit("InputShowStateChanged", Obj(("inputName", input.Name), ("inputUuid", input.UuidText), ("videoShowing", nowShowing)));
                }

                var nowActive = active.Contains(input);
                if (input.Active != nowActive)
                {
                    input.Active = nowActive;
                    Emit("InputActiveStateChanged", Obj(("inputName", input.Name), ("inputUuid", input.UuidText), ("videoActive", nowActive)));
                    TrackMeter(input);
                }
            }
        }

        // The state of a collection that was just loaded: nothing is announced of it.
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_programAir))]
        private void InitializeActivity()
        {
            _programAir = MakeOnAir(_program);
            var (active, showing) = ReachableSources();
            foreach (var input in _inputs)
            {
                input.Showing = showing.Contains(input);
                input.Active = active.Contains(input);
            }

            InitializeMeters();
        }

        // What is wrong with the activity: an input that is not what the scene items and the scenes make it.
        private IEnumerable<string> FindActivityProblems()
        {
            foreach (var problem in FindMeterProblems())
            {
                yield return problem;
            }

            var (active, showing) = ReachableSources();
            foreach (var input in _inputs)
            {
                if (input.Active != active.Contains(input) || input.Showing != showing.Contains(input))
                {
                    yield return $"Input \"{input.Name}\" is {(input.Active ? "on the air" : "off the air")} and {(input.Showing ? "shown" : "not shown")}, and what is on the air makes it {(active.Contains(input) ? "on the air" : "off the air")} and {(showing.Contains(input) ? "shown" : "not shown")}.";
                }

                if (input.Active && !input.Showing)
                {
                    yield return $"Input \"{input.Name}\" is on the air and not shown.";
                }
            }
        }
    }
}
