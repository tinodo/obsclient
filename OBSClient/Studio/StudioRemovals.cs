namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;

    /// <summary>
    /// What removing a scene or an input is made of. OBS Studio keeps a source that was removed alive for as long as scene items show it (and, for an input, and for a scene before
    /// obs-websocket 5.7, its name stays taken), so removing one for good is: remove the source (when it is not removed yet), and then each scene item that shows it. The last scene
    /// item destroys it.
    /// </summary>
    internal static class StudioRemovals
    {
        /// <summary>
        /// Makes the requests of a removal. The source is removed first: when OBS Studio refuses that (it is gone, or it is the last scene), nothing else was done.
        /// </summary>
        /// <param name="removal">The request that removes the source itself, or null when it is removed already (OBS Studio keeps it alive).</param>
        /// <param name="showing">The scene items that show the source, which are removed after it.</param>
        /// <returns>The requests, in the order in which they are sent as one serial batch that stops at its first failure.</returns>
        internal static IReadOnlyList<WriteRequest> Requests(WriteRequest? removal, IReadOnlyList<ObsSceneItem> showing)
        {
            var requests = new List<WriteRequest>(showing.Count + 1);
            if (removal is { } first)
            {
                requests.Add(first);
            }

            foreach (var item in showing)
            {
                requests.Add(new WriteRequest(RequestType.RemoveSceneItem, new { sceneUuid = item.Scene.Uuid, sceneItemId = item.Id }));
            }

            return requests;
        }

        /// <summary>
        /// Says what the model shows when the requests of a removal before the one that was refused have been applied: the source is removed (when the first request removed it), and the
        /// scene items before the refused one are gone. A call that is made again at once (the message of the refusal asks for that) builds its requests from what the model shows, and
        /// would repeat the ones that were made if it did not show it yet.
        /// </summary>
        /// <param name="source">The source that is removed.</param>
        /// <param name="includesRemoval">Whether the first request removes the source itself.</param>
        /// <param name="showing">The scene items that the requests after it remove.</param>
        /// <param name="index">The position of the request that was refused.</param>
        /// <returns>What says that the model shows it.</returns>
        internal static Func<bool> Reconciled(ObsSource source, bool includesRemoval, ObsSceneItem[] showing, int index)
        {
            var done = Math.Max(index - (includesRemoval ? 1 : 0), 0);
            return () => (!includesRemoval || source.IsRemoved) && showing.Take(done).All(item => item.IsRemoved);
        }

        /// <summary>
        /// Refuses the removal of a source that a scene item inside a group shows. obs-websocket removes the scene items of scenes only (<c>RemoveSceneItem</c>: "Scenes only"; a group's
        /// UUID is refused as the wrong type of resource), so the removal could not be finished: the source would be removed, and kept alive by the scene item of the group for good.
        /// </summary>
        /// <param name="source">The source that is removed.</param>
        /// <exception cref="InvalidOperationException">A scene item in a group shows the source. Nothing was changed.</exception>
        internal static void ThrowIfAGroupShows(ObsSource source)
        {
            foreach (var item in source.GetSceneItems())
            {
                if (!item.IsRemoved && item.Scene.IsGroup)
                {
                    throw new InvalidOperationException(
                        $"The {source.Label} is shown by scene item {item.Id} of the group '{item.Scene.Name}'. obs-websocket removes the scene items of scenes only (RemoveSceneItem), not those of a group, "
                        + "so it can not be removed for good from here: take it out of the group in OBS Studio first. Nothing was changed.");
                }
            }
        }

        /// <summary>
        /// Says what a refusal of one of the requests of a removal means. The refusal of the request that removes the source itself is left to the default: nothing was changed.
        /// A scene item that OBS Studio refuses to remove comes after the source was removed, and the scene items after it were not tried.
        /// </summary>
        /// <param name="description">What is done, for the message.</param>
        /// <param name="includesRemoval">Whether the first request removes the source itself.</param>
        /// <param name="showing">The scene items that the requests after it remove.</param>
        /// <param name="index">The position of the request that was refused.</param>
        /// <param name="response">The answer.</param>
        /// <returns>The exception, or null to leave it to the default.</returns>
        internal static Exception? Refusal(string description, bool includesRemoval, ObsSceneItem[] showing, int index, RequestResponseMessage response)
        {
            var position = index - (includesRemoval ? 1 : 0);
            if (position < 0 || position >= showing.Length)
            {
                return null;
            }

            var item = showing[position];
            var status = response.RequestStatus;
            return new ObsResponseException(
                status.Code,
                $"OBS Studio refused to remove scene item {item.Id} of scene '{item.Scene.Name}' while {description}: {(status.Comment ?? status.Code.ToString()).TrimEnd('.')}. "
                + "The requests before it were made, and the ones after it were not: the source is removed, and OBS Studio keeps it alive for the scene items that are left. Call RemoveAsync again to remove them.");
        }
    }
}
