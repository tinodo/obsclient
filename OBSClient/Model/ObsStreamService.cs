namespace OBSStudioClient.Model
{
    using System.Text.Json;

    /// <summary>
    /// The stream service of OBS Studio (<em>Settings</em>, <em>Stream</em>) of the profile that is current: where the stream goes. It is a snapshot: it does not change when OBS Studio does
    /// (<see cref="ObsModel.GetStreamServiceAsync"/> asks again).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Settings"/> holds what OBS Studio keeps for the service, and that is **the stream key** (the field is called <c>key</c> for <c>rtmp_custom</c>, which was measured with OBS Studio 33.0.0): do not log it, and do not show it. <see cref="ToString"/>
    /// tells the type only.
    /// </para>
    /// </remarks>
    public sealed class ObsStreamService
    {
        internal ObsStreamService(string type, JsonElement settings)
        {
            this.Type = type;
            this.Settings = settings;
        }

        /// <summary>
        /// Gets the type of the service: <c>rtmp_common</c> for one of the services that OBS Studio knows (Twitch, YouTube, ...), <c>rtmp_custom</c> for a server and a key that the user types, or the type of a plugin.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the settings of the service, as a JSON object: for <c>rtmp_custom</c> the fields <c>server</c> and <c>key</c> (measured with OBS Studio 33.0.0). **It holds the stream key.** What the fields are called depends on the type.
        /// </summary>
        public JsonElement Settings { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Stream service '{this.Type}'";
        }
    }
}
