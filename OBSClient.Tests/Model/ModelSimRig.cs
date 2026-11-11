namespace OBSStudioClient.Tests.Model
{
    using System.Globalization;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;

    /// <summary>
    /// What an <see cref="ObsModel"/> shows, compared with what the simulation of OBS Studio really holds (see <see cref="SimSnapshot"/>). Both are written as the same
    /// lines, so that what differs is told in words: a scene or an input that only one of them has, a scene item that is somewhere else, a volume that is not the same.
    /// It uses the public objects of the model only, which is what an application sees.
    /// </summary>
    internal static class ModelMirror
    {
        // The decibels tell quiet levels apart (6 dB at -60 dB is a difference of 0.001 in the multiplier), and the multiplier is compared by its share, so that
        // neither one can hide a difference that the other shows. The model keeps the volume in single precision.
        private const double VolumeDbTolerance = 0.01;
        private const double VolumeMulShare = 0.001;
        private const double VolumeMulFloor = 0.000001;

        /// <summary>Compares the model with the simulation. Returns what differs, or null when they are the same and the model is synchronized.</summary>
        /// <param name="truth">What the simulation holds.</param>
        /// <param name="model">The model.</param>
        /// <returns>The differences, one to a line, or null.</returns>
        public static string? Compare(SimSnapshot truth, ObsModel model)
        {
            var truthLines = Project(truth);
            var modelLines = Project(model, out var modelInputs, out var disagreements);
            var problems = new List<string>();
            problems.AddRange(truthLines.Except(modelLines).Select(line => "only in OBS Studio: " + line));
            problems.AddRange(modelLines.Except(truthLines).Select(line => "only in the model:  " + line));
            problems.AddRange(disagreements);
            if (problems.Count == 0 && !truthLines.SequenceEqual(modelLines))
            {
                problems.Add("The same things are in a different order.");
            }

            foreach (var input in truth.Inputs.Where(input => input.HasAudio))
            {
                if (modelInputs.TryGetValue(input.Uuid, out var held) && !SameVolume(input, held))
                {
                    problems.Add($"volume of \"{input.Name}\": OBS Studio {Number(input.VolumeMul)} ({Number(input.VolumeDb)} dB), model {Number(held.VolumeMul)} ({Number(held.VolumeDb)} dB)");
                }
            }

            if (model.State != ObsModelState.Synchronized)
            {
                problems.Add($"The model is {model.State}, not Synchronized.");
            }

            return problems.Count == 0 ? null : string.Join(Environment.NewLine, problems);
        }

        private static bool SameVolume(SimInputSnapshot truth, ObsInput model)
        {
            return Math.Abs(model.VolumeDb - truth.VolumeDb) <= VolumeDbTolerance
                && Math.Abs(model.VolumeMul - truth.VolumeMul) <= Math.Max(VolumeMulFloor, VolumeMulShare * Math.Max(truth.VolumeMul, model.VolumeMul));
        }

        private static string Number(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        // What OBS Studio holds. A removed input that no scene item uses (a global audio device stays alive that way) cannot be reached by anybody, and nothing tells it
        // from one that was destroyed: it is not compared.
        private static List<string> Project(SimSnapshot snapshot)
        {
            var lines = new List<string> { "program scene " + snapshot.ProgramSceneUuid.ToString("D") };
            var referenced = snapshot.Scenes.Concat(snapshot.DetachedScenes).SelectMany(scene => scene.Items).Select(item => item.SourceUuid).ToHashSet();
            foreach (var scene in snapshot.Scenes)
            {
                lines.Add(SceneLine(scene.Index, scene.Uuid, scene.Name));
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked)));
            }

            foreach (var scene in snapshot.DetachedScenes.OrderBy(scene => scene.Uuid))
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked)));
            }

            foreach (var input in snapshot.Inputs.OrderBy(input => input.Uuid))
            {
                if (!input.Removed || referenced.Contains(input.Uuid))
                {
                    lines.Add(InputLine(input.Uuid, input.Name, input.Kind, input.HasAudio, input.Muted, input.Removed, AudioDetails(input.HasAudio, input.Balance, input.SyncOffset, input.MonitorType, input.Tracks), Canonical(input.Settings)));
                }
            }

            return lines;
        }

        // What the model shows: the scenes and inputs of its collections, and what a scene item reaches that the collections do not have any more (a scene or an input
        // that OBS Studio removed, and keeps alive for the scene items that use it). What is live is what the collections list, and the State of the object has to say
        // the same: Live for what is listed, and Detached (not Gone, which says that nothing shows it any more) for what a scene item reaches and the collections do not
        // list. It can disagree for a moment: a step of the model changes its objects in place, and swaps in the collections after that, and a reader that is between
        // the two sees an object that says Detached and a collection that still lists it. That is reported as a difference, so that a model is only called the same
        // as OBS Studio when the collections and the objects agree.
        private static List<string> Project(ObsModel model, out Dictionary<Guid, ObsInput> inputs, out List<string> disagreements)
        {
            var lines = new List<string> { "program scene " + (model.ProgramScene?.Uuid.ToString("D") ?? "(none)") };
            var liveScenes = model.Scenes;
            var liveInputs = model.Inputs;
            var liveSceneUuids = liveScenes.Select(scene => scene.Uuid).ToHashSet();
            var liveInputUuids = liveInputs.Select(input => input.Uuid).ToHashSet();
            var detachedScenes = new SortedDictionary<Guid, ObsScene>();
            var detachedInputs = new Dictionary<Guid, ObsInput>();
            var found = new List<string>();

            void Collect(ObsScene scene)
            {
                foreach (var item in scene.Items)
                {
                    if (item.Source is ObsScene child && !liveSceneUuids.Contains(child.Uuid))
                    {
                        if (detachedScenes.TryAdd(child.Uuid, child))
                        {
                            if (child.State != ObsObjectState.Detached)
                            {
                                found.Add($"The scene \"{child.Name}\" is reached by a scene item and is not in Scenes, but says that it is {child.State}, and not Detached.");
                            }

                            Collect(child);
                        }
                    }
                    else if (item.Source is ObsInput input && !liveInputUuids.Contains(input.Uuid))
                    {
                        if (detachedInputs.TryAdd(input.Uuid, input) && input.State != ObsObjectState.Detached)
                        {
                            found.Add($"The input \"{input.Name}\" is reached by a scene item and is not in Inputs, but says that it is {input.State}, and not Detached.");
                        }
                    }
                }
            }

            foreach (var scene in liveScenes)
            {
                if (scene.State != ObsObjectState.Live)
                {
                    found.Add($"The scene \"{scene.Name}\" is in Scenes, but says that it is {scene.State}.");
                }

                lines.Add(SceneLine(scene.Index, scene.Uuid, scene.Name));
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.Source.Uuid, item.IsEnabled, item.IsLocked)));
                Collect(scene);
            }

            foreach (var scene in detachedScenes.Values)
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.Source.Uuid, item.IsEnabled, item.IsLocked)));
                Collect(scene);
            }

            inputs = liveInputs.ToDictionary(input => input.Uuid);
            foreach (var input in liveInputs.Where(input => input.State != ObsObjectState.Live))
            {
                found.Add($"The input \"{input.Name}\" is in Inputs, but says that it is {input.State}.");
            }

            foreach (var detached in detachedInputs.Values)
            {
                inputs[detached.Uuid] = detached;
            }

            foreach (var input in inputs.Values.OrderBy(input => input.Uuid))
            {
                lines.Add(InputLine(
                    input.Uuid,
                    input.Name,
                    input.Kind,
                    input.HasAudio,
                    input.IsMuted,
                    !liveInputUuids.Contains(input.Uuid),
                    AudioDetails(input.HasAudio, input.AudioBalance, (int)input.SyncOffset.TotalMilliseconds, ModelEnums.ToWire(input.MonitorType).ToString(), (int)input.AudioTracks),
                    Canonical(input.Settings.GetRawText())));
            }

            disagreements = found;
            return lines;
        }

        private static string SceneLine(int index, Guid uuid, string name)
        {
            return $"scene [{index.ToString(CultureInfo.InvariantCulture)}] {uuid:D} \"{name}\"";
        }

        private static string ItemLine(int id, int index, Guid source, bool enabled, bool locked)
        {
            return $"    item #{id.ToString(CultureInfo.InvariantCulture)} at {index.ToString(CultureInfo.InvariantCulture)}: {source:D}{(enabled ? string.Empty : ", disabled")}{(locked ? ", locked" : string.Empty)}";
        }

        private static string InputLine(Guid uuid, string name, string kind, bool hasAudio, bool muted, bool detached, string audioDetails, string settings)
        {
            return $"input {uuid:D} \"{name}\" {kind}{(hasAudio ? (muted ? ", muted" : ", unmuted") : ", no audio")}{audioDetails}{(settings == "{}" ? string.Empty : ", settings " + settings)}{(detached ? ", removed but alive" : string.Empty)}";
        }

        private static string AudioDetails(bool hasAudio, float balance, int syncOffset, string monitorType, int tracks)
        {
            return hasAudio
                ? $", balance {balance.ToString("R", CultureInfo.InvariantCulture)}, sync offset {syncOffset.ToString(CultureInfo.InvariantCulture)} ms, {monitorType}, tracks {tracks.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
        }

        // The settings with the fields in the same order on both sides: what is compared is what the fields are, and not how they are written.
        private static string Canonical(string settings)
        {
            return SimJson.Serialize(SimJson.Sorted(System.Text.Json.Nodes.JsonNode.Parse(settings))!);
        }
    }

    /// <summary>
    /// An <see cref="ObsModel"/> and the simulation of OBS Studio that it is connected to, over a real connection on the loopback address (see
    /// <see cref="SimObsServer"/>). The simulation is changed as OBS Studio is changed by its user (<see cref="World(Action{SimExternal})"/>) or by what the client
    /// asks for, and <see cref="ConvergedAsync"/> waits until the model shows what the simulation holds.
    /// </summary>
    internal sealed class ModelSimRig : IAsyncDisposable
    {
        private readonly List<ObsChangedEventArgs> _changes = [];

        public ModelSimRig(SimQuirks? quirks = null, ObsModelOptions? options = null, int seed = 1)
        {
            this.Server = new SimObsServer(new SimObs(quirks ?? DefaultQuirks, seed));
            this.Server.Start();
            this.Model = new ObsModel(options);
            this.Model.Changed += (_, e) =>
            {
                lock (this._changes)
                {
                    this._changes.Add(e);
                }
            };
        }

        /// <summary>Gets the quirks of the simulation when a test names none: a perfect OBS Studio, with the one quirk that a client has to live with (a scene that is created is announced twice).</summary>
        public static SimQuirks DefaultQuirks { get; } = SimQuirks.Ideal with { DuplicateSceneCreated = true };

        public SimObsServer Server { get; }

        public ObsModel Model { get; }

        /// <summary>Gets what the model told through <see cref="ObsModel.Changed"/>, in order.</summary>
        public ObsChangedEventArgs[] Changes
        {
            get
            {
                lock (this._changes)
                {
                    return this._changes.ToArray();
                }
            }
        }

        /// <summary>Changes OBS Studio as its user does (or another client): the connection gets the events.</summary>
        public void World(Action<SimExternal> change)
        {
            this.Server.Run(sim => change(sim.External));
        }

        /// <summary>Changes OBS Studio as its user does, and returns what the change gives.</summary>
        public T World<T>(Func<SimExternal, T> change)
        {
            return this.Server.Run(sim => change(sim.External));
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.ConnectAsync("127.0.0.1", this.Server.Port, cancellationToken: cancellationToken);
        }

        /// <summary>Compares the model with the simulation now. Returns what differs, or null.</summary>
        public string? Compare()
        {
            var truth = this.Server.Run(sim => sim.Snapshot());
            return ModelMirror.Compare(truth, this.Model);
        }

        /// <summary>Waits until the model shows what OBS Studio holds, and is synchronized. Throws what differs when it does not in time.</summary>
        public async Task ConvergedAsync(string what, TimeSpan? timeout = null)
        {
            var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
            string? differences;
            while ((differences = this.Compare()) is not null)
            {
                if (DateTime.UtcNow > until)
                {
                    throw new TimeoutException($"The model did not show what OBS Studio holds in time: {what}.{Environment.NewLine}{differences}");
                }

                await Task.Delay(10);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await this.Model.DisposeAsync();
            await this.Server.DisposeAsync();
        }
    }
}
