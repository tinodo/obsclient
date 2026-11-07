namespace OBSStudioClient.Studio
{
    using System.Globalization;
    using OBSStudioClient.Exceptions;

    /// <summary>
    /// Says in the terms of the model why OBS Studio refused a name: what holds it.
    /// </summary>
    internal static class StudioNames
    {
        private const int ItemsNamed = 3;

        /// <summary>
        /// Makes the exception for a name that OBS Studio refused because it is taken (601), from what the model shows at this moment.
        /// </summary>
        /// <param name="studio">The studio.</param>
        /// <param name="operation">What was refused, for the message: "creating scene 'Gaming'".</param>
        /// <param name="name">The name that was refused.</param>
        /// <param name="forScene">Whether the name is for a scene: what can hold the name of a scene is looked for among the scenes first, and what can hold the name of an input among the inputs.</param>
        /// <param name="requester">The object that is renamed, or null when something is created: an object that already has the name is told so.</param>
        /// <returns>The exception.</returns>
        internal static ObsNameInUseException InUse(ObsStudio studio, string operation, string name, bool forScene, ObsSource? requester)
        {
            // What holds the name: a scene or an input that is alive and that the collections list, or else a source that OBS Studio removed and keeps alive.
            var holder = FindLive(studio, name, forScene);
            IReadOnlyList<ObsSceneItem> keepingItAlive = [];
            if (holder is null)
            {
                // A source that was removed stays in the list of sources of OBS Studio, with its name taken, for as long as scene items show it.
                keepingItAlive = studio.FindItemsOfRemovedSource(name, forScene);
                holder = keepingItAlive.Count > 0 ? keepingItAlive[0].Source : null;
            }

            string reason;
            if (holder is not null && ReferenceEquals(holder, requester))
            {
                reason = $"the {Kind(holder)} already has the name '{name}'";
            }
            else if (keepingItAlive.Count > 0)
            {
                var kind = Kind(holder!);
                reason = $"{(holder is ObsScene ? "a" : "an")} {kind} named '{name}' was removed, but OBS Studio keeps it alive because {Count(keepingItAlive.Count, "scene item")} still show{(keepingItAlive.Count == 1 ? "s" : string.Empty)} it ({Describe(keepingItAlive)}), "
                    + $"and a name stays taken until the last of them is gone. Call RemoveAsync() on the {kind} (HeldBy of this exception) to destroy it and free the name";
            }
            else if (holder is ObsScene)
            {
                reason = forScene
                    ? $"a scene named '{name}' exists already"
                    : $"a scene named '{name}' exists already, and an input can not have the name of a scene";
            }
            else if (holder is ObsInput)
            {
                reason = forScene
                    ? $"an input named '{name}' exists already, and this OBS Studio does not let a scene have the name of an input (obs-websocket before 5.7 does not)"
                    : $"an input named '{name}' exists already";
            }
            else
            {
                reason = $"OBS Studio says that a source named '{name}' exists already, and the model does not show which (it can be a few milliseconds behind)";
            }

            return new ObsNameInUseException(name, $"OBS Studio refused {operation}: {reason}. Nothing was changed.", holder);
        }

        // What holds a name among the sources that are alive and that the collections list: for the name of a scene a scene first, for the name of an input an input first. An object
        // that says that it was removed is not live, also when the collection still lists it: the model changes the objects first, and swaps the collections after that.
        private static ObsSource? FindLive(ObsStudio studio, string name, bool forScene)
        {
            ObsSource? scene = null;
            foreach (var candidate in studio.Scenes)
            {
                if (candidate.State == ObsObjectState.Live && string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    scene = candidate;
                    break;
                }
            }

            ObsSource? input = null;
            foreach (var candidate in studio.Inputs)
            {
                if (candidate.State == ObsObjectState.Live && string.Equals(candidate.Name, name, StringComparison.Ordinal))
                {
                    input = candidate;
                    break;
                }
            }

            return forScene ? scene ?? input : input ?? scene;
        }

        private static string Kind(ObsSource source)
        {
            return source is ObsScene ? "scene" : "input";
        }

        private static string Count(int count, string noun)
        {
            return count == 1 ? $"1 {noun}" : string.Create(CultureInfo.InvariantCulture, $"{count} {noun}s");
        }

        private static string Describe(IReadOnlyList<ObsSceneItem> items)
        {
            var parts = items.Take(ItemsNamed).Select(item => $"scene item {item.Id} of scene '{item.Scene.Name}'").ToList();
            if (items.Count > ItemsNamed)
            {
                parts.Add($"and {items.Count - ItemsNamed} more");
            }

            return string.Join(", ", parts);
        }
    }
}
