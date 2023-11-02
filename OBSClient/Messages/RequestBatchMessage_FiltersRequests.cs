namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Responses;
    using System.Collections.Generic;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get an array of all of a source's filters.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <returns>Array of <see cref="Filter"/></returns>
        public void AddGetSourceFilterListRequest(string sourceName)
        {
            this._requests.Add(new(new { sourceName }));
        }

        /// <summary>
        /// Adds a request to get the default settings for a filter kind.
        /// </summary>
        /// <param name="filterKind">Filter kind to get the default settings for</param>
        /// <returns>Object of default settings for the filter kind</returns>
        public void AddGetSourceFilterDefaultSettingsRequest(string filterKind)
        {
            this._requests.Add(new(new { filterKind }));
        }

        /// <summary>
        /// Adds a request to creates a new filter, adding it to the specified source.
        /// </summary>
        /// <param name="sourceName">Name of the source to add the filter to</param>
        /// <param name="filterName">Name of the new filter to be created</param>
        /// <param name="filterKind">The kind of filter to be created</param>
        /// <param name="filterSettings">Settings object to initialize the filter with</param>
        public void AddCreateSourceFilterRequest(string sourceName, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            this._requests.Add(new(new { sourceName, filterName, filterKind, filterSettings }));
        }

        /// <summary>
        /// Adds a request to removes a filter from a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public void AddRemoveSourceFilterRequest(string sourceName, string filterName)
        {
            this._requests.Add(new(new { sourceName, filterName }));
        }

        /// <summary>
        /// Adds a request to set the name of a source filter (rename).
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public void AddSetSourceFilterNameRequest(string sourceName, string filterName, string newFilterName)
        {
            this._requests.Add(new(new { sourceName, filterName, newFilterName }));
        }

        /// <summary>
        /// Adds a request to get the info for a specific source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponse"/></returns>
        public void AddGetSourceFilterRequest(string sourceName, string filterName)
        {
            this._requests.Add(new(new { sourceName, filterName }));
        }

        /// <summary>
        /// Adds a request to set the index position of a filter on a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public void AddSetSourceFilterIndexRequest(string sourceName, string filterName, int filterIndex)
        {
            this._requests.Add(new(new { sourceName, filterName, filterIndex }));
        }

        /// <summary>
        /// Adds a request to set the settings of a source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to set the settings of</param>
        /// <param name="filterSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public void AddSetSourceFilterSettingsRequest(string sourceName, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            this._requests.Add(new(new { sourceName, filterName, filterSettings, overlay }));
        }

        /// <summary>
        /// Adds a request to set the enable state of a source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public void AddSetSourceFilterEnabledRequest(string sourceName, string filterName, bool filterEnabled)
        {
            this._requests.Add(new(new { sourceName, filterName, filterEnabled }));
        }
    }
}