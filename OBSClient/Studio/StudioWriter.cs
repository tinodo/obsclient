namespace OBSStudioClient.Studio
{
    using System.Threading.Channels;
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// One request of a change.
    /// </summary>
    /// <param name="Type">The type of the request.</param>
    /// <param name="Data">The data of the request, or null.</param>
    internal readonly record struct WriteRequest(RequestType Type, object? Data);

    /// <summary>
    /// A question that a change asks of OBS Studio in its turn (see <see cref="WriteJob.Prepare"/>). It returns the response of OBS Studio. A question changes nothing: when it
    /// fails, it throws what says that nothing was changed.
    /// </summary>
    /// <param name="question">The request that asks.</param>
    /// <returns>The response.</returns>
    internal delegate Task<RequestResponseMessage> WriteQuestion(WriteRequest question);

    /// <summary>
    /// One change that the application asked for: the requests that make it, what it needs to be allowed, and how to tell that the model shows it.
    /// </summary>
    internal sealed class WriteJob
    {
        private readonly TaskCompletionSource<object?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenSource? _beforeWritten;

        /// <summary>Gets what is done, in words, for the messages: "enable scene item 3 of scene 'Gaming'".</summary>
        public required string Description { get; init; }

        /// <summary>Gets the object that the change is about, in words, for the message of a request that OBS Studio answers "not found": "scene item 3 of scene 'Gaming'". The description when it is null.</summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Gets what throws when the change must not be sent: the object was removed. It is called when the change is asked for, and again when its turn has come.
        /// </summary>
        public Action Check { get; init; } = static () => { };

        /// <summary>
        /// Gets what a change that has to know what OBS Studio holds asks it, in its turn and before <see cref="BuildRequests"/> (the toggle of a scene item: OBS Studio has
        /// no request that toggles it, and the model may not show yet what an earlier change made, so the state is asked at the time of the turn, when every change that was
        /// asked for before it has been answered). The question is sent as a batch of its own, and the change goes on when it is answered. A question changes nothing: when
        /// it fails, nothing was changed, and the call says so.
        /// </summary>
        public Func<WriteQuestion, Task>? Prepare { get; init; }

        /// <summary>
        /// Gets what makes the requests. It is called when the turn of the change has come, after the changes that were asked for before it were sent (and after
        /// <see cref="Prepare"/> has run): what it reads from the model is as late as it can be.
        /// </summary>
        public required Func<IReadOnlyList<WriteRequest>> BuildRequests { get; init; }

        /// <summary>Gets what makes the result of the change from the responses (for example the id of the scene item that was made). The result goes to <see cref="IsConfirmed"/>.</summary>
        public Func<IReadOnlyList<RequestResponseMessage>, object?> ReadResponses { get; init; } = static _ => null;

        /// <summary>
        /// Gets what says that the model shows the change. The loop of the model calls it after every step that publishes, so it only reads the objects of the studio; the
        /// writer calls it once more, from its own thread, when the model could not be read again in time (what it reads is published, so that is safe). It is true too when
        /// the object was removed meanwhile (the change does not show then).
        /// </summary>
        public Func<object?, bool> IsConfirmed { get; init; } = static _ => true;

        /// <summary>
        /// Gets what makes what the call returns, when the model shows the change or has been read again. It throws when the model has no way to show the result (the
        /// scene that was made is not in it).
        /// </summary>
        public Func<object?, object?> Finish { get; init; } = static result => result;

        /// <summary>
        /// Gets the parts of the model that the change is about. They are read again when the events did not show the change in time, and when its outcome is not known; and
        /// the changes that are about the same part are confirmed in the order in which they were made (see <see cref="StudioHost.ConfirmAsync"/>).
        /// </summary>
        public IReadOnlyList<RefreshTarget> Refresh { get; init; } = [];

        /// <summary>Gets or sets the connection that the change was asked for on.</summary>
        internal int Session { get; set; }

        /// <summary>Gets or sets the scene collection that the change was asked for in.</summary>
        internal int Epoch { get; set; }

        /// <summary>
        /// Gets or sets the token of the caller. It ends the call, and a change that was not sent yet is not sent; it ends a question that is under way. It does not end the
        /// wait of the line for the answer to a change that was sent (see <see cref="StudioWriter"/>).
        /// </summary>
        internal CancellationToken Cancellation { get; set; }

        /// <summary>
        /// Gets the token that ends when the caller gives up, and only matters until the batch of the change is written: it ends the wait for the turn to write it on the
        /// connection (another message is being written), so that a batch that waits for its turn is not written when the turn comes. It is ended before the call is (see
        /// <see cref="GiveUp"/>), so that nobody who sees the call end can let the turn go before the batch has been given up. It never ends when the caller has no token.
        /// </summary>
        internal CancellationToken BeforeWritten => this._beforeWritten?.Token ?? default;

        /// <summary>Gets the task that ends the call.</summary>
        internal Task<object?> Task => this._completion.Task;

        internal bool IsCompleted => this._completion.Task.IsCompleted;

        /// <summary>Makes the job end when its caller gives up: <see cref="BeforeWritten"/> is the first to end.</summary>
        internal void PrepareToGiveUp()
        {
            // No timer and no registration of its own: there is nothing to dispose of.
            this._beforeWritten = new CancellationTokenSource();
        }

        /// <summary>The caller gave up: the wait for the turn to write the batch ends first (this returns when it has), and then the call.</summary>
        internal void GiveUp()
        {
            this._beforeWritten?.Cancel();
            this.Cancel();
        }

        internal void Complete(object? result)
        {
            this._completion.TrySetResult(result);
        }

        internal void Fail(Exception exception)
        {
            this._completion.TrySetException(exception);
        }

        internal void Cancel()
        {
            this._completion.TrySetCanceled(this.Cancellation);
        }
    }

    /// <summary>
    /// Makes the changes that the application asks for: one at a time, in the order in which they were asked for, each as one serial batch, and each ends when the
    /// model shows the change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBS Studio runs requests that arrive together in no order that anybody can count on (it can answer the second before the first), and the answer to a change says
    /// that OBS Studio has made it, not that the model knows. So there is one change under way at a time: the next is sent when the answer to the one before it has
    /// come. What takes the time after the answer, the wait for the model to show the change, is not part of that turn: it goes on while the next change is sent.
    /// </para>
    /// <para>
    /// A change is confirmed when the model shows it. The changes that are about the same part of the model (the scene items of a scene, the audio of an input) are confirmed in
    /// the order in which they were made, by the loop of the model (see <see cref="StudioHost.ConfirmAsync"/>): the model passes through the values of changes that were made
    /// right after one another, and a confirmation that looked at the model too early would take the value that an earlier change left for its own. The wait of a change keeps
    /// its place in that order while the model is read again (when the events did not show the change in time): the waits after it are looked at when that read has been
    /// applied, not before.
    /// </para>
    /// <para>
    /// The turn belongs to the line, not to the caller. A caller that gives up (its token ends) stops waiting at once, and a change that was not sent yet is not sent. A change
    /// that was sent keeps its turn until OBS Studio has answered it, or <see cref="ObsStudioOptions.WriteTimeout"/> has passed: OBS Studio may still make it, and when the
    /// next change were sent meanwhile, the two could be made in the other order. The model goes on following the change, whether the caller waits or not.
    /// </para>
    /// <para>
    /// A change that has to know what OBS Studio holds before it can say what to change (the toggle of a scene item) asks it in its turn (<see cref="WriteJob.Prepare"/>). The
    /// answer includes every change that was asked for before it, whatever the model shows at that moment.
    /// </para>
    /// <para>
    /// A change is not sent when there is no connection, when OBS Studio is changing its scene collection, or when the collection is not the one that it was asked
    /// for in; each of those is certain: nothing was changed. A change that was sent and not answered in time, or whose connection ended, is not certain
    /// (<see cref="ObsUnknownOutcomeException"/>): it is never sent again, the model reads again what it was about.
    /// </para>
    /// </remarks>
    internal sealed class StudioWriter
    {
        private const string NotConnectedMessage = "Not connected.";

        // The longest time that a timer can be given (the 4,294,967,294 milliseconds of a timer of the system): the options allow 24 days each for the time for the events
        // and for the reads, and the wait for the model adds them up.
        private static readonly TimeSpan LongestTimer = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

        private readonly ObsClient _client;
        private readonly StudioHost _host;
        private readonly ILogger _logger;
        private readonly TimeProvider _time;
        private readonly TimeSpan _confirmationTimeout;
        private readonly TimeSpan _writeTimeout;
        private readonly TimeSpan _readTimeout;
        private readonly Channel<WriteJob> _queue = Channel.CreateUnbounded<WriteJob>(new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
        private readonly CancellationTokenSource _stopping = new();
        private Task _loop = Task.CompletedTask;

        // Called by ExecuteAsync when the job has been put in the line, before it returns. For the tests: what the caller can do (give up) while the line can already take the job.
        internal Action? AfterPutInTheLine { get; set; }

        public StudioWriter(ObsClient client, StudioHost host, ObsStudioOptions options, ILogger logger)
        {
            this._client = client;
            this._host = host;
            this._logger = logger;
            this._time = options.TimeProvider ?? TimeProvider.System;
            this._confirmationTimeout = options.ConfirmationTimeout;
            this._writeTimeout = options.WriteTimeout;
            this._readTimeout = options.ReadTimeout;
        }

        /// <summary>Starts the loop that sends the changes.</summary>
        public void Start()
        {
            this._loop = Task.Run(this.RunAsync);
        }

        /// <summary>Stops the loop: the changes that wait are not sent, and end with an <see cref="ObjectDisposedException"/>.</summary>
        public void Stop()
        {
            this._queue.Writer.TryComplete();
            this._stopping.Cancel();
        }

        /// <summary>
        /// Asks for a change. The change is put in the line before this returns, so changes that are asked for one after the other are made in that order.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="job">The change.</param>
        /// <param name="cancellationToken">A token that ends the call: a change that was not sent yet is not sent. A change that was sent keeps its turn until OBS Studio has answered it.</param>
        /// <returns>A task that completes when the model shows the change.</returns>
        public Task<T> ExecuteAsync<T>(WriteJob job, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<T>(cancellationToken);
            }

            var gate = this._host.Gate;
            if (gate.Session == 0)
            {
                return Task.FromException<T>(new ObsNotConnectedException($"There is no connection to OBS Studio: {job.Description} was not sent."));
            }

            if (gate.Switching)
            {
                return Task.FromException<T>(new ObsNotReadyException($"OBS Studio is changing its scene collection: {job.Description} was not sent."));
            }

            try
            {
                job.Check();
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }

            job.Session = gate.Session;
            job.Epoch = gate.Epoch;
            job.Cancellation = cancellationToken;
            if (cancellationToken.CanBeCanceled)
            {
                // The call ends with the token. A change that has not been sent yet is then skipped when its turn comes; one that was sent keeps its turn until it is
                // answered, and the model follows it all the same (see SendAsync and ConfirmAsync). A batch that waits for its turn to be written on the connection is given
                // up before the call ends. It is registered before the job is put in the line: the line can take the job at once, and a caller that gives up in between must have
                // its change given up, not sent (when the token has ended by now, the callback runs here, and the line skips the job).
                job.PrepareToGiveUp();
                var registration = cancellationToken.Register(static state => ((WriteJob)state!).GiveUp(), job);
                _ = job.Task.ContinueWith(_ => registration.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            if (!this._queue.Writer.TryWrite(job))
            {
                // The line has ended: the job ends too, which also lets go of the registration.
                job.Fail(new ObjectDisposedException(nameof(ObsStudio)));
            }
            else
            {
                this.AfterPutInTheLine?.Invoke();
            }

            return Typed<T>(job.Task);
        }

        private static async Task<T> Typed<T>(Task<object?> task)
        {
            return (T)(await task.ConfigureAwait(false))!;
        }

        // The error that a failed request is: what OBS Studio says, in the terms of the model.
        private static Exception MapFailure(WriteJob job, RequestResponseMessage response)
        {
            var status = response.RequestStatus;
            return status.Code switch
            {
                RequestStatusCode.NotReady => new ObsNotReadyException($"OBS Studio was not ready for {job.Description}: it is changing its scene collection. Nothing was changed."),
                RequestStatusCode.ResourceNotFound => new ObsRemovedException(job.Subject ?? job.Description, ObsObjectState.Gone),
                _ => new ObsResponseException(status),
            };
        }

        // A request that was refused before one byte of it was written: the client says so with this message and nothing inside. A failure while the message was being
        // written has the failure of the socket inside, and is not certain.
        internal static bool IsNotSent(Exception exception)
        {
            return exception is ObsNotConnectedException || (exception is ObsClientException { InnerException: null } && exception.Message == NotConnectedMessage);
        }

        // What has to be true when the turn of a change has come, and again when it is about to be sent: the connection and the scene collection are the ones that the change
        // was asked for in, OBS Studio is not changing its scene collection, and the object can be changed.
        private void EnsureItCanBeSent(WriteJob job)
        {
            var gate = this._host.Gate;
            if (gate.Session == 0 || gate.Session != job.Session)
            {
                throw new ObsNotConnectedException($"The connection to OBS Studio ended before {job.Description} was sent. Nothing was changed.");
            }

            if (gate.Switching || gate.Epoch != job.Epoch)
            {
                throw new ObsNotReadyException($"OBS Studio changed its scene collection before {job.Description} was sent. Nothing was changed.");
            }

            job.Check();
        }

        // The same, when the batch of the change has its turn to be written on the connection: that can be much later than the turn of the change, when another message holds the
        // turn of the connection (a large request that OBS Studio does not read), and OBS Studio can have begun to change its scene collection, or the object can have been
        // removed, meanwhile. A change that is written then would be made in a collection that the application did not mean (or on an object that is not the one that it meant).
        // Called by the client before one byte of the batch is written; what it throws is wrapped, so that it is not taken for a failure of the connection.
        private void CheckBeforeWriting(WriteJob job)
        {
            try
            {
                this.EnsureItCanBeSent(job);
            }
            catch (Exception ex)
            {
                throw new NotWrittenException(ex);
            }
        }

        // What stopped the batch of a change from being written (see CheckBeforeWriting): the exception that says so is the inner exception.
        private sealed class NotWrittenException(Exception reason) : Exception("The batch of a change was not written.", reason);

        // A question that a change asks of OBS Studio in its turn. It changes nothing, so whatever goes wrong with it is certain: nothing was changed. The caller can give up
        // while it is asked, which is no loss: unlike a change, a question that is still on its way does no harm.
        private async Task<RequestResponseMessage> AskAsync(WriteJob job, WriteRequest question)
        {
            var batch = new RequestBatchMessage(haltOnFailure: true, RequestBatchExecutionType.SerialRealtime);
            batch.AddRaw(question.Type, question.Data);
            RequestBatchResponseMessage response;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation, this._stopping.Token);
                response = await this._client.SendRawBatchAsync(batch, (int)this._writeTimeout.TotalMilliseconds, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (this._stopping.IsCancellationRequested && !job.Cancellation.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(ObsStudio));
            }
            catch (TimeoutException ex)
            {
                throw new ObsNotReadyException($"OBS Studio did not say in time what it holds, which {job.Description} needs to know. Nothing was changed.", ex);
            }
            catch (ObsClientException ex)
            {
                throw new ObsNotConnectedException($"The connection to OBS Studio ended while {job.Description} asked it what it holds. Nothing was changed.", ex);
            }

            if (response.Results.Length != 1)
            {
                throw new ObsClientException($"OBS Studio answered {response.Results.Length} results to the 1 question that {job.Description} asked. Nothing was changed.");
            }

            var result = response.Results[0];
            if (!result.RequestStatus.Result)
            {
                throw MapFailure(job, result);
            }

            return result;
        }

        private async Task RunAsync()
        {
            var reader = this._queue.Reader;
            try
            {
                while (await reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (reader.TryRead(out var job))
                    {
                        try
                        {
                            await this.SendAsync(job).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Nothing that happens to one change may end the line of the others.
                            this._logger.LogError(ex, "A change of the studio model failed unexpectedly: {Description}", job.Description);
                            job.Fail(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogCritical(ex, "The line of changes of the studio model ended unexpectedly.");
            }
            finally
            {
                // What is left in the line is not sent.
                while (this._queue.Reader.TryRead(out var left))
                {
                    left.Fail(new ObjectDisposedException(nameof(ObsStudio)));
                }
            }
        }

        // The turn of a change: the checks, the question (when the change has one), the batch, the answer. It ends when the answer has come, and the confirmation goes on by itself.
        private async Task SendAsync(WriteJob job)
        {
            if (job.IsCompleted)
            {
                // The caller gave up while the change waited.
                return;
            }

            if (this._stopping.IsCancellationRequested)
            {
                job.Fail(new ObjectDisposedException(nameof(ObsStudio)));
                return;
            }

            IReadOnlyList<WriteRequest> requests;
            try
            {
                this.EnsureItCanBeSent(job);
                if (job.Prepare is { } prepare)
                {
                    await prepare(question => this.AskAsync(job, question)).ConfigureAwait(false);

                    // The question took its time: what was true before it is looked at again.
                    this.EnsureItCanBeSent(job);
                }

                requests = job.BuildRequests();
            }
            catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
            {
                // The caller does not wait any more, and nothing that changes anything was sent.
                job.Cancel();
                return;
            }
            catch (Exception ex)
            {
                job.Fail(ex);
                return;
            }

            if (job.IsCompleted)
            {
                // The caller gave up while the question was asked.
                return;
            }

            var batch = new RequestBatchMessage(haltOnFailure: true, RequestBatchExecutionType.SerialRealtime);
            foreach (var request in requests)
            {
                batch.AddRaw(request.Type, request.Data);
            }

            RequestBatchResponseMessage response;
            try
            {
                // Only the end of the studio ends the wait for the answer, not the token of the caller: the batch is on its way, OBS Studio may still make the change, and the next
                // change is not sent before this one is answered. The caller stops waiting by itself (see ExecuteAsync). The caller giving up does end the wait for the turn to
                // write the batch on the connection (when another message is being written): the batch was not written then, and there is nothing to wait for. And what was true
                // at the turn of the change has to be true when the batch is written, which can be much later (see CheckBeforeWriting).
                response = await this._client.SendRawBatchAsync(batch, (int)this._writeTimeout.TotalMilliseconds, this._stopping.Token, job.BeforeWritten, () => this.CheckBeforeWriting(job)).ConfigureAwait(false);
            }
            catch (NotWrittenException ex)
            {
                // The batch had its turn to be written, and what was true when the change was asked for, or when its own turn came, was not true any more: nothing was written, and
                // nothing was changed.
                job.Fail(ex.InnerException!);
                return;
            }
            catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested && !this._stopping.IsCancellationRequested)
            {
                // The caller gave up while the batch waited for its turn to be written: it was not written, so nothing was changed, and nothing is left to look at.
                job.Cancel();
                return;
            }
            catch (OperationCanceledException) when (this._stopping.IsCancellationRequested)
            {
                // The studio is disposed of while the request waits for its answer: it may have been written, and made.
                job.Fail(new ObsUnknownOutcomeException(job.Description, new ObjectDisposedException(nameof(ObsStudio))));
                return;
            }
            catch (Exception ex) when (IsNotSent(ex))
            {
                job.Fail(new ObsNotConnectedException($"The connection to OBS Studio ended before {job.Description} was sent. Nothing was changed.", ex));
                return;
            }
            catch (Exception ex)
            {
                // No answer in time, or the connection ended with the batch under way: it is not known what OBS Studio did, and the batch is never sent again.
                job.Fail(new ObsUnknownOutcomeException(job.Description, ex));
                this.RefreshQuietly(job.Refresh);
                return;
            }

            var results = response.Results;
            for (var i = 0; i < results.Length; i++)
            {
                if (!results[i].RequestStatus.Result)
                {
                    // The requests before the one that failed were made: a change of several requests that stopped halfway leaves the model to read again.
                    job.Fail(MapFailure(job, results[i]));
                    if (i > 0)
                    {
                        this.RefreshQuietly(job.Refresh);
                    }

                    return;
                }
            }

            object? result;
            try
            {
                if (results.Length != requests.Count)
                {
                    throw new ObsClientException($"OBS Studio answered {results.Length} of the {requests.Count} requests of the change.");
                }

                result = job.ReadResponses(results);
            }
            catch (Exception ex)
            {
                job.Fail(new ObsUnknownOutcomeException(job.Description, ex));
                this.RefreshQuietly(job.Refresh);
                return;
            }

            // The answer came: the next change has its turn. The model takes its time to show this one, and that is no reason to hold up the others.
            _ = this.ConfirmAsync(job, result);
        }

        // Waits until the model shows the change: first for the events, and when they do not say it in time, for a read of the parts that it was about. The wait is not the caller's:
        // when the caller gave up, the model still has to follow the change. It always goes through the loop of the model, also when the model shows the change already: the loop
        // looks at the changes that are about the same part in the order in which they were made (see StudioHost.ConfirmAsync), and a look from here could find the value that an
        // earlier change left.
        private async Task ConfirmAsync(WriteJob job, object? result)
        {
            try
            {
                await this.WaitForModelAsync(job, result).ConfigureAwait(false);
                if (!job.IsCompleted)
                {
                    job.Complete(job.Finish(result));
                }
            }
            catch (Exception ex)
            {
                job.Fail(ex);
            }
        }

        private bool IsShown(WriteJob job, object? result)
        {
            try
            {
                return job.IsConfirmed(result);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "The check of a change failed: {Description}", job.Description);
                return false;
            }
        }

        // Waits until the model shows the change: first for the events, and when they do not say it in time, for a read of the parts that it was about.
        //
        // It is one wait of the loop of the model from the start to the end, not a wait for the events followed by a wait for the read: the wait keeps its place in the order of the
        // waits about the same part (see StudioHost.ConfirmAsync) while the model is read again. The waits after it are not looked at before that read has been applied, because
        // what the model shows until then is what an earlier change left, and a change to the same value would be taken for shown.
        private async Task WaitForModelAsync(WriteJob job, object? result)
        {
            // The read of the model that is asked for when the events did not show the change in time.
            Task? read = null;

            // What the model can not show any more ends the wait: OBS Studio changed its scene collection (or the connection changed) since the change was asked for, so
            // the change was made in a collection that the model has left (or is leaving), and nothing will show it. So does the end of the read (what a read says is what
            // OBS Studio holds, whatever the events said or did not say), also when it ends in failure: the connection ended, or the studio is disposed of. A read that was
            // cancelled is the end of the time that the wait was given, not the end of the read.
            bool Done() => job.IsConfirmed(result) || this.Left(job) || Volatile.Read(ref read) is { IsCompleted: true, IsCanceled: false };

            using var events = new CancellationTokenSource(this._confirmationTimeout, this._time);
            using var limit = new CancellationTokenSource(Shortest(this._confirmationTimeout + this._readTimeout + this._readTimeout, LongestTimer), this._time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, this._stopping.Token);
            using var eventsTimedOut = events.Token.Register(() => Volatile.Write(ref read, this._host.RefreshAsync(job.Refresh, linked.Token)));
            try
            {
                await this._host.ConfirmAsync(Done, linked.Token, job.Refresh).ConfigureAwait(false);
            }
            catch (ObsNotConnectedException)
            {
                // The connection ended. OBS Studio has answered: the change was made, and the model is read again when the connection is restored.
            }
            catch (OperationCanceledException) when (this._stopping.IsCancellationRequested)
            {
                // The studio is disposed of: the change was made, and nothing will show it.
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested)
            {
                this._logger.LogWarning("The model was not read again in time after {Description}.", job.Description);

                // The model could not be read again in time, and OBS Studio did say that it made the change: that is not an end that the call can call well. The read of the part that
                // the change was about may have been applied all the same, with other reads still to do, and then the model shows the change.
                if (!this.IsShown(job, result) && !this.Left(job))
                {
                    throw new ObsNotConfirmedException(job.Description);
                }
            }
            finally
            {
                // The wait is over, whatever ended it: what it still holds in the loop of the model (the wait for the read that was asked for) is let go of. The read goes on in the
                // model; only this wait for it ends, and it would otherwise stay until the model settles, which it does not while OBS Studio keeps failing the reads.
                linked.Cancel();
                if (Volatile.Read(ref read) is { } asked)
                {
                    // Whatever became of the read is of no interest any more: it was asked for to end this wait.
                    _ = this.ObserveAsync(asked);
                }
            }
        }

        private static TimeSpan Shortest(TimeSpan first, TimeSpan second)
        {
            return first < second ? first : second;
        }

        // Whether the model has left the connection and the scene collection that the change was asked for in.
        private bool Left(WriteJob job)
        {
            var gate = this._host.Gate;
            return gate.Switching || gate.Epoch != job.Epoch || gate.Session != job.Session;
        }

        private void RefreshQuietly(IReadOnlyList<RefreshTarget> targets)
        {
            if (targets.Count == 0)
            {
                return;
            }

            _ = this.ObserveAsync(this._host.RefreshAsync(targets, this._stopping.Token));
        }

        private async Task ObserveAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "The model could not be read again after a change whose outcome is not known.");
            }
        }
    }
}
