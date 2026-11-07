namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>What a request comes to: a status, and for a request that went well, the data of the response.</summary>
    /// <param name="Code">The code of the status.</param>
    /// <param name="Comment">The comment of the status, or null.</param>
    /// <param name="Data">The response data, or null for a request that has none.</param>
    /// <param name="NoAnswer">Whether OBS Studio never answers the request (fact 9).</param>
    /// <param name="SwitchTo">The name of the scene collection that OBS Studio switches to because of the request, or null. The request is answered when the switch is done.</param>
    /// <param name="Create">Whether the collection that is switched to is made by the request.</param>
    internal readonly record struct SimResult(int Code, string? Comment, JsonObject? Data, bool NoAnswer = false, string? SwitchTo = null, bool Create = false)
    {
        /// <summary>Gets a value indicating whether the request succeeded.</summary>
        public bool IsSuccess => Code == SimStatus.Success && !NoAnswer;

        public static SimResult Ok(JsonObject? data = null) => new(SimStatus.Success, null, data);

        public static SimResult Fail(int code, string? comment = null) => new(code, comment, null);

        public static SimResult Fail(SimFailure failure) => new(failure.Code, failure.Comment, null);

        /// <summary>Gets the result of a request that never gets an answer.</summary>
        public static SimResult Hung => new(0, null, null, true);

        /// <summary>Gets the result of a request that switches to a scene collection, and is answered with a success when OBS Studio has done so.</summary>
        public static SimResult Switching(string name, bool create) => new(SimStatus.Success, null, null, false, name, create);
    }

    /// <summary>
    /// A request, with the checks of obs-websocket 5.7.6 (<c>Request.cpp</c>) on the data that came with it, and the way it finds the
    /// scenes, inputs and scene items that it names. A check that fails keeps the status that obs-websocket answers with, in
    /// <see cref="Failure"/>, and the comment is the one of obs-websocket.
    /// </summary>
    // ASSUMPTION: the checks, their order, the statuses and the comments below were read from the source of obs-websocket 5.7.6 (Request.cpp,
    // RequestHandler.cpp, the request handlers and the helpers that they use), and not measured in a running OBS Studio, except where a
    // fact or the capture says so. One captured failure of each kind (a missing field, a field of the wrong type, a number out of range, a
    // name that is not there, a UUID that is not there) would confirm the texts and the order.
    internal sealed class SimRequest
    {
        private readonly SimWorld _world;

        /// <summary>Initializes a new instance of the <see cref="SimRequest"/> class.</summary>
        /// <param name="world">The state that the request works on.</param>
        /// <param name="data">The request data, or null when the request has none, or has data that is not an object.</param>
        public SimRequest(SimWorld world, JsonObject? data)
        {
            _world = world;
            HasData = data is not null;
            Data = data ?? new JsonObject();
        }

        /// <summary>Gets or sets a value indicating whether the request came with request data that is an object.</summary>
        public bool HasData { get; set; }

        /// <summary>Gets the request data: an object, empty when there was none.</summary>
        public JsonObject Data { get; }

        /// <summary>Gets the status that the last check that failed came to, or null.</summary>
        public SimFailure? Failure { get; private set; }

        /// <summary>Gets a value indicating whether a field is in the request data and is not null.</summary>
        public bool Contains(string key)
        {
            return Data.TryGetPropertyValue(key, out var value) && value is not null;
        }

        /// <summary>Checks that a field is there (ValidateBasic).</summary>
        public bool ValidateBasic(string key)
        {
            if (!HasData)
            {
                return Fail(SimStatus.MissingRequestData, "Your request data is missing or invalid (non-object)");
            }

            if (!Contains(key))
            {
                return Fail(SimStatus.MissingRequestField, $"Your request is missing the `{key}` field.");
            }

            return true;
        }

        /// <summary>Checks that a field that is there is a string, and not empty unless that is allowed (ValidateOptionalString).</summary>
        public bool OptionalString(string key, out string value, bool allowEmpty = false)
        {
            value = string.Empty;
            var node = Data[key]!;
            if (node.GetValueKind() != JsonValueKind.String)
            {
                return Fail(SimStatus.InvalidRequestFieldType, $"The field value of `{key}` must be a string.");
            }

            value = node.GetValue<string>();
            if (value.Length == 0 && !allowEmpty)
            {
                return Fail(SimStatus.RequestFieldEmpty, $"The field value of `{key}` must not be empty.");
            }

            return true;
        }

        /// <summary>Checks that a field is there and is a string (ValidateString).</summary>
        public bool RequireString(string key, out string value, bool allowEmpty = false)
        {
            value = string.Empty;
            return ValidateBasic(key) && OptionalString(key, out value, allowEmpty);
        }

        /// <summary>Checks that a field that is there is a number in a range (ValidateOptionalNumber).</summary>
        public bool OptionalNumber(string key, out double value, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
        {
            value = 0;
            var node = Data[key]!;
            if (!SimJson.TryGetNumber(node, out value))
            {
                return Fail(SimStatus.InvalidRequestFieldType, $"The field value of `{key}` must be a number.");
            }

            if (value < min)
            {
                return Fail(SimStatus.RequestFieldOutOfRange, $"The field value of `{key}` is below the minimum of `{Fixed(min)}`");
            }

            if (value > max)
            {
                return Fail(SimStatus.RequestFieldOutOfRange, $"The field value of `{key}` is above the maximum of `{Fixed(max)}`");
            }

            return true;
        }

        /// <summary>Checks that a field is there and is a number in a range (ValidateNumber).</summary>
        public bool RequireNumber(string key, out double value, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
        {
            value = 0;
            return ValidateBasic(key) && OptionalNumber(key, out value, min, max);
        }

        /// <summary>Checks that a field that is there is a boolean (ValidateOptionalBoolean).</summary>
        public bool OptionalBoolean(string key, out bool value)
        {
            value = false;
            var kind = Data[key]!.GetValueKind();
            if (kind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return Fail(SimStatus.InvalidRequestFieldType, $"The field value of `{key}` must be boolean.");
            }

            value = kind == JsonValueKind.True;
            return true;
        }

        /// <summary>Checks that a field is there and is a boolean (ValidateBoolean).</summary>
        public bool RequireBoolean(string key, out bool value)
        {
            value = false;
            return ValidateBasic(key) && OptionalBoolean(key, out value);
        }

        /// <summary>Checks that a field that is there is an object, and not empty unless that is allowed (ValidateOptionalObject).</summary>
        public bool OptionalObject(string key, out JsonObject value, bool allowEmpty)
        {
            value = new JsonObject();
            if (Data[key] is not JsonObject obj)
            {
                return Fail(SimStatus.InvalidRequestFieldType, $"The field value of `{key}` must be an object.");
            }

            if (obj.Count == 0 && !allowEmpty)
            {
                return Fail(SimStatus.RequestFieldEmpty, $"The field value of `{key}` must not be empty.");
            }

            value = obj;
            return true;
        }

        /// <summary>Checks that the canvas, if the request names one, is the main canvas: the only canvas that the simulation has (AcquireCanvas).</summary>
        public bool AcquireCanvas()
        {
            if (TryNonEmptyString("canvasUuid", out var uuid) && uuid != SimWorld.MainCanvasUuid)
            {
                return Fail(SimStatus.ResourceNotFound, $"No canvas was found by the UUID of `{uuid}`.");
            }

            return true;
        }

        /// <summary>
        /// Finds the source that the request names (AcquireSource). A UUID that is a string and not empty wins over a name. A UUID finds every
        /// source that is alive, also a scene that was removed; a name finds an input that is alive (also one that was removed) or a scene
        /// of the list.
        /// </summary>
        public SimSource? AcquireSource(string nameKey, string uuidKey)
        {
            if (TryNonEmptyString(uuidKey, out var uuid))
            {
                var found = _world.FindByUuid(uuid);
                if (found is null)
                {
                    Fail(SimStatus.ResourceNotFound, $"No source was found by the UUID of `{uuid}`.");
                }

                return found;
            }

            if (TryNonEmptyString(nameKey, out var name))
            {
                if (!AcquireCanvas())
                {
                    return null;
                }

                var found = _world.FindByName(name);
                if (found is null)
                {
                    Fail(SimStatus.ResourceNotFound, $"No source was found by the name of `{name}` within the canvas `{SimWorld.MainCanvasName}`.");
                }

                return found;
            }

            Fail(SimStatus.MissingRequestField, $"Your request must contain at least one of the following fields: `{nameKey}` with optional `canvasUuid` or `{uuidKey}`.");
            return null;
        }

        /// <summary>Finds the scene that the request names with <c>sceneName</c> or <c>sceneUuid</c> (AcquireScene).</summary>
        public SimScene? AcquireScene()
        {
            var source = AcquireSource("sceneName", "sceneUuid");
            if (source is null)
            {
                return null;
            }

            if (source is not SimScene scene)
            {
                Fail(SimStatus.InvalidResourceType, "The specified source is not a scene.");
                return null;
            }

            return scene;
        }

        /// <summary>Finds the input that the request names with <c>inputName</c> or <c>inputUuid</c> (AcquireInput).</summary>
        public SimInput? AcquireInput()
        {
            var source = AcquireSource("inputName", "inputUuid");
            if (source is null)
            {
                return null;
            }

            if (source is not SimInput input)
            {
                Fail(SimStatus.InvalidResourceType, "The specified source is not an input.");
                return null;
            }

            return input;
        }

        /// <summary>Finds the scene item that the request names with a scene and a <c>sceneItemId</c> (AcquireSceneItem).</summary>
        public SimItem? AcquireSceneItem()
        {
            var scene = AcquireScene();
            if (scene is null || !RequireNumber("sceneItemId", out var number, min: 0))
            {
                return null;
            }

            var id = (long)Math.Truncate(number);
            var item = scene.Items.FirstOrDefault(candidate => candidate.Id == id);
            if (item is null)
            {
                Fail(SimStatus.ResourceNotFound, $"No scene items were found in scene `{scene.Name}` with the ID `{id.ToString(CultureInfo.InvariantCulture)}`.");
            }

            return item;
        }

        // A field that is a string and is not empty, without making a failure of it: obs-websocket tries the UUID and falls back to the name.
        private bool TryNonEmptyString(string key, out string value)
        {
            value = string.Empty;
            if (!Data.TryGetPropertyValue(key, out var node) || node is null || node.GetValueKind() != JsonValueKind.String)
            {
                return false;
            }

            value = node.GetValue<string>();
            return value.Length > 0;
        }

        private bool Fail(int code, string comment)
        {
            Failure = new SimFailure(code, comment);
            return false;
        }

        // std::to_string of a double, which is what obs-websocket writes in the comment of a number that is out of range.
        private static string Fixed(double value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }
}
