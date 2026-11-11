namespace OBSStudioClient.Model
{
    /// <summary>
    /// A parameter of the configuration of a profile of OBS Studio (the file <c>basic.ini</c> of the profile): what it is now, and what OBS Studio gives it when nobody set it.
    /// </summary>
    /// <remarks>
    /// Every value is a string, whatever it means (<c>Video</c>/<c>BaseCX</c> is <c>"1920"</c>). It is a snapshot: <see cref="ObsProfiles.GetParameterAsync"/> asks again.
    /// </remarks>
    public sealed class ObsProfileParameter
    {
        internal ObsProfileParameter(string category, string name, string? value, string? defaultValue)
        {
            this.Category = category;
            this.Name = name;
            this.Value = value;
            this.DefaultValue = defaultValue;
        }

        /// <summary>
        /// Gets the category (the section of the file) that the parameter is in: <c>Video</c>, <c>SimpleOutput</c>, <c>AdvOut</c>, <c>Output</c>, ...
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the name of the parameter in its category: <c>BaseCX</c>, <c>FilePath</c>, ...
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the value of the parameter, or null when nobody set it and OBS Studio has no default for it.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Gets the value that OBS Studio gives the parameter when nobody set it, or null when it has none.
        /// </summary>
        public string? DefaultValue { get; }

        /// <summary>
        /// Gets whether the parameter has a value: it was set, or OBS Studio has a default for it.
        /// </summary>
        public bool HasValue => this.Value is not null;

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.Category}/{this.Name} = {(this.Value is null ? "(none)" : $"'{this.Value}'")}";
        }
    }
}
