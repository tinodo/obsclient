namespace OBSStudioClient.Model
{
    using OBSStudioClient.Responses;

    public sealed partial class ObsModel
    {
        // What the connection said about its OBS Studio (GetVersion). The model keeps the last that it was told, like everything else that it holds: a new connection replaces it when that says its own.
        private static readonly IReadOnlyList<string> NoFormats = Array.AsReadOnly(Array.Empty<string>());

        private volatile VersionFacts? _versionFacts;

        /// <summary>
        /// Gets the version of OBS Studio that the model is connected to (for example 33.0.0), or null until a connection has said it, and when it said it in a form that cannot be read.
        /// </summary>
        /// <remarks>
        /// The model keeps the last value when the connection is lost. A reconnect can be to another OBS Studio: the value is replaced when the new connection says its version, and
        /// PropertyChanged tells when it changed.
        /// </remarks>
        public Version? ObsVersion => this._versionFacts?.ObsVersion;

        /// <summary>
        /// Gets the version of obs-websocket that the model is connected to (for example 5.7.6), or null until a connection has said it. It decides which requests OBS Studio has: some come with
        /// a newer version (splitting a recording needs 5.5.0, and canvases 5.7.0).
        /// </summary>
        public Version? WebSocketVersion => this._versionFacts?.WebSocketVersion;

        /// <summary>
        /// Gets the platform that OBS Studio runs on, as it says it ("windows", "macos", "ubuntu"...), or null until a connection has said it.
        /// </summary>
        public string? Platform => this._versionFacts?.Platform;

        /// <summary>
        /// Gets the image formats that OBS Studio can make a screenshot in (<c>png</c>, <c>jpg</c> and so on), as it said when it connected: the formats that <see cref="ObsSource.GetScreenshotAsync"/> can ask for.
        /// It is empty until a connection has said it.
        /// </summary>
        public IReadOnlyList<string> SupportedImageFormats => this._versionFacts?.SupportedImageFormats ?? NoFormats;

        // Called by the loop of the model when a connection says which version of OBS Studio it is connected to.
        internal void SetVersionFacts(VersionResponse response, Version? obsVersion, Version? webSocketVersion)
        {
            var old = this._versionFacts;
            var facts = new VersionFacts(obsVersion, webSocketVersion, response.Platform, Array.AsReadOnly([.. response.SupportedImageFormats]));
            this._versionFacts = facts;
            this.NotifyIfChanged(nameof(this.ObsVersion), old?.ObsVersion, facts.ObsVersion);
            this.NotifyIfChanged(nameof(this.WebSocketVersion), old?.WebSocketVersion, facts.WebSocketVersion);
            this.NotifyIfChanged(nameof(this.Platform), old?.Platform, facts.Platform);

            // What an application can see is what counts: a connection that says no formats, to a model that knew none, changes nothing.
            var oldFormats = old?.SupportedImageFormats ?? NoFormats;
            if (!oldFormats.SequenceEqual(facts.SupportedImageFormats, StringComparer.Ordinal))
            {
                this.QueueNotice(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, null, nameof(this.SupportedImageFormats), oldFormats, facts.SupportedImageFormats), this, nameof(this.SupportedImageFormats)));
            }
        }

        private void NotifyIfChanged(string property, object? oldValue, object? newValue)
        {
            if (!Equals(oldValue, newValue))
            {
                this.QueueNotice(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, null, property, oldValue, newValue), this, property));
            }
        }

        private sealed record VersionFacts(Version? ObsVersion, Version? WebSocketVersion, string? Platform, IReadOnlyList<string> SupportedImageFormats);
    }
}
