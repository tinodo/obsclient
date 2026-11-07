namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;

    /// <summary>
    /// What removing a scene or an input is made of. OBS Studio keeps a source that was removed alive for as long as scene items show it, and its name stays taken, so removing
    /// one for good is: remove the source (when it is not removed yet), and then each scene item that shows it. The last scene item destroys it.
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
