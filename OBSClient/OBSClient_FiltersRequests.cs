namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Messages;

    public partial class OBSClient
    {
        /// <summary>
        /// Gets an array of all of a source's filters.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <returns>Array of <see cref="Filter"/></returns>
        public Filter[] GetSourceFilterList(string sourceName)
        {
            return this.SendRequest<FiltersResponseData>(new { sourceName }).Filters;
        }

        /// <summary>
        /// Gets the default settings for a filter kind.
        /// </summary>
        /// <param name="filterKind">Filter kind to get the default settings for</param>
        /// <returns>Object of default settings for the filter kind</returns>
        public Dictionary<string, object> GetSourceFilterDefaultSettings(string filterKind)
        {
            return this.SendRequest<DefaultFilterSettingsResponseData>(new { filterKind }).DefaultFilterSettings;
        }

        /// <summary>
        /// Creates a new filter, adding it to the specified source.
        /// </summary>
        /// <param name="sourceName">Name of the source to add the filter to</param>
        /// <param name="filterName">Name of the new filter to be created</param>
        /// <param name="filterKind">The kind of filter to be created</param>
        /// <param name="filterSettings">Settings object to initialize the filter with</param>
        public void CreateSourceFilter(string sourceName, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            this.SendRequest(new { sourceName, filterName, filterKind, filterSettings });
        }

        /// <summary>
        /// Removes a filter from a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public void RemoveSourceFilter(string sourceName, string filterName)
        {
            this.SendRequest(new { sourceName, filterName });
        }

        /// <summary>
        /// Sets the name of a source filter (rename).
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public void SetSourceFilterName(string sourceName, string filterName, string newFilterName)
        {
            this.SendRequest(new { sourceName, filterName, newFilterName });
        }

        /// <summary>
        /// Gets the info for a specific source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponseData"/></returns>
        public SourceFilterResponseData GetSourceFilter(string sourceName, string filterName)
        {
            return this.SendRequest<SourceFilterResponseData>(new { sourceName, filterName });
        }

        /// <summary>
        /// Sets the index position of a filter on a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public void SetSourceFilterIndex(string sourceName, string filterName, int filterIndex)
        {
            this.SendRequest(new { sourceName, filterName, filterIndex });
        }

        /// <summary>
        /// Sets the settings of a source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to set the settings of</param>
        /// <param name="filterSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public void SetSourceFilterSettings(string sourceName, string filterName, FilterSettings filterSettings, bool overlay = true)
        {
            this.SendRequest(new { sourceName, filterName, filterSettings, overlay });
        }

        /// <summary>
        /// Sets the enable state of a source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public void SetSourceFilterEnabled(string sourceName, string filterName, bool filterEnabled)
        {
            this.SendRequest(new { sourceName, filterName, filterEnabled });
        }
    }
}
