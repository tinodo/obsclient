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
    /// One change that the application asked for: the requests that make it, what it needs to be allowed, and how to tell that the model shows it.
    /// </summary>
    internal sealed class WriteJob
    {
        private readonly TaskCompletionSource<object?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets what is done, in words, for the messages: "enable scene item 3 of scene 'Gaming'".</summary>
        public required string Description { get; init; }

        /// <summary>Gets the object that the change is about, in words, for the message of a request that OBS Studio answers "not found": "scene item 3 of scene 'Gaming'". The description when it is null.</summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Gets what throws when the change must not be sent: the object was removed. It is called when the change is asked for, and again when its turn has come.
        /// </summary>
        public Action Check { get; init; } = static () => { };

        /// <summary>
        /// Gets what makes the requests. It is called when the turn of the change has come, after the changes that were asked for before it were sent: what it reads
        /// from the model is as late as it can be.
        /// </summary>
        public required Func<IReadOnlyList<WriteRequest>> BuildRequests { get; init; }

        /// <summary>Gets what makes the result of the change from the responses (for example the id of the scene item that was made). The result goes to <see cref="IsConfirmed"/>.</summary>
        public Func<IReadOnlyList<RequestResponseMessage>, object?> ReadResponses { get; init; } = static _ => null;

        /// <summary>
        /// Gets what says that the model shows the change. It is called on the loop of the model after every step that publishes, so it only reads the objects of the studio,
        /// and it is true too when the object was removed meanwhile (the change does not show then).
        /// </summary>
        public Func<object?, bool> IsConfirmed { get; init; } = static _ => true;

        /// <summary>
        /// Gets what makes what the call returns, when the model shows the change or has been read again. It throws when the model has no way to show the result (the
        /// scene that was made is not in it).
        /// </summary>
        public Func<object?, object?> Finish { get; init; } = static result => result;

        /// <summary>Gets the parts of the model to read again when the events did not show the change in time, and when its outcome is not known.</summary>
        public IReadOnlyList<RefreshTarget> Refresh { get; init; } = [];

        /// <summary>Gets or sets the connection that the change was asked for on.</summary>
        internal int Session { get; set; }

        /// <summary>Gets or sets the scene collection that the change was asked for in.</summary>
        internal int Epoch { get; set; }

        /// <summary>Gets or sets the token of the caller.</summary>
        internal CancellationToken Cancellation { get; set; }

        /// <summary>Gets the task that ends the call.</summary>
        internal Task<object?> Task => this._completion.Task;

        internal bool IsCompleted => this._completion.Task.IsCompleted;

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
    /// A change is not sent when there is no connection, when OBS Studio is changing its scene collection, or when the collection is not the one that it was asked
    /// for in; each of those is certain: nothing was changed. A change that was sent and not answered in time, or whose connection ended, is not certain
    /// (<see cref="ObsUnknownOutcomeException"/>): it is never sent again, the model reads again what it was about.
    /// </para>
    /// </remarks>
    internal sealed class StudioWriter
    {
        private const string NotConnectedMessage = "Not connected.";

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
        /// <param name="cancellationToken">A token that ends the wait: a change that was not sent yet is not sent.</param>
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
            if (!this._queue.Writer.TryWrite(job))
            {
                return Task.FromException<T>(new ObjectDisposedException(nameof(ObsStudio)));
            }

            if (cancellationToken.CanBeCanceled)
            {
                // The call ends with the token. A change that has not been sent yet is then skipped when its turn comes; one that was sent goes on, and its outcome
                // is looked at by the model (see SendAsync).
                var registration = cancellationToken.Register(static state => ((WriteJob)state!).Cancel(), job);
                _ = job.Task.ContinueWith(_ => registration.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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

        // The turn of a change: the checks, the batch, the answer. It ends when the answer has come, and the confirmation goes on by itself.
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
                requests = job.BuildRequests();
            }
            catch (Exception ex)
            {
                job.Fail(ex);
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
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation, this._stopping.Token);
                response = await this._client.SendRawBatchAsync(batch, (int)this._writeTimeout.TotalMilliseconds, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
            {
                // The caller does not wait any more. The batch may have been sent: the model looks at what it was about.
                job.Cancel();
                this.RefreshQuietly(job.Refresh);
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

        // Waits until the model shows the change: first for the events, and when they do not say it in time, for a read of the parts that it was about.
        private async Task ConfirmAsync(WriteJob job, object? result)
        {
            try
            {
                if (!this.IsShown(job, result))
                {
                    await this.WaitForModelAsync(job, result).ConfigureAwait(false);
                }

                job.Complete(job.Finish(result));
            }
            catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
            {
                job.Cancel();
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

        private async Task WaitForModelAsync(WriteJob job, object? result)
        {
            // What the model can not show any more ends the wait: OBS Studio changed its scene collection (or the connection changed) since the change was asked for, so
            // the change was made in a collection that the model has left (or is leaving), and nothing will show it.
            bool Done() => job.IsConfirmed(result) || this.Left(job);

            using (var limit = new CancellationTokenSource(this._confirmationTimeout, this._time))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, job.Cancellation, this._stopping.Token))
            {
                try
                {
                    await this._host.ConfirmAsync(Done, linked.Token).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException) when (limit.IsCancellationRequested && !job.Cancellation.IsCancellationRequested && !this._stopping.IsCancellationRequested)
                {
                    // The events did not show it in time.
                }
                catch (ObsNotConnectedException)
                {
                    // The connection ended. OBS Studio has answered: the change was made, and the model is read again when the connection is restored.
                    return;
                }
                catch (OperationCanceledException) when (this._stopping.IsCancellationRequested)
                {
                    // The studio is disposed of: the change was made, and nothing will show it.
                    return;
                }
            }

            // What a read says is what OBS Studio holds, whatever the events said or did not say.
            using (var limit = new CancellationTokenSource(this._readTimeout + this._readTimeout, this._time))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(limit.Token, job.Cancellation, this._stopping.Token))
            using (var race = CancellationTokenSource.CreateLinkedTokenSource(linked.Token))
            {
                try
                {
                    var read = this._host.RefreshAsync(job.Refresh, race.Token);
                    var left = this._host.ConfirmAsync(() => this.Left(job), race.Token);
                    var first = await Task.WhenAny(read, left).ConfigureAwait(false);
                    await race.CancelAsync().ConfigureAwait(false);
                    _ = this.ObserveAsync(ReferenceEquals(first, read) ? left : read);
                    await first.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (limit.IsCancellationRequested && !job.Cancellation.IsCancellationRequested && !this._stopping.IsCancellationRequested)
                {
                    this._logger.LogWarning("The model was not read again in time after {Description}.", job.Description);
                }
                catch (ObsNotConnectedException)
                {
                    // The connection ended: see above.
                }
                catch (OperationCanceledException) when (this._stopping.IsCancellationRequested)
                {
                    // The studio is disposed of: see above.
                }
            }
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
