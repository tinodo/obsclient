namespace OBSStudioClient.Model
{
    using System.Threading.Channels;
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model.Engine;

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
        /// Gets what says in the terms of the model why OBS Studio refused a request of the change (its position in the batch, and the answer), or null to leave it to the
        /// default: not ready, "not found" (the subject was removed), and the refusal as OBS Studio says it. For a refusal that the model can explain (the name is taken, and
        /// by what), and for a change of several requests, where "not found" can be about any of them.
        /// </summary>
        public Func<int, RequestResponseMessage, Exception?>? Map { get; init; }

        /// <summary>
        /// Gets what says that the model shows the change. The loop of the model calls it after every step that publishes, so it only reads the objects of the model; the
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
        /// Gets whether the line waits until the model shows the change before the next change has its turn. The next change is built from what the model shows then: a filter has no id, so the
        /// change that follows a rename of it has to say its new name. The wait ends when the model shows the change or, when it does not, after the time that a confirmation is given.
        /// </summary>
        public bool HoldsTheTurn { get; init; }

        /// <summary>
        /// Gets the parts of the model that the change is about. They are read again when the events did not show the change in time, and when its outcome is not known; and
        /// the changes that are about the same part are confirmed in the order in which they were made (see <see cref="ModelHost.ConfirmAsync"/>). A change whose parts depend on what
        /// the model shows when it has its turn names them in <see cref="RefreshAtTurn"/>, and what that says replaces this once the turn has come.
        /// </summary>
        public IReadOnlyList<RefreshTarget> Refresh { get; internal set; } = [];

        /// <summary>
        /// Gets what makes <see cref="Refresh"/> when the change has its turn, right after <see cref="BuildRequests"/> (the scene items that a removal removes, in the scenes that show them: a scene item
        /// that was published between the call and the turn is changed too, and its scene has to be read like the others, and confirmed in the same order).
        /// </summary>
        public Func<IReadOnlyList<RefreshTarget>>? RefreshAtTurn { get; init; }

        /// <summary>
        /// Gets what says, for a change of several requests that stopped at a refusal, what the model shows when the requests before the one that was refused (its position) have been applied,
        /// and the refused one too when it is gone (the code of the refusal says: a scene item that OBS Studio does not know), or null when there is nothing to wait for and the call may end at once
        /// (the first request was refused, and nothing was made or is gone); it is given the position and the code of the refusal. The call ends with the refusal when the model shows it, or when it was not shown in time: the model
        /// learns of what the first requests made a moment after the answer, and a call that is made again at once (the message of the refusal asks for that) would otherwise see the
        /// objects as they were, and repeat the requests that were made.
        /// </summary>
        public Func<int, RequestStatusCode, Func<bool>?>? Reconcile { get; init; }

        /// <summary>
        /// Gets what makes <see cref="Observe"/> when the change has its turn, right after <see cref="BuildRequests"/>.
        /// </summary>
        public Func<IReadOnlyList<RefreshTarget>>? ObserveAtTurn { get; init; }

        /// <summary>
        /// Gets the parts of the model that are read once more after the answer to the change, whatever the events said, before the call ends: a side effect of the change that OBS Studio
        /// announces late or not at all, and that the model can not tell from what it shows (a scene that is removed while a queued switch makes it the program scene: the model does not
        /// show yet that it is). The wait for the model keeps its place in the order of the waits about the same part until they have been read (see <see cref="ModelHost.ConfirmAsync"/>),
        /// so that a change that is asked for after it is not looked at before; a read that does not come in time is not an error: the change itself is shown.
        /// </summary>
        public IReadOnlyList<RefreshTarget> Observe { get; internal set; } = [];

        /// <summary>
        /// Gets what the observed parts (<see cref="Observe"/>) have to show, once they have been read again, for the observation to be done, or null when a read is all that is asked for.
        /// OBS Studio makes a side effect of a change a moment after it has answered (it makes another scene the program scene about a frame after it removed the program scene, 16 ms were
        /// measured, and a read in between still says the scene that was removed), so the read after the answer may not show it yet: the model is read again, by the event that OBS Studio
        /// sends or, when the events do not come in time, by the read that the wait asks for, until it does. A wait that ends without it ends as it would without the observation.
        /// </summary>
        public Func<bool>? ObservedShown { get; init; }

        /// <summary>
        /// Gets what says that the model shows the object that the change is about removed, for a change of one request: when OBS Studio does not know that object (600) the call says that
        /// it is gone (<see cref="ObsRemovedException"/>), and it ends when the model says so too, so that what the application does next (it is the same object that it holds) is not
        /// sent to an object that is gone. The wait is the one of any change (the events, and then a read of <see cref="Refresh"/>), and the call ends with the same exception when it ends
        /// without the model showing it.
        /// </summary>
        public Func<bool>? SubjectGone { get; init; }

        /// <summary>
        /// Gets the parts of the model that a read has to cover as well once the result of the change is known: what the change made has parts of its own that the model reads
        /// before it shows the object (the audio of an input, which the list of inputs does not say), and that no read of <see cref="Refresh"/> covers, because the object is not
        /// in the model before the list that shows it has been read. A part that the model does not have has nothing to read.
        /// </summary>
        public Func<object?, IReadOnlyList<RefreshTarget>>? RefreshFor { get; init; }

        /// <summary>
        /// Gets what the read covers when the events did not show the change in time: <see cref="Refresh"/>, and the parts of the result (<see cref="RefreshFor"/>) when the change has one: a change
        /// that OBS Studio refused made nothing, and has no result to name parts of.
        /// </summary>
        internal IReadOnlyList<RefreshTarget> RefreshWith(object? result)
        {
            return result is not null && this.RefreshFor is { } more ? [.. this.Refresh, .. more(result)] : this.Refresh;
        }

        /// <summary>Gets or sets the connection that the change was asked for on.</summary>
        internal int Session { get; set; }

        /// <summary>Gets or sets the scene collection that the change was asked for in.</summary>
        internal int Epoch { get; set; }

        /// <summary>
        /// Gets or sets the token of the caller. It ends the call, and a change that was not sent yet is not sent; it ends a question that is under way. It does not end the
        /// wait of the line for the answer to a change that was sent (see <see cref="ModelWriter"/>).
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
    /// the order in which they were made, by the loop of the model (see <see cref="ModelHost.ConfirmAsync"/>): the model passes through the values of changes that were made
    /// right after one another, and a confirmation that looked at the model too early would take the value that an earlier change left for its own. The wait of a change keeps
    /// its place in that order while the model is read again (when the events did not show the change in time): the waits after it are looked at when that read has been
    /// applied, not before.
    /// </para>
    /// <para>
    /// The turn belongs to the line, not to the caller. A caller that gives up (its token ends) stops waiting at once, and a change that was not sent yet is not sent. A change
    /// that was sent keeps its turn until OBS Studio has answered it, or <see cref="ObsModelOptions.WriteTimeout"/> has passed: OBS Studio may still make it, and when the
    /// next change were sent meanwhile, the two could be made in the other order. The model goes on following the change, whether the caller waits or not.
    /// </para>
    /// <para>
    /// A change that has to know what OBS Studio holds before it can say what to change (the toggle of a scene item) asks it in its turn (<see cref="WriteJob.Prepare"/>). The
    /// answer includes every change that was asked for before it, whatever the model shows at that moment.
    /// </para>
    /// <para>
    /// A change is not sent when there is no connection, when the connection is not the one that it was asked for on (that one was lost, and the client made another), when OBS
    /// Studio is changing its scene collection, or when the collection is not the one that it was asked for in; each of those is certain: nothing was changed. The model applies
    /// what the client reads a moment later (its loop is behind the client by the time that it takes to handle what has arrived), so the checks do not rest on the model alone:
    /// the connection that a batch is written on is told to the check before the batch is written, and a change of the scene collection that the client has read and the model has
    /// not applied yet stops the changes (<see cref="ModelHost.CollectionEventsRead"/>) until it has. What is left is the time between OBS Studio sending the event and the client
    /// reading it: a change that is sent then is refused by OBS Studio while it changes its scene collection (it is not ready), which is certain too. A change that was sent and
    /// not answered in time, or whose connection ended, is not certain
    /// (<see cref="ObsUnknownOutcomeException"/>): it is never sent again, the model reads again what it was about.
    /// </para>
    /// </remarks>
    internal sealed class ModelWriter
    {
        private const string NotConnectedMessage = "Not connected.";

        // The longest time that a timer can be given (the 4,294,967,294 milliseconds of a timer of the system): the options allow 24 days each for the time for the events
        // and for the reads, and the wait for the model adds them up.
        private static readonly TimeSpan LongestTimer = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

        private readonly ObsClient _client;
        private readonly ModelHost _host;
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

        // Called by the wait for the model when the time for the events has passed and the read has been asked for, before the wait has written the read down. For the tests: what
        // the loop of the model can do meanwhile (end the read).
        internal Action<Task>? AfterTheReadWasAskedFor { get; set; }

        public ModelWriter(ObsClient client, ModelHost host, ObsModelOptions options, ILogger logger)
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

            if (this._host.CollectionEventsRead != gate.CollectionEvents)
            {
                // The client has read the event of a change of the scene collection that the model has not applied yet: what the application holds is of the collection before it.
                return Task.FromException<T>(new ObsNotReadyException($"OBS Studio is changing its scene collection, and the model has not caught up yet: {job.Description} was not sent."));
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
                job.Fail(new ObjectDisposedException(nameof(ObsModel)));
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

        // The error that a failed request is: what the change says about it (WriteJob.Map; not for a question), or what OBS Studio says, in the terms of the model.
        private static Exception MapFailure(WriteJob job, int? index, RequestResponseMessage response)
        {
            if (index is { } position && job.Map?.Invoke(position, response) is { } mapped)
            {
                return mapped;
            }

            var status = response.RequestStatus;
            if (index is > 0 && status.Code == RequestStatusCode.NotReady)
            {
                // ObsNotReadyException says that nothing was changed, and the requests before this one were made (the batch stops at the first refusal).
                return new ObsResponseException(
                    status.Code,
                    $"OBS Studio was not ready for request {index + 1} of {job.Description}: it is changing its scene collection. The requests before it were made, and the ones after it were not.");
            }

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

        // The client does not check the entries of the results of a batch (see ObsClient.ProcessRequestBatchResponseMessage): one can be null, or have no status.
        private static bool IsReadable(RequestResponseMessage? result)
        {
            return result?.RequestStatus is not null;
        }

        // What has to be true when the turn of a change has come, and again when it is about to be sent: the connection and the scene collection are the ones that the change
        // was asked for in, OBS Studio is not changing its scene collection, and the object can be changed. The gate is what the loop of the model has published, and the loop can
        // be behind the client (the client has made another connection, or has read the end of a change of the scene collection, and the model does not know yet): when the batch
        // is about to be written, the client says on which connection (writtenOn), and that is the one that counts; and a change of the scene collection that the client has read
        // and the loop has not applied yet is counted (CollectionEventsRead), so that nothing is written in the collection that OBS Studio has loaded now with what the application
        // learned from the one that it left.
        private void EnsureItCanBeSent(WriteJob job, int? writtenOn = null)
        {
            var gate = this._host.Gate;
            if (gate.Session == 0 || gate.Session != job.Session || (writtenOn is { } connection && connection != job.Session))
            {
                throw new ObsNotConnectedException($"The connection to OBS Studio ended before {job.Description} was sent. Nothing was changed.");
            }

            if (gate.Switching || gate.Epoch != job.Epoch || this._host.CollectionEventsRead != gate.CollectionEvents)
            {
                throw new ObsNotReadyException($"OBS Studio changed its scene collection before {job.Description} was sent. Nothing was changed.");
            }

            job.Check();
        }

        // The same, when the batch of the change has its turn to be written on the connection: that can be much later than the turn of the change, when another message holds the
        // turn of the connection (a large request that OBS Studio does not read), and OBS Studio can have begun to change its scene collection, or the object can have been
        // removed, meanwhile. A change that is written then would be made in a collection that the application did not mean (or on an object that is not the one that it meant).
        // The connection can have been replaced too, while the loop of the model is behind the client and its gate still shows the old one: the client says which connection the batch
        // is written on, and a change that was asked for on another is not written on it.
        // Called by the client before one byte of the batch is written; what it throws is wrapped, so that it is not taken for a failure of the connection.
        private void CheckBeforeWriting(WriteJob job, int writtenOn)
        {
            try
            {
                this.EnsureItCanBeSent(job, writtenOn);
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
                throw new ObjectDisposedException(nameof(ObsModel));
            }
            catch (TimeoutException ex)
            {
                throw new ObsNotReadyException($"OBS Studio did not say in time what it holds, which {job.Description} needs to know. Nothing was changed.", ex);
            }
            catch (ObsClientException ex)
            {
                throw new ObsNotConnectedException($"The connection to OBS Studio ended while {job.Description} asked it what it holds. Nothing was changed.", ex);
            }

            if (response.Results.Length != 1 || !IsReadable(response.Results[0]))
            {
                throw new ObsClientException($"OBS Studio answered the 1 question that {job.Description} asked with {response.Results.Length} results, or with one that can not be read. Nothing was changed.");
            }

            var result = response.Results[0];
            if (!result.RequestStatus.Result)
            {
                throw MapFailure(job, null, result);
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
                            this._logger.LogError(ex, "A change of the model failed unexpectedly: {Description}", job.Description);
                            job.Fail(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogCritical(ex, "The line of changes of the model ended unexpectedly.");
            }
            finally
            {
                // What is left in the line is not sent.
                while (this._queue.Reader.TryRead(out var left))
                {
                    left.Fail(new ObjectDisposedException(nameof(ObsModel)));
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
                job.Fail(new ObjectDisposedException(nameof(ObsModel)));
                return;
            }

            IReadOnlyList<WriteRequest> requests;
            try
            {
                this.EnsureItCanBeSent(job);
                if (job.Prepare is { } prepare)
                {
                    try
                    {
                        await prepare(question => this.AskAsync(job, question)).ConfigureAwait(false);
                    }
                    catch (ObsRemovedException ex) when (job.SubjectGone is { } gone && !gone())
                    {
                        // OBS Studio does not know what the question was about (600) and the model still shows it: the call ends when the model shows it removed (see WriteJob.SubjectGone).
                        _ = this.FailWhenReconciledAsync(job, ex, gone);
                        return;
                    }

                    // The question took its time: what was true before it is looked at again.
                    this.EnsureItCanBeSent(job);
                }

                requests = job.BuildRequests();

                // What the change is about is what the model shows now, as it is for the requests: the parts that were named when the change was asked for can be behind.
                if (job.RefreshAtTurn is { } atTurn)
                {
                    job.Refresh = atTurn();
                }

                if (job.ObserveAtTurn is { } observeAtTurn)
                {
                    job.Observe = observeAtTurn();
                }
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

            if (requests.Count == 0)
            {
                // Nothing to send: there is nothing for the change to do (an input that no scene item shows has no scene item to show or hide). The turn is over, and the
                // confirmation goes on as it does for any change, so that the call ends in its place in the order of the changes.
                object? emptyResult;
                try
                {
                    emptyResult = job.ReadResponses([]);
                }
                catch (Exception ex)
                {
                    job.Fail(ex);
                    return;
                }

                _ = this.ConfirmAsync(job, emptyResult);
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
                // Only the end of the model ends the wait for the answer, not the token of the caller: the batch is on its way, OBS Studio may still make the change, and the next
                // change is not sent before this one is answered. The caller stops waiting by itself (see ExecuteAsync). The caller giving up does end the wait for the turn to
                // write the batch on the connection (when another message is being written): the batch was not written then, and there is nothing to wait for. And what was true
                // at the turn of the change has to be true when the batch is written, which can be much later (see CheckBeforeWriting).
                response = await this._client.SendRawBatchAsync(batch, (int)this._writeTimeout.TotalMilliseconds, this._stopping.Token, job.BeforeWritten, writtenOn => this.CheckBeforeWriting(job, writtenOn)).ConfigureAwait(false);
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
                // The model is disposed of while the request waits for its answer: it may have been written, and made.
                job.Fail(new ObsUnknownOutcomeException(job.Description, new ObjectDisposedException(nameof(ObsModel))));
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
            if (!results.All(IsReadable))
            {
                // The client hands a malformed answer on as it came (an entry that is null, or has no status), and what it says about the requests is nothing: OBS Studio ran the batch,
                // and may have made the change. It is not certain, like an answer that did not come, and the model reads again what the change was about.
                job.Fail(new ObsUnknownOutcomeException(job.Description, new ObsClientException($"OBS Studio answered the {requests.Count} requests of the change with a result that can not be read.")));
                this.RefreshQuietly(job.Refresh);
                return;
            }

            for (var i = 0; i < results.Length; i++)
            {
                if (!results[i].RequestStatus.Result)
                {
                    var failure = MapFailure(job, i, results[i]);
                    if (i == 0)
                    {
                        // Nothing was made before the refusal, so nothing happened that has to be observed.
                        job.Observe = [];
                    }

                    // What the model has to show before the call ends with the refusal: what the requests before the refused one made, and the refused thing when it is gone (WriteJob.Reconcile);
                    // or, for a change of one request that OBS Studio answers with 600 (it does not know what the change is about), that object removed (WriteJob.SubjectGone).
                    var reconciled = job.Reconcile?.Invoke(i, results[i].RequestStatus.Code) ?? (failure is ObsRemovedException ? job.SubjectGone : null);
                    if (reconciled is not null)
                    {
                        // The requests before the one that failed were made (and the one that failed is gone, when OBS Studio does not know it), and the model learns of that a moment after the answer: the call ends with the refusal when the model
                        // shows it (see WriteJob.Reconcile). The turn is over, and the next change goes on meanwhile.
                        _ = this.FailWhenReconciledAsync(job, failure, reconciled);
                        return;
                    }

                    // The requests before the one that failed were made: a change of several requests that stopped halfway leaves the model to read again.
                    job.Fail(failure);
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

            // The answer came: the next change has its turn. The model takes its time to show this one, and that is no reason to hold up the others, unless the next change is built from what the
            // model shows of this one (WriteJob.HoldsTheTurn).
            if (job.HoldsTheTurn)
            {
                await this.ConfirmAsync(job, result).ConfigureAwait(false);
            }
            else
            {
                _ = this.ConfirmAsync(job, result);
            }
        }

        // Ends the call of a change that stopped halfway with the refusal, once the model shows what the requests before it made. The wait is the one of any change (the events, and when
        // they do not show it in time a read of the parts that the change was about), and when it ends without the model showing it the call ends with the refusal all the same.
        private async Task FailWhenReconciledAsync(WriteJob job, Exception failure, Func<bool> reconciled)
        {
            try
            {
                await this.WaitForModelAsync(job, null, reconciled).ConfigureAwait(false);
                this.RefreshDetached(job);
            }
            catch (Exception ex)
            {
                // Not shown in time (ObsNotConfirmedException), or the model is disposed of: the refusal is what the call says, and the model goes on reading by itself.
                this._logger.LogDebug(ex, "The model did not show what the requests before the refusal made: {Description}", job.Description);
            }

            job.Fail(failure);
        }

        // Waits until the model shows the change: first for the events, and when they do not say it in time, for a read of the parts that it was about. The wait is not the caller's:
        // when the caller gave up, the model still has to follow the change. It always goes through the loop of the model, also when the model shows the change already: the loop
        // looks at the changes that are about the same part in the order in which they were made (see ModelHost.ConfirmAsync), and a look from here could find the value that an
        // earlier change left.
        private async Task ConfirmAsync(WriteJob job, object? result)
        {
            try
            {
                await this.WaitForModelAsync(job, result).ConfigureAwait(false);
                this.RefreshDetached(job);
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

        // OBS Studio sends no events for the scene items of a scene that it removed and keeps alive, and a change that was made there (the model could not know that the scene was
        // removed when the change was written: the event was on its way) is shown by a read only. The wait for the model ends at once for a scene that the model knows to be
        // removed (the change says that a removed scene needs no more), so the scene items of such a scene that the change was about are read here, and the model shows what OBS Studio holds.
        private void RefreshDetached(WriteJob job)
        {
            List<RefreshTarget>? targets = null;
            foreach (var target in job.Refresh)
            {
                if (target.Kind == ReadKind.Items && this.IsDetached(target.Target))
                {
                    (targets ??= []).Add(target);
                }
            }

            if (targets is not null)
            {
                this.RefreshQuietly(targets);
            }
        }

        private bool IsDetached(Guid scene)
        {
            foreach (var candidate in this._host.View.AllScenes)
            {
                if (candidate.Uuid == scene)
                {
                    return candidate.State == ObsObjectState.Detached;
                }
            }

            return false;
        }

        private bool IsShown(WriteJob job, object? result, Func<bool>? shown = null)
        {
            try
            {
                return shown is not null ? shown() : job.IsConfirmed(result);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "The check of a change failed: {Description}", job.Description);
                return false;
            }
        }

        // Waits until the model shows the change: first for the events, and when they do not say it in time, for a read of the parts that it was about. What the model has to show is what
        // the change says (WriteJob.IsConfirmed), or what is given: a change that stopped halfway waits for what its first requests made.
        //
        // It is one wait of the loop of the model from the start to the end, not a wait for the events followed by a wait for the read: the wait keeps its place in the order of the
        // waits about the same part (see ModelHost.ConfirmAsync) while the model is read again. The waits after it are not looked at before that read has been applied, because
        // what the model shows until then is what an earlier change left, and a change to the same value would be taken for shown.
        private async Task WaitForModelAsync(WriteJob job, object? result, Func<bool>? shown = null)
        {
            // The read of the model that is asked for when the events did not show the change in time.
            Task? read = null;

            // The parts that the change names for itself (WriteJob.Observe) are read once more after the answer, whatever the events said: what the change did to them (the program scene that
            // OBS Studio replaces when it removes it) is only known by a read. The wait is not done before that read has been applied, so it keeps its place in the order of the waits about
            // the same part until then (a switch that is asked for after it is not looked at before). A read that does not come in time is no error: the change itself is shown.
            var observe = job.Observe;
            Task? observation = null;

            // What the model can not show any more ends the wait: OBS Studio changed its scene collection (or the connection changed) since the change was asked for, so
            // the change was made in a collection that the model has left (or is leaving), and nothing will show it. So does the end of the read (what a read says is what
            // OBS Studio holds, whatever the events said or did not say), also when it ends in failure: the connection ended, or the model is disposed of. A read that was
            // cancelled is the end of the time that the wait was given, not the end of the read.
            bool Done() => ((shown is not null ? shown() : job.IsConfirmed(result)) && (observe.Count == 0 || (this._host.AreRefreshed(observe) && (job.ObservedShown?.Invoke() ?? true))))
                || this.Left(job)
                || Volatile.Read(ref read) is { IsCompleted: true, IsCanceled: false };

            using var events = new CancellationTokenSource(this._confirmationTimeout, this._time);
            using var limit = new CancellationTokenSource(Shortest(this._confirmationTimeout + this._readTimeout + this._readTimeout, LongestTimer), this._time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, this._stopping.Token);
            using var eventsTimedOut = events.Token.Register(() =>
            {
                // Nothing that is thrown here may leave the callback: it runs on a timer, and the application would end with it.
                IReadOnlyList<RefreshTarget> targets;
                try
                {
                    targets = job.RefreshWith(result);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "The parts that the model has to read again could not be told: {Description}", job.Description);
                    targets = job.Refresh;
                }

                if (shown is not null)
                {
                    // The change was refused, and the events did not show what the refusal says (OBS Studio does not know the object that the change was about: 600): the lists say what OBS Studio has, and
                    // an object that the model still shows and that the lists do not have is taken for removed, so that what the application does next is refused before it is sent.
                    targets = [.. targets, new RefreshTarget(ReadKind.Scenes), new RefreshTarget(ReadKind.Inputs)];
                }

                var asked = this._host.RefreshAsync(targets.Distinct().ToArray(), linked.Token);
                this.AfterTheReadWasAskedFor?.Invoke(asked);
                Volatile.Write(ref read, asked);

                // The loop of the model can have had the answer to the read, and looked at this wait, before the read was written down above: it found no read then, and it would not
                // look at the wait again before the next item, which can be long in coming. It looks again now that the read is written down.
                this._host.Recheck();
            });
            try
            {
                // Asked for before the wait is made, so that the loop marks the parts before it looks at the wait for the first time.
                if (observe.Count > 0)
                {
                    observation = this._host.RefreshAsync(observe, linked.Token);
                }

                await this._host.ConfirmAsync(Done, linked.Token, job.Refresh).ConfigureAwait(false);
            }
            catch (ObsNotConnectedException)
            {
                // The connection ended. OBS Studio has answered: the change was made, and the model is read again when the connection is restored.
            }
            catch (OperationCanceledException) when (this._stopping.IsCancellationRequested)
            {
                // The model is disposed of: the change was made, and nothing will show it.
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested)
            {
                this._logger.LogWarning("The model was not read again in time after {Description}.", job.Description);

                // The model could not be read again in time, and OBS Studio did say that it made the change: that is not an end that the call can call well. The read of the part that
                // the change was about may have been applied all the same, with other reads still to do, and then the model shows the change.
                if (!this.IsShown(job, result, shown) && !this.Left(job))
                {
                    // The time for the events and the time for the read can both be over before the callback of the first one has run (a timer that is late, a thread pool that is
                    // busy), and the callback is let go of when this wait ends: then the read was never asked for, and nothing would read the model again although the call ends
                    // with "the model reads again". It is asked for now; a read that was asked for already goes on in the model.
                    if (Volatile.Read(ref read) is null)
                    {
                        this.RefreshQuietly(job.Refresh);
                    }

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

                if (observation is not null)
                {
                    _ = this.ObserveAsync(observation);
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

            // Only the read matters, and the model goes on with it by itself: the wait for it ends after the time that a read is given, so that the waits do not pile up in the loop of the
            // model while OBS Studio keeps failing the reads (they would stay until the model is disposed of).
            var limit = new CancellationTokenSource(Shortest(this._readTimeout + this._readTimeout, LongestTimer), this._time);
            var linked = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, this._stopping.Token);
            _ = this.ObserveAsync(this._host.RefreshAsync(targets, linked.Token), limit, linked);
        }

        private async Task ObserveAsync(Task task, params IDisposable[] owned)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex, "The model could not be read again after a change whose outcome is not known.");
            }
            finally
            {
                foreach (var disposable in owned)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
