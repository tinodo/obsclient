namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Model;

    /// <summary>
    /// An OBS Studio with one scene, "Gaming", that shows one input without audio, "Camera", and one with audio, "Mic", and an input that no scene shows, "Spare"; and the model that follows it. What the
    /// model sends is kept, for the tests to look at, and what it is asked is answered as the test says.
    /// </summary>
    internal sealed class CallFixture : IAsyncDisposable
    {
        private readonly object _lock = new();
        private readonly List<(string Type, JsonElement? Data)> _sent = [];
        private readonly Dictionary<string, Func<JsonElement?, (int Code, object? Data)>> _answers = [];

        private CallFixture(ModelRig rig, Guid gaming)
        {
            this.Rig = rig;
            this.GamingUuid = gaming;
            rig.Responder.OnRequest = this.OnRequest;
        }

        public ModelRig Rig { get; }

        public Guid GamingUuid { get; }

        public ObsModel Model => this.Rig.Model;

        public ObsScene Gaming => this.Rig.Model.Scenes["Gaming"];

        public ObsInput Camera => this.Rig.Model.Inputs["Camera"];

        public ObsInput Mic => this.Rig.Model.Inputs["Mic"];

        public static ObsModelOptions DefaultOptions()
        {
            return new ObsModelOptions
            {
                ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromMilliseconds(600),
            };
        }

        public static async Task<CallFixture> StartAsync(ObsModelOptions? options = null, Action<FakeWorld>? setup = null, Action<ObsModel>? beforeConnect = null)
        {
            var gaming = Guid.Empty;
            var rig = await ModelRig.ConnectedAsync(
                world =>
                {
                    gaming = world.AddScene("Gaming");
                    var camera = world.AddInput("Camera");
                    var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    world.AddInput("Spare");
                    world.AddItem(gaming, camera, "Camera");
                    world.AddItem(gaming, mic, "Mic");
                    setup?.Invoke(world);
                },
                options ?? DefaultOptions(),
                beforeConnect: beforeConnect);
            return new CallFixture(rig, gaming);
        }

        // Answers the requests of a type with a success or an error (the data of an error is the text that OBS Studio says about it); 0 does not answer at all.
        public void Answer(string type, int code = 100, object? data = null)
        {
            lock (this._lock)
            {
                this._answers[type] = _ => (code, data);
            }
        }

        public void Answer(string type, Func<JsonElement?, (int Code, object? Data)> answer)
        {
            lock (this._lock)
            {
                this._answers[type] = answer;
            }
        }

        public void Record(string type, JsonElement? data)
        {
            lock (this._lock)
            {
                this._sent.Add((type, data?.Clone()));
            }
        }

        public JsonElement[] AllSent(string type)
        {
            lock (this._lock)
            {
                return this._sent.Where(sent => sent.Type == type).Select(sent => sent.Data ?? default).ToArray();
            }
        }

        public int Count(string type)
        {
            lock (this._lock)
            {
                return this._sent.Count(sent => sent.Type == type);
            }
        }

        public ValueTask DisposeAsync()
        {
            return this.Rig.DisposeAsync();
        }

        private (int Code, object? Data)? OnRequest(string type, JsonElement? data)
        {
            Func<JsonElement?, (int Code, object? Data)>? answer;
            lock (this._lock)
            {
                this._sent.Add((type, data?.Clone()));
                this._answers.TryGetValue(type, out answer);
            }

            return answer?.Invoke(data);
        }
    }
}
