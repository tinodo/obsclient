namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when a scene, an input, a scene item, a profile or a scene collection with a given name does not exist in the model of an <see cref="Model.ObsModel"/>, or in OBS Studio.
    /// </summary>
    /// <remarks>
    /// Names are case sensitive, as they are in OBS Studio. The message says which names are close to the one that was asked for.
    /// </remarks>
    [Serializable]
    public class ObsNotFoundException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotFoundException"/> class.
        /// </summary>
        /// <param name="kind">What was looked for: "scene", "input", "scene item", "profile" or "scene collection".</param>
        /// <param name="requestedName">The name that was asked for.</param>
        /// <param name="suggestions">The names that are close to it, if there are any.</param>
        /// <param name="scope">Where it was looked for, for example "scene 'Gaming'", or null.</param>
        public ObsNotFoundException(string kind, string requestedName, IReadOnlyList<string> suggestions, string? scope = null)
            : base(BuildMessage(kind, requestedName, suggestions, scope))
        {
            this.Kind = kind;
            this.RequestedName = requestedName;
            this.Suggestions = suggestions;
        }

        /// <summary>
        /// Gets what was looked for: "scene", "input", "scene item", "profile" or "scene collection".
        /// </summary>
        public string Kind { get; } = string.Empty;

        /// <summary>
        /// Gets the name that was asked for.
        /// </summary>
        public string RequestedName { get; } = string.Empty;

        /// <summary>
        /// Gets the names that are close to the name that was asked for (at most three), or an empty list.
        /// </summary>
        public IReadOnlyList<string> Suggestions { get; } = [];

        private static string BuildMessage(string kind, string requestedName, IReadOnlyList<string> suggestions, string? scope)
        {
            var where = scope is null ? string.Empty : $" in {scope}";
            var message = $"There is no {kind} named \"{requestedName}\"{where}.";
            if (suggestions.Count > 0)
            {
                message += " Did you mean " + string.Join(" or ", suggestions.Select(name => $"\"{name}\"")) + "?";
            }

            return message;
        }
    }
}
