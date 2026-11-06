namespace OBSStudioClient
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public sealed partial class ObsClient
    {
        // The messages that the receive loop reads are numbered 1, 2, 3, ... in the order in which it reads them, across connections. The
        // studio layer needs the order in which things ARRIVE: the handlers of the events run later, one at a time, and a response completes
        // its request at once, so a response can reach its caller before an event that arrived earlier (and a slow handler delays every
        // event behind it). Nothing here is visible outside the library.
        private long _ingressCounter;

        private Action<IngressItem>? _ingressTap;

        /// <summary>
        /// Gets the number of the last message that was read. A request that is sent after this was read is answered with a higher number.
        /// </summary>
        internal long LastIngress => Volatile.Read(ref this._ingressCounter);

        /// <summary>
        /// Sets the one observer of the receive loop, or removes it with <see langword="null"/>.
        /// </summary>
        /// <param name="tap">
        /// The observer. It is called by the receive loop, for every event (also those that nobody listens to) and for the start and the end of every
        /// connection, in the order of their numbers: it must not block, and an exception that it throws is logged.
        /// </param>
        internal void SetIngressTap(Action<IngressItem>? tap)
        {
            Volatile.Write(ref this._ingressTap, tap);
        }

        /// <summary>
        /// Sends a request, and returns the response message, which carries its number (<see cref="RequestResponseMessage.IngressSequence"/>).
        /// </summary>
        /// <param name="requestType">The type of request.</param>
        /// <param name="requestData">The request data.</param>
        /// <param name="cancellationToken">A token that ends the wait for the response.</param>
        /// <exception cref="Exceptions.ObsResponseException">OBS Studio answered with a failure.</exception>
        internal Task<RequestResponseMessage> SendRawRequestAsync(RequestType requestType, object? requestData, CancellationToken cancellationToken = default)
        {
            return this.SendRequestAndWaitAsync(requestType, requestData, cancellationToken);
        }

        /// <summary>
        /// Sends a batch, and returns the response message, which carries its number (<see cref="RequestBatchResponseMessage.IngressSequence"/>).
        /// </summary>
        /// <param name="requestBatchMessage">The batch.</param>
        /// <param name="timeOutInMilliseconds">The time limit, or null for the one of the client.</param>
        /// <param name="cancellationToken">A token that ends the wait for the responses.</param>
        internal Task<RequestBatchResponseMessage> SendRawBatchAsync(RequestBatchMessage requestBatchMessage, int? timeOutInMilliseconds = null, CancellationToken cancellationToken = default)
        {
            return this.SendBatchAsync(requestBatchMessage, timeOutInMilliseconds, cancellationToken);
        }

        private long NextIngress()
        {
            return Interlocked.Increment(ref this._ingressCounter);
        }

        private void Tap(IngressKind kind, long sequence, ConnectionSession session, EventMessage? eventMessage = null)
        {
            var tap = Volatile.Read(ref this._ingressTap);
            if (tap is null)
            {
                return;
            }

            try
            {
                tap(new IngressItem(kind, sequence, session.Id, eventMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in the ingress tap");
            }
        }

        // The handshake of a connection is complete. Also called for the Identified message that answers a Reidentify: only the first one counts.
        private void TapSessionStarted(ConnectionSession session, long sequence)
        {
            if (Interlocked.CompareExchange(ref session.IngressState, IngressStarted, IngressNotStarted) == IngressNotStarted)
            {
                this.Tap(IngressKind.SessionStarted, sequence, session);
            }
        }

        // The receive loop of a connection ended. Only a connection that had started is reported, and once.
        private void TapSessionEnded(ConnectionSession session)
        {
            if (Interlocked.CompareExchange(ref session.IngressState, IngressEnded, IngressStarted) == IngressStarted)
            {
                this.Tap(IngressKind.SessionEnded, this.NextIngress(), session);
            }
        }
    }
}
