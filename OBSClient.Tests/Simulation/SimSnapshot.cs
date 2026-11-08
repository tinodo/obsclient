namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text;

    /// <summary>A scene item in a <see cref="SimSnapshot"/>.</summary>
    /// <param name="Id">The ID of the item, which is unique in its scene.</param>
    /// <param name="SourceUuid">The UUID of the source of the item.</param>
    /// <param name="SourceName">The name of the source of the item.</param>
    /// <param name="Index">The index of the item in its scene.</param>
    /// <param name="Enabled">Whether the item is enabled.</param>
    /// <param name="Locked">Whether the item is locked.</param>
    /// <param name="IsScene">Whether the source of the item is a scene.</param>
    internal sealed record SimItemSnapshot(int Id, Guid SourceUuid, string SourceName, int Index, bool Enabled, bool Locked, bool IsScene);

    /// <summary>A scene in a <see cref="SimSnapshot"/>.</summary>
    /// <param name="Uuid">The UUID of the scene.</param>
    /// <param name="Name">The name of the scene.</param>
    /// <param name="Index">The index of the scene in the list of scenes, or -1 for a scene that is detached from it.</param>
    /// <param name="Items">The items of the scene, in the order of their index.</param>
    internal sealed record SimSceneSnapshot(Guid Uuid, string Name, int Index, IReadOnlyList<SimItemSnapshot> Items)
    {
        public bool Equals(SimSceneSnapshot? other)
        {
            return other is not null && Uuid == other.Uuid && Name == other.Name && Index == other.Index && Items.SequenceEqual(other.Items);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Uuid, Name, Index, Items.Count);
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            Describe(text, string.Empty);
            return text.ToString().TrimEnd();
        }

        internal void Describe(StringBuilder text, string indent)
        {
            text.Append(indent).Append(Index >= 0 ? $"[{Index}] " : "[detached] ")
                .Append('"').Append(Name).Append("\" ").Append(SimSnapshot.Short(Uuid)).AppendLine();
            foreach (var item in Items)
            {
                text.Append(indent).Append("    #").Append(item.Id.ToString(CultureInfo.InvariantCulture))
                    .Append(" at ").Append(item.Index.ToString(CultureInfo.InvariantCulture))
                    .Append(item.IsScene ? ": scene \"" : ": input \"").Append(item.SourceName).Append("\" ").Append(SimSnapshot.Short(item.SourceUuid))
                    .Append(item.Enabled ? string.Empty : ", disabled")
                    .Append(item.Locked ? ", locked" : string.Empty)
                    .AppendLine();
            }
        }
    }

    /// <summary>An input in a <see cref="SimSnapshot"/>.</summary>
    /// <param name="Uuid">The UUID of the input.</param>
    /// <param name="Name">The name of the input.</param>
    /// <param name="Kind">The kind of the input.</param>
    /// <param name="HasAudio">Whether the kind of the input has audio.</param>
    /// <param name="Muted">Whether the input is muted.</param>
    /// <param name="VolumeMul">The volume of the input, as a multiplier.</param>
    /// <param name="VolumeDb">The volume of the input, in dB (-100 for a multiplier of zero).</param>
    /// <param name="Pinned">Whether the input is pinned: kept alive without a scene item.</param>
    /// <param name="Removed">Whether the input was removed but is still alive, because a scene item or a pin refers to it.</param>
    internal sealed record SimInputSnapshot(Guid Uuid, string Name, string Kind, bool HasAudio, bool Muted, double VolumeMul, double VolumeDb, bool Pinned, bool Removed)
    {
        public override string ToString()
        {
            return $"\"{Name}\" {Kind} {SimSnapshot.Short(Uuid)}"
                + (HasAudio ? $", {(Muted ? "muted" : "unmuted")}, volume {VolumeMul.ToString("0.####", CultureInfo.InvariantCulture)} ({VolumeDb.ToString("0.##", CultureInfo.InvariantCulture)} dB)" : string.Empty)
                + (Pinned ? ", pinned" : string.Empty)
                + (Removed ? ", removed" : string.Empty);
        }
    }

    /// <summary>
    /// What a simulated OBS Studio really holds, without any of its lag: the thing that a model of OBS Studio is compared with. Two snapshots
    /// are equal when everything in them is.
    /// </summary>
    /// <param name="CollectionName">The name of the scene collection.</param>
    /// <param name="Scenes">The scenes in the list of scenes, in the order of the list (index 0 first).</param>
    /// <param name="DetachedScenes">The scenes that were removed but are alive, because they are an item of another scene.</param>
    /// <param name="Inputs">Every input that is alive, in the order in which they were created.</param>
    /// <param name="ProgramSceneUuid">The UUID of the program scene.</param>
    /// <param name="PreviewSceneUuid">The UUID of the preview scene, which is null: studio mode is not simulated.</param>
    /// <param name="StudioMode">Whether studio mode is enabled, which it is not.</param>
    internal sealed record SimSnapshot(
        string CollectionName,
        IReadOnlyList<SimSceneSnapshot> Scenes,
        IReadOnlyList<SimSceneSnapshot> DetachedScenes,
        IReadOnlyList<SimInputSnapshot> Inputs,
        Guid ProgramSceneUuid,
        Guid? PreviewSceneUuid,
        bool StudioMode)
    {
        public bool Equals(SimSnapshot? other)
        {
            return other is not null
                && CollectionName == other.CollectionName
                && ProgramSceneUuid == other.ProgramSceneUuid
                && PreviewSceneUuid == other.PreviewSceneUuid
                && StudioMode == other.StudioMode
                && Scenes.SequenceEqual(other.Scenes)
                && DetachedScenes.SequenceEqual(other.DetachedScenes)
                && Inputs.SequenceEqual(other.Inputs);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CollectionName, ProgramSceneUuid, Scenes.Count, DetachedScenes.Count, Inputs.Count);
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            var program = Scenes.FirstOrDefault(scene => scene.Uuid == ProgramSceneUuid);
            text.Append("collection \"").Append(CollectionName).Append("\", program scene ")
                .Append(program is null ? "(none)" : $"\"{program.Name}\" {Short(ProgramSceneUuid)}")
                .Append(StudioMode ? ", studio mode on" : ", studio mode off").AppendLine();
            text.AppendLine("scenes:");
            foreach (var scene in Scenes)
            {
                scene.Describe(text, "  ");
            }

            if (DetachedScenes.Count > 0)
            {
                text.AppendLine("detached scenes (removed, still alive):");
                foreach (var scene in DetachedScenes)
                {
                    scene.Describe(text, "  ");
                }
            }

            text.AppendLine("inputs:");
            foreach (var input in Inputs)
            {
                text.Append("  ").AppendLine(input.ToString());
            }

            return text.ToString().TrimEnd();
        }

        internal static string Short(Guid uuid)
        {
            return uuid.ToString("D")[..8];
        }
    }
}
