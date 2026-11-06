namespace OBSStudioClient.Tests.Simulation
{
    using System.Text;
    using System.Text.Json;

    /// <summary>One message of a simulated OBS Studio, read: an event, the response to a request, or the response to a batch.</summary>
    internal sealed class SimEntry
    {
        public SimEntry(SimMessage message)
        {
            Message = message;
            Document = JsonDocument.Parse(message.Json);
            var d = Document.RootElement.GetProperty("d");
            Data = default;
            switch (message.Op)
            {
                case SimOp.Event:
                    Name = d.GetProperty("eventType").GetString()!;
                    Intent = d.GetProperty("eventIntent").GetInt32();
                    Data = d.TryGetProperty("eventData", out var eventData) ? eventData : default;
                    break;
                case SimOp.RequestResponse:
                    Name = d.GetProperty("requestType").GetString()!;
                    RequestId = d.GetProperty("requestId").GetString();
                    Code = d.GetProperty("requestStatus").GetProperty("code").GetInt32();
                    Comment = d.GetProperty("requestStatus").TryGetProperty("comment", out var comment) ? comment.GetString() : null;
                    Data = d.TryGetProperty("responseData", out var responseData) ? responseData : default;
                    break;
                default:
                    Name = "RequestBatch";
                    RequestId = d.GetProperty("requestId").GetString();
                    Code = SimStatus.Success;
                    Data = d.GetProperty("results");
                    break;
            }
        }

        public SimMessage Message { get; }

        public JsonDocument Document { get; }

        /// <summary>Gets the type of the event or of the request, or "RequestBatch".</summary>
        public string Name { get; }

        public int Intent { get; }

        public string? RequestId { get; }

        public int Code { get; }

        public string? Comment { get; }

        /// <summary>Gets the data of the event, of the response, or the results of a batch. It is default when there is none.</summary>
        public JsonElement Data { get; }

        public TimeSpan At => Message.SentAt;

        public bool IsEvent => Message.Op == SimOp.Event;

        public bool IsResponse => Message.Op == SimOp.RequestResponse;

        public bool IsBatchResponse => Message.Op == SimOp.RequestBatchResponse;

        public bool Succeeded => Code == SimStatus.Success;

        public string String(string member) => Data.GetProperty(member).GetString()!;

        public int Int(string member) => Data.GetProperty(member).GetInt32();

        public bool Bool(string member) => Data.GetProperty(member).GetBoolean();

        public Guid Uuid(string member) => Guid.Parse(String(member));

        /// <summary>Reads a field of every object in an array member of the data, for example the names of the scenes of a list.</summary>
        public string[] Strings(string arrayMember, string field)
        {
            return Data.GetProperty(arrayMember).EnumerateArray().Select(item => item.GetProperty(field).GetString()!).ToArray();
        }

        public override string ToString()
        {
            var kind = IsEvent ? "event" : IsResponse ? $"response {(Succeeded ? "ok" : Code.ToString(System.Globalization.CultureInfo.InvariantCulture))}" : "batch response";
            return $"{(long)At.TotalMilliseconds,6} ms {kind} {Name}";
        }
    }

    /// <summary>
    /// Drives a <see cref="SimObs"/> in a test: sends requests, runs virtual time, and keeps every message that OBS Studio sent, read.
    /// </summary>
    internal sealed class SimHarness
    {
        private int _batchCounter;

        public SimHarness(SimQuirks? quirks = null, int seed = 1)
        {
            Sim = new SimObs(quirks ?? SimQuirks.Ideal, seed);
        }

        public SimObs Sim { get; }

        /// <summary>Gets every message that OBS Studio sent so far, in the order in which it sent them.</summary>
        public List<SimEntry> Log { get; } = [];

        public TimeSpan Now => Sim.Clock.Now;

        /// <summary>Gets the UUID of the scene that a new simulation starts with.</summary>
        public Guid InitialScene => Sim.Snapshot().Scenes[0].Uuid;

        /// <summary>Sends a request and returns its ID, without running time.</summary>
        public string Send(string type, object? data = null, string? requestId = null)
        {
            return Sim.Request(type, data, requestId);
        }

        /// <summary>Sends a request batch (op 8) and returns its ID, without running time.</summary>
        /// <param name="requests">The requests, as objects with a requestType, and optionally a requestId, requestData, inputVariables and outputVariables.</param>
        /// <param name="executionType">The execution type, or null to leave it out.</param>
        /// <param name="haltOnFailure">Whether to halt on failure, or null to leave it out.</param>
        /// <param name="variables">The variables to start with, or null to leave them out.</param>
        /// <param name="requestId">The ID of the batch. A new one is made when it is null.</param>
        public string SendBatch(object[] requests, int? executionType = null, bool? haltOnFailure = null, object? variables = null, string? requestId = null)
        {
            requestId ??= "batch-" + (++_batchCounter);
            var data = new Dictionary<string, object?> { ["requestId"] = requestId, ["requests"] = requests };
            if (executionType is not null)
            {
                data["executionType"] = executionType;
            }

            if (haltOnFailure is not null)
            {
                data["haltOnFailure"] = haltOnFailure;
            }

            if (variables is not null)
            {
                data["variables"] = variables;
            }

            Sim.Receive(System.Text.Json.JsonSerializer.Serialize(new { op = SimOp.RequestBatch, d = data }));
            return requestId;
        }

        /// <summary>Sends a request batch, runs virtual time until its response comes, and returns it.</summary>
        public SimEntry CallBatch(object[] requests, int? executionType = null, bool? haltOnFailure = null, object? variables = null)
        {
            return AwaitResponse(SendBatch(requests, executionType, haltOnFailure, variables));
        }

        /// <summary>Takes what OBS Studio sent so far into the log.</summary>
        public void Take()
        {
            foreach (var message in Sim.TakeDue())
            {
                Log.Add(new SimEntry(message));
            }
        }

        /// <summary>Runs virtual time for some milliseconds, and takes what OBS Studio sends meanwhile.</summary>
        public void Run(int milliseconds)
        {
            Sim.Advance(TimeSpan.FromMilliseconds(milliseconds));
            Take();
        }

        /// <summary>Runs virtual time until nothing is scheduled any more: every request has run, every message is sent, every lag has run out.</summary>
        public void Settle()
        {
            Take();
            while (Sim.NextDeadline is { } next)
            {
                Sim.AdvanceTo(next > Now ? next : Now);
                Take();
            }
        }

        /// <summary>Sends a request, runs virtual time until its response comes, and returns the response.</summary>
        public SimEntry Call(string type, object? data = null)
        {
            var requestId = Send(type, data);
            return AwaitResponse(requestId);
        }

        /// <summary>Runs virtual time until the response to a request comes, and returns it.</summary>
        public SimEntry AwaitResponse(string requestId)
        {
            Take();
            while (true)
            {
                var found = Log.FirstOrDefault(entry => entry.RequestId == requestId && !entry.IsEvent);
                if (found is not null)
                {
                    return found;
                }

                if (Sim.NextDeadline is not { } next)
                {
                    throw new InvalidOperationException($"The request {requestId} was never answered.{Environment.NewLine}{Describe()}");
                }

                Sim.AdvanceTo(next > Now ? next : Now);
                Take();
            }
        }

        /// <summary>Calls a request that has to succeed, and returns its response.</summary>
        public SimEntry Ok(string type, object? data = null)
        {
            var response = Call(type, data);
            Assert.True(response.Succeeded, $"{type} failed with {response.Code}: {response.Comment}");
            return response;
        }

        /// <summary>Creates a scene by request and returns its UUID.</summary>
        public Guid CreateScene(string name)
        {
            return Ok("CreateScene", new { sceneName = name }).Uuid("sceneUuid");
        }

        /// <summary>Creates an input by request, in a scene (by UUID), and returns its UUID and the ID of its scene item.</summary>
        public (Guid InputUuid, int ItemId) CreateInput(Guid sceneUuid, string name, string kind = "color_source_v3")
        {
            var response = Ok("CreateInput", new { sceneUuid = sceneUuid.ToString(), inputName = name, inputKind = kind });
            return (response.Uuid("inputUuid"), response.Int("sceneItemId"));
        }

        /// <summary>Puts a source in a scene by request and returns the ID of the new item.</summary>
        public int CreateSceneItem(Guid sceneUuid, Guid sourceUuid)
        {
            return Ok("CreateSceneItem", new { sceneUuid = sceneUuid.ToString(), sourceUuid = sourceUuid.ToString() }).Int("sceneItemId");
        }

        /// <summary>Lists the names of the inputs by request (GetInputList), in the order of the list.</summary>
        public string[] InputNames()
        {
            return Ok("GetInputList").Strings("inputs", "inputName");
        }

        /// <summary>Lists the items of a scene by request, as (ID, index, source name) triples.</summary>
        public List<(int Id, int Index, string Source)> Items(Guid sceneUuid)
        {
            var response = Ok("GetSceneItemList", new { sceneUuid = sceneUuid.ToString() });
            return response.Data.GetProperty("sceneItems").EnumerateArray()
                .Select(item => (item.GetProperty("sceneItemId").GetInt32(), item.GetProperty("sceneItemIndex").GetInt32(), item.GetProperty("sourceName").GetString()!))
                .ToList();
        }

        /// <summary>The events in the log, optionally only those of one type.</summary>
        public List<SimEntry> Events(string? type = null)
        {
            return Log.Where(entry => entry.IsEvent && (type is null || entry.Name == type)).ToList();
        }

        /// <summary>Forgets what was sent so far.</summary>
        public void ClearLog()
        {
            Take();
            Log.Clear();
        }

        /// <summary>Names of the messages in the log, in order: the type of an event, and the type of a response with "&gt;" in front of it.</summary>
        public string[] Names()
        {
            return Log.Select(entry => entry.IsEvent ? entry.Name : ">" + entry.Name).ToArray();
        }

        /// <summary>Names of the events in the log, in order.</summary>
        public string[] EventNames()
        {
            return Log.Where(entry => entry.IsEvent).Select(entry => entry.Name).ToArray();
        }

        /// <summary>Describes the log, for the message of a failure.</summary>
        public string Describe()
        {
            var text = new StringBuilder();
            foreach (var entry in Log)
            {
                text.AppendLine(entry.ToString());
            }

            return text.ToString();
        }
    }
}
