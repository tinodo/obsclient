namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    /// <summary>
    /// What the receive loop of an <see cref="ObsClient"/> tells the studio layer, in the order in which it happened.
    /// </summary>
    internal enum IngressKind
    {
        /// <summary>An event of OBS Studio was read.</summary>
        Event,

        /// <summary>The handshake of a connection is complete: requests can be made.</summary>
        SessionStarted,

        /// <summary>The receive loop of a connection that had started has ended.</summary>
        SessionEnded,
    }

    /// <summary>
    /// One thing that the receive loop of an <see cref="ObsClient"/> reports to the studio layer.
    /// </summary>
    /// <param name="Kind">What it is.</param>
    /// <param name="Sequence">The number of the item: unique, and increasing in the order in which the receive loop read the messages, across connections.</param>
    /// <param name="SessionId">The connection it belongs to.</param>
    /// <param name="Event">The event, for <see cref="IngressKind.Event"/>.</param>
    internal readonly record struct IngressItem(IngressKind Kind, long Sequence, int SessionId, EventMessage? Event);
}
