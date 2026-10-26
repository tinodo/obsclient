namespace OBSStudioClient.Tests.Conformance
{
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Channels;

    /// <summary>
    /// Calls every request method of <see cref="ObsClient"/> and checks, against the obs-websocket protocol
    /// snapshot, what actually goes over the wire: the request type, the names and JSON types of the fields, the
    /// required fields, and that every documented field can be sent by at least one method. The server answers with
    /// the response the protocol documents for each request, and a call that then fails (for example because a response
    /// cannot be read) is a problem too.
    /// </summary>
    /// <remarks>
    /// This is what catches a field name that is mistyped (it once was: <c>inputGuid</c> instead of <c>inputUuid</c>),
    /// a field that cannot be sent at all (such as <c>canvasUuid</c> on <c>CreateInput</c>), or a response that is
    /// registered with the wrong type, without having to know about the bug in advance.
    /// </remarks>
    public class RequestConformanceTests
    {
        private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);

        // Methods of ObsClient that are not requests of the protocol.
        private static readonly HashSet<string> NonRequestMethods = new(StringComparer.Ordinal)
        {
            nameof(ObsClient.ConnectAsync),
            nameof(ObsClient.SendRequestBatchAsync),
            nameof(ObsClient.ReidentifyAsync),
        };

        // Sleep only exists inside a request batch; it cannot be sent on its own.
        private static readonly HashSet<string> BatchOnlyRequests = new(StringComparer.Ordinal) { "Sleep" };

        [Fact]
        public async Task EveryRequestMethodOfTheClient_SendsWhatTheProtocolDocuments()
        {
            var protocol = ProtocolSnapshot.Current;
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 10000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(HandshakeTimeout);
            Assert.True(await connect.WithTimeoutAsync(HandshakeTimeout), "Could not connect to the fake OBS server.");

            var received = Channel.CreateUnbounded<(string RequestType, JsonElement? Data)>();
            _ = RespondAsync(server, received.Writer, new ResponseSamples(protocol));

            var problems = new List<string>();
            var sent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var methods = typeof(ObsClient)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType) && !NonRequestMethods.Contains(method.Name))
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(method => method.GetParameters().Length)
                .ToList();

            Assert.NotEmpty(methods);

            foreach (var method in methods)
            {
                var outcome = await InvokeAsync(client, method, ArgumentFactory.Create(method), received.Reader);
                var outcomes = new List<(Exception? Failure, List<(string RequestType, JsonElement? Data)> Calls)> { outcome };

                // A method that refuses the arguments (it accepts either one value or another) is tried again with each
                // optional argument left out in turn.
                if (outcome.Calls.Count == 0 && outcome.Failure is ArgumentException or NotSupportedException)
                {
                    foreach (var index in ArgumentFactory.NullableParameterIndexes(method))
                    {
                        outcomes.Add(await InvokeAsync(client, method, ArgumentFactory.Create(method, index), received.Reader));
                    }
                }

                var anySent = false;
                foreach (var (failure, calls) in outcomes)
                {
                    foreach (var (requestType, data) in calls)
                    {
                        anySent = true;
                        problems.AddRange(ProtocolPayload.Validate(protocol, requestType, data).Select(problem => $"{ArgumentFactory.Describe(method)}: {problem}"));
                        Record(protocol, sent, requestType, data);
                    }

                    if (failure is InvalidCastException)
                    {
                        problems.Add($"{ArgumentFactory.Describe(method)}: the response is registered with another type than the method expects ({failure.Message})");
                    }
                    else if (failure is TimeoutException)
                    {
                        problems.Add($"{ArgumentFactory.Describe(method)}: did not complete");
                    }
                    else if (failure is not null && calls.Count > 0)
                    {
                        // The server answered with exactly what the protocol documents, so the call has no reason to fail
                        // (a method that refuses its arguments does so before it sends anything).
                        problems.Add($"{ArgumentFactory.Describe(method)}: failed although obs-websocket answered with a valid response ({failure.GetType().Name}: {failure.Message})");
                    }
                }

                if (!anySent)
                {
                    var failure = outcomes.Select(candidate => candidate.Failure).FirstOrDefault(candidate => candidate is not null);
                    problems.Add($"{ArgumentFactory.Describe(method)}: sent no request ({failure?.GetType().Name}: {failure?.Message})");
                }
            }

            problems.AddRange(FindUnreachable(protocol, sent, "ObsClient", BatchOnlyRequests));
            ConformanceAssert.NoProblems(problems, $"ObsClient does not conform to obs-websocket {protocol.Version}:");
        }

        [Fact]
        public void EveryBatchBuilder_PutsWhatTheProtocolDocumentsInTheRequest()
        {
            var protocol = ProtocolSnapshot.Current;
            var problems = new List<string>();
            var sent = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var builders = typeof(RequestBatchMessage)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.Name.StartsWith("Add", StringComparison.Ordinal) && method.Name.EndsWith("Request", StringComparison.Ordinal))
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(method => method.GetParameters().Length)
                .ToList();

            Assert.NotEmpty(builders);

            foreach (var builder in builders)
            {
                var requests = Build(builder, ArgumentFactory.Create(builder), problems, out var refused);

                if (requests.Count == 0 && refused)
                {
                    // The builder refuses the arguments (it accepts either one value or another): leave each optional one out in turn.
                    foreach (var index in ArgumentFactory.NullableParameterIndexes(builder))
                    {
                        requests.AddRange(Build(builder, ArgumentFactory.Create(builder, index), problems, out _));
                    }
                }

                if (requests.Count == 0)
                {
                    problems.Add($"{ArgumentFactory.Describe(builder)}: added no request");
                }

                foreach (var request in requests)
                {
                    var requestType = request.RequestType.ToString();
                    problems.AddRange(ProtocolPayload.Validate(protocol, requestType, request.RequestData).Select(problem => $"{ArgumentFactory.Describe(builder)}: {problem}"));
                    Record(protocol, sent, requestType, request.RequestData);
                }
            }

            problems.AddRange(FindUnreachable(protocol, sent, "RequestBatchMessage", new HashSet<string>(StringComparer.Ordinal)));
            ConformanceAssert.NoProblems(problems, $"RequestBatchMessage does not conform to obs-websocket {protocol.Version}:");
        }

        private static List<RequestMessage> Build(System.Reflection.MethodInfo builder, object?[] arguments, List<string> problems, out bool refused)
        {
            refused = false;
            var batch = new RequestBatchMessage();
            try
            {
                builder.Invoke(batch, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is ArgumentException or NotSupportedException)
            {
                refused = true;
                return [];
            }
            catch (TargetInvocationException exception)
            {
                problems.Add($"{ArgumentFactory.Describe(builder)}: threw {exception.InnerException?.GetType().Name}: {exception.InnerException?.Message}");
                return [];
            }

            return batch.Requests.ToList();
        }

        private static void Record(ProtocolSnapshot protocol, Dictionary<string, HashSet<string>> sent, string requestType, JsonElement? data)
        {
            if (!protocol.Requests.TryGetValue(requestType, out var request))
            {
                return;
            }

            if (!sent.TryGetValue(requestType, out var fields))
            {
                fields = new HashSet<string>(StringComparer.Ordinal);
                sent[requestType] = fields;
            }

            fields.UnionWith(ProtocolPayload.Flatten(data, request.RequestFields.Select(field => field.Name).ToList()).Keys);
        }

        // Every request has to be reachable, and so does every field of it: otherwise the client cannot do what the protocol allows.
        private static IEnumerable<string> FindUnreachable(ProtocolSnapshot protocol, Dictionary<string, HashSet<string>> sent, string subject, HashSet<string> exceptions)
        {
            foreach (var (requestType, request) in protocol.Requests.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (exceptions.Contains(requestType))
                {
                    continue;
                }

                if (!sent.TryGetValue(requestType, out var fields))
                {
                    yield return $"{subject} cannot send the request '{requestType}' at all";
                    continue;
                }

                foreach (var field in request.RequestFields.Where(field => !fields.Contains(field.Name)))
                {
                    yield return $"{subject} cannot send the field '{field.Name}' of the request '{requestType}'";
                }
            }
        }

        private static async Task<(Exception? Failure, List<(string RequestType, JsonElement? Data)> Calls)> InvokeAsync(
            ObsClient client,
            System.Reflection.MethodInfo method,
            object?[] arguments,
            ChannelReader<(string RequestType, JsonElement? Data)> received)
        {
            Exception? failure = null;
            try
            {
                var task = (Task)method.Invoke(client, arguments)!;
                await task.WaitAsync(CallTimeout);
            }
            catch (Exception exception)
            {
                failure = exception is TargetInvocationException { InnerException: not null } wrapped ? wrapped.InnerException : exception;
            }

            // The server records a request before it responds to it, so once a call is over everything it sent is in.
            var calls = new List<(string RequestType, JsonElement? Data)>();
            while (received.TryRead(out var call))
            {
                calls.Add(call);
            }

            return (failure, calls);
        }

        // Answers every request with the success response the protocol documents for it, and remembers what was asked.
        private static async Task RespondAsync(FakeObsWebSocketServer server, ChannelWriter<(string RequestType, JsonElement? Data)> received, ResponseSamples responses)
        {
            try
            {
                while (true)
                {
                    var (requestType, requestId, data) = await server.ReceiveRequestAsync(TimeSpan.FromMinutes(2));
                    await received.WriteAsync((requestType, data));
                    await server.SendRequestSuccessAsync(requestType, requestId, responses.For(requestType));
                }
            }
            catch (Exception)
            {
                // The test is over: the client disconnected or the server was disposed.
            }
            finally
            {
                received.TryComplete();
            }
        }
    }
}
