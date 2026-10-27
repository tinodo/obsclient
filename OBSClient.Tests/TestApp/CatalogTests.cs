namespace OBSStudioClient.Tests.TestAppTests
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.Conformance;
    using OBSStudioClient.Tests.TestSupport;
    using OBSStudioClient.TestApp.Catalog;
    using System.Text.Json;

    /// <summary>
    /// The test app sends every request, and shows every event, through a catalog that is generated from the protocol (and a
    /// few descriptors that are written by hand). These tests check that the catalog is complete, and that what it sends is
    /// what the editors of the app produce.
    /// </summary>
    public class CatalogTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public void EveryRequestOfTheLibrary_IsInTheCatalogOnce()
        {
            var cataloged = RequestCatalog.All.Where(request => request.Category != "Shortcuts").Select(request => request.Name).ToList();

            Assert.Equal(cataloged.Count, cataloged.Distinct().Count());
            Assert.Equal(
                Enum.GetNames<RequestType>().OrderBy(name => name, StringComparer.Ordinal),
                cataloged.OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void EveryEventOfTheLibrary_IsInTheCatalogOnce()
        {
            var cataloged = RequestCatalog.Events.Select(descriptor => descriptor.Name).ToList();

            Assert.Equal(cataloged.Count, cataloged.Distinct().Count());
            Assert.Equal(
                Enum.GetNames<EventType>().Where(name => name != nameof(EventType.Unknown)).OrderBy(name => name, StringComparer.Ordinal),
                cataloged.OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void EveryParameter_HasWhatItsEditorNeeds()
        {
            foreach (var request in RequestCatalog.All)
            {
                Assert.Equal(request.Parameters.Count, request.Parameters.Select(parameter => parameter.Name).Distinct().Count());
                Assert.False(string.IsNullOrWhiteSpace(request.Description), $"{request.Name} has no description.");

                foreach (var parameter in request.Parameters)
                {
                    if (parameter.Kind == ParameterKind.Enum)
                    {
                        Assert.True(parameter.EnumType is { IsEnum: true }, $"{request.Name}.{parameter.Name} needs an enumeration.");
                    }

                    if (parameter.Example is not null)
                    {
                        using var document = JsonDocument.Parse(parameter.Example);
                        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
                    }
                }
            }
        }

        [Fact]
        public void TheCatalogOfTheProtocol_CanSendAndBatchEveryRequest()
        {
            Assert.All(ProtocolCatalog.Requests, request =>
            {
                Assert.NotNull(request.Invoke);
                Assert.NotNull(request.AddToBatch);
            });
        }

        [Fact]
        public void ARequestWhoseResponseHasOneField_NamesIt_SoThatTheAppCanCompareThatField()
        {
            var protocol = ProtocolSnapshot.Current;

            foreach (var request in ProtocolCatalog.Requests)
            {
                var fields = protocol.Requests[request.Name].ResponseFields.Where(field => !field.Name.Contains('.', StringComparison.Ordinal)).ToList();
                var expected = fields.Count == 1 ? fields[0].Name : null;

                Assert.True(expected == request.ResponseField, $"{request.Name}: the response field is {request.ResponseField ?? "(none)"}, the protocol has {expected ?? "none or several"}.");
            }
        }

        [Fact]
        public void EveryShortcut_NamesTheRequestItSends_AndCannotBeBatched()
        {
            var shortcuts = RequestCatalog.All.Where(request => request.Category == "Shortcuts").ToList();

            Assert.NotEmpty(shortcuts);
            foreach (var shortcut in shortcuts)
            {
                Assert.NotNull(shortcut.WireType);
                Assert.Contains(shortcut.WireType, Enum.GetNames<RequestType>());
                Assert.Null(shortcut.AddToBatch);
            }
        }

        [Fact]
        public async Task EveryRequest_IsSent_AsItself_WithTheValuesOfItsEditors()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            foreach (var request in RequestCatalog.All.Where(request => request.Invoke is not null))
            {
                var call = request.Invoke!(client, ArgumentsFor(request));
                var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);

                Assert.Equal(request.RequestType, requestType);

                await server.SendRequestSuccessAsync(requestType, requestId);
                try
                {
                    await call.WithTimeoutAsync(Timeout);
                }
                catch (Exception exception) when (exception is not TimeoutException)
                {
                    // A response without data is refused by the methods that return one. What this test is about is that the
                    // request was sent, with arguments that the method accepted.
                }
            }
        }

        [Fact]
        public void EveryRequestThatCanBeBatched_IsAddedToABatchAsItself()
        {
            foreach (var request in RequestCatalog.All.Where(request => request.AddToBatch is not null && request.Name != "Sleep"))
            {
                var batch = new RequestBatchMessage();

                request.AddToBatch!(batch, ArgumentsFor(request));

                var added = Assert.Single(batch.Requests);
                Assert.Equal(request.Name, added.RequestType.ToString());
            }
        }

        [Fact]
        public void ASleep_IsAddedToABatch_WithMillisecondsOrFrames()
        {
            var sleep = RequestCatalog.Find("Sleep")!;
            Assert.Null(sleep.Invoke);

            var batch = new RequestBatchMessage();
            var arguments = new ArgumentSet();
            arguments.Set("sleepMillis", 100);
            sleep.AddToBatch!(batch, arguments);

            Assert.Equal(RequestType.Sleep, Assert.Single(batch.Requests).RequestType);
        }

        [Fact]
        public async Task TheCatalog_HearsTheEventsOfTheClient()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var heard = new List<(string Name, object? Arguments)>();
            RequestCatalog.Subscribe(client, (name, arguments) =>
            {
                lock (heard)
                {
                    heard.Add((name, arguments));
                }
            });
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            await server.SendEventAsync("ExitStarted");
            await server.SendEventAsync("RecordFileChanged", new { newOutputPath = "/videos/recording-2.mp4" });

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (heard) { return heard.Count >= 2; } }, Timeout));
            lock (heard)
            {
                Assert.Equal("ExitStarted", heard[0].Name);
                Assert.Null(heard[0].Arguments);
                Assert.Equal("RecordFileChanged", heard[1].Name);
                var path = heard[1].Arguments!.GetType().GetProperty("NewOutputPath")!.GetValue(heard[1].Arguments);
                Assert.Equal("/videos/recording-2.mp4", path);
            }
        }

        /// <summary>Gives every parameter a value of the kind that its editor produces, so the optional parameters are exercised too.</summary>
        private static ArgumentSet ArgumentsFor(RequestDescriptor request)
        {
            var arguments = new ArgumentSet();
            foreach (var parameter in request.Parameters)
            {
                arguments.Set(parameter.Name, ValueFor(parameter));
            }

            return arguments;
        }

        private static object ValueFor(ParameterDescriptor parameter) => parameter.Kind switch
        {
            ParameterKind.Text => "text",
            ParameterKind.Integer => 1,
            ParameterKind.Number => 0.5f,
            ParameterKind.Boolean => true,
            ParameterKind.Guid => Guid.NewGuid(),
            ParameterKind.Enum => FirstMember(parameter.EnumType!),
            ParameterKind.Scene or ParameterKind.Input or ParameterKind.Source => new ReferenceValue("name", null),
            ParameterKind.JsonObject or ParameterKind.Json => JsonDocument.Parse(parameter.Example ?? "{}").RootElement.Clone(),
            _ => throw new NotSupportedException($"{parameter.Name} has the kind {parameter.Kind}, which these tests do not know."),
        };

        private static object FirstMember(Type enumType)
        {
            var values = Enum.GetValues(enumType).Cast<object>().ToList();
            return values.FirstOrDefault(value => Convert.ToInt64(value) > 0) ?? values[0];
        }
    }
}
