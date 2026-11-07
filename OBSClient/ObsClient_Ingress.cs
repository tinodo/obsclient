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
        /// Gets the number of the current connection (the number of <see cref="IngressItem.SessionId"/>), or 0 when there is none. Every connection has a number of its own, also
        /// the one that replaces a connection that was lost.
        /// </summary>
        internal int SessionId => this._session?.Id ?? 0;

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
        /// Sets the one observer of the receive loop, when there is none yet (see <see cref="SetIngressTap"/>).
        /// </summary>
        /// <param name="tap">The observer.</param>
        /// <returns>Whether the observer was set: false when another one is set already.</returns>
        internal bool TryAttachIngressTap(Action<IngressItem> tap)
        {
            return Interlocked.CompareExchange(ref this._ingressTap, tap, null) is null;
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
        /// <param name="cancelUntilWritten">
        /// A token that ends the call only while the message waits for its turn to be written on the connection (another message is being written): it is not written then,
        /// and the call throws an <see cref="OperationCanceledException"/>. From the moment that the message is being written it has no effect: the wait for the responses
        /// ends with <paramref name="cancellationToken"/> and the time limit only.
        /// </param>
        /// <param name="beforeWriting">
        /// Called when the message has its turn to be written, before one byte of it is written, by whoever has the turn (so it must be quick: nothing else is written
        /// meanwhile), with the number of the connection that the message is about to be written on (the number of <see cref="IngressItem.SessionId"/>): the connection can have
        /// been replaced by another since the message was asked for, and a message that was asked for on one connection must not be written on the next. When it throws, the
        /// message is not written, and the call throws what it threw. For what has to be true when the message is written, and may have changed while it waited for its turn.
        /// </param>
        internal Task<RequestBatchResponseMessage> SendRawBatchAsync(RequestBatchMessage requestBatchMessage, int? timeOutInMilliseconds = null, CancellationToken cancellationToken = default, CancellationToken cancelUntilWritten = default, Action<int>? beforeWriting = null)
        {
            return this.SendBatchAsync(requestBatchMessage, timeOutInMilliseconds, cancellationToken, cancelUntilWritten, beforeWriting);
        }

        /// <summary>
        /// Takes the turn to write messages on the current connection, and returns what gives it back. What is sent meanwhile waits for its turn, as it does while a message is
        /// being written that OBS Studio does not read. For the tests of what happens to a message that waits for its turn.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait for the turn.</param>
        /// <returns>What gives the turn back when it is disposed of.</returns>
        /// <exception cref="Exceptions.ObsClientException">There is no connection.</exception>
        internal async Task<IDisposable> TakeTheTurnToWriteAsync(CancellationToken cancellationToken = default)
        {
            var session = this._session ?? throw new Exceptions.ObsClientException("Not connected.");
            await session.SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new TurnToWrite(session.SendLock);
        }

        private sealed class TurnToWrite(SemaphoreSlim sendLock) : IDisposable
        {
            private int _given;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref this._given, 1) == 0)
                {
                    sendLock.Release();
                }
            }
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
