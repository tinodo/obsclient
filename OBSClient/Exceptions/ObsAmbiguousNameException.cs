namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when a name matches more than one object, and so does not say which one is meant.
    /// </summary>
    /// <remarks>
    /// This happens when the same source is in a scene more than once (each copy is a scene item with its own id), and when
    /// ignoring the case of names leaves more than one match. For scene items use the method that returns all the matches, or the id; for the other
    /// objects, and for the filters of a source (which have no id), use the name exactly as it is written: an exact match always wins.
    /// </remarks>
    [Serializable]
    public class ObsAmbiguousNameException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsAmbiguousNameException"/> class.
        /// </summary>
        /// <param name="kind">What was looked for: "scene", "input", "scene item" or "filter".</param>
        /// <param name="requestedName">The name that was asked for.</param>
        /// <param name="candidates">A description of each of the matches.</param>
        /// <param name="scope">Where it was looked for, for example "scene 'Gaming'", or null.</param>
        public ObsAmbiguousNameException(string kind, string requestedName, IReadOnlyList<string> candidates, string? scope = null)
            : base($"The name \"{requestedName}\" matches {candidates.Count} {kind}s{(scope is null ? string.Empty : " in " + scope)}: {string.Join(", ", candidates)}. {(kind == "filter" ? "Use the name exactly as it is written." : "Use FindAll to get them all, or the id.")}")
        {
            this.Kind = kind;
            this.RequestedName = requestedName;
            this.Candidates = candidates;
        }

        /// <summary>
        /// Gets what was looked for: "scene", "input", "scene item" or "filter".
        /// </summary>
        public string Kind { get; } = string.Empty;

        /// <summary>
        /// Gets the name that was asked for.
        /// </summary>
        public string RequestedName { get; } = string.Empty;

        /// <summary>
        /// Gets a description of each of the matches.
        /// </summary>
        public IReadOnlyList<string> Candidates { get; } = [];
    }
}
