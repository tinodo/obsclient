namespace OBSStudioClient.Model
{
    using System.Text.Json;

    /// <summary>
    /// Describes a change of the settings of a filter of an <see cref="ObsModel"/>, as OBS Studio said it (see <see cref="ObsModel.FilterSettingsChanged"/>).
    /// </summary>
    public sealed class ObsFilterSettingsEventArgs : EventArgs
    {
        internal ObsFilterSettingsEventArgs(ObsFilter filter, JsonElement settings)
        {
            this.Filter = filter;
            this.Settings = settings;
        }

        /// <summary>
        /// Gets the filter whose settings changed.
        /// </summary>
        public ObsFilter Filter { get; }

        /// <summary>
        /// Gets the settings that the filter has now, as OBS Studio said them: a JSON object with the values that were set, like <see cref="ObsFilter.GetSettingsAsync"/> returns them.
        /// </summary>
        public JsonElement Settings { get; }
    }
}
