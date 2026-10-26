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
            this.AddGetSourceFilterListRequest(sourceName, null);
        }

        /// <summary>
        /// Adds a request to get an array of all of a source's filters on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <returns>Array of <see cref="Filter"/></returns>
        public void AddGetSourceFilterListRequest(string sourceName, Guid canvasUuid)
        {
            this.AddGetSourceFilterListRequest(sourceName, (Guid?)canvasUuid);
        }

        /// <summary>
        /// Adds a request to get an array of all of a source's filters.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source</param>
        /// <returns>Array of <see cref="Filter"/></returns>
        public void AddGetSourceFilterListRequest(Guid sourceUuid)
        {
            this._requests.Add(new(new { sourceUuid }));
        }

        private void AddGetSourceFilterListRequest(string sourceName, Guid? canvasUuid)
        {
            this._requests.Add(new(new { sourceName, canvasUuid }));
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
        /// Adds a request to get an array of all available source filter kinds.
        /// </summary>
        /// <returns>Array of source filter kinds</returns>
        public void AddGetSourceFilterKindListRequest()
        {
            this._requests.Add(new());
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
            this.AddCreateSourceFilterRequest(sourceName, null, filterName, filterKind, filterSettings);
        }

        /// <summary>
        /// Adds a request to creates a new filter, adding it to the specified source on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source to add the filter to</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the new filter to be created</param>
        /// <param name="filterKind">The kind of filter to be created</param>
        /// <param name="filterSettings">Settings object to initialize the filter with</param>
        public void AddCreateSourceFilterRequest(string sourceName, Guid canvasUuid, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            this.AddCreateSourceFilterRequest(sourceName, (Guid?)canvasUuid, filterName, filterKind, filterSettings);
        }

        /// <summary>
        /// Adds a request to creates a new filter, adding it to the specified source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source to add the filter to</param>
        /// <param name="filterName">Name of the new filter to be created</param>
        /// <param name="filterKind">The kind of filter to be created</param>
        /// <param name="filterSettings">Settings object to initialize the filter with</param>
        public void AddCreateSourceFilterRequest(Guid sourceUuid, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            this._requests.Add(new(new { sourceUuid, filterName, filterKind, filterSettings }));
        }

        private void AddCreateSourceFilterRequest(string sourceName, Guid? canvasUuid, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            this._requests.Add(new(new { sourceName, canvasUuid, filterName, filterKind, filterSettings }));
        }

        /// <summary>
        /// Adds a request to removes a filter from a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public void AddRemoveSourceFilterRequest(string sourceName, string filterName)
        {
            this.AddRemoveSourceFilterRequest(sourceName, null, filterName);
        }

        /// <summary>
        /// Adds a request to removes a filter from a source on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public void AddRemoveSourceFilterRequest(string sourceName, Guid canvasUuid, string filterName)
        {
            this.AddRemoveSourceFilterRequest(sourceName, (Guid?)canvasUuid, filterName);
        }

        /// <summary>
        /// Adds a request to removes a filter from a source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public void AddRemoveSourceFilterRequest(Guid sourceUuid, string filterName)
        {
            this._requests.Add(new(new { sourceUuid, filterName }));
        }

        private void AddRemoveSourceFilterRequest(string sourceName, Guid? canvasUuid, string filterName)
        {
            this._requests.Add(new(new { sourceName, canvasUuid, filterName }));
        }

        /// <summary>
        /// Adds a request to set the name of a source filter (rename).
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public void AddSetSourceFilterNameRequest(string sourceName, string filterName, string newFilterName)
        {
            this.AddSetSourceFilterNameRequest(sourceName, null, filterName, newFilterName);
        }

        /// <summary>
        /// Adds a request to set the name of a source filter on a canvas (rename).
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public void AddSetSourceFilterNameRequest(string sourceName, Guid canvasUuid, string filterName, string newFilterName)
        {
            this.AddSetSourceFilterNameRequest(sourceName, (Guid?)canvasUuid, filterName, newFilterName);
        }

        /// <summary>
        /// Adds a request to set the name of a source filter (rename).
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public void AddSetSourceFilterNameRequest(Guid sourceUuid, string filterName, string newFilterName)
        {
            this._requests.Add(new(new { sourceUuid, filterName, newFilterName }));
        }

        private void AddSetSourceFilterNameRequest(string sourceName, Guid? canvasUuid, string filterName, string newFilterName)
        {
            this._requests.Add(new(new { sourceName, canvasUuid, filterName, newFilterName }));
        }

        /// <summary>
        /// Adds a request to get the info for a specific source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponse"/></returns>
        public void AddGetSourceFilterRequest(string sourceName, string filterName)
        {
            this.AddGetSourceFilterRequest(sourceName, null, filterName);
        }

        /// <summary>
        /// Adds a request to get the info for a specific source filter on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponse"/></returns>
        public void AddGetSourceFilterRequest(string sourceName, Guid canvasUuid, string filterName)
        {
            this.AddGetSourceFilterRequest(sourceName, (Guid?)canvasUuid, filterName);
        }

        /// <summary>
        /// Adds a request to get the info for a specific source filter.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponse"/></returns>
        public void AddGetSourceFilterRequest(Guid sourceUuid, string filterName)
        {
            this._requests.Add(new(new { sourceUuid, filterName }));
        }

        private void AddGetSourceFilterRequest(string sourceName, Guid? canvasUuid, string filterName)
        {
            this._requests.Add(new(new { sourceName, canvasUuid, filterName }));
        }

        /// <summary>
        /// Adds a request to set the index position of a filter on a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public void AddSetSourceFilterIndexRequest(string sourceName, string filterName, int filterIndex)
        {
            this.AddSetSourceFilterIndexRequest(sourceName, null, filterName, filterIndex);
        }

        /// <summary>
        /// Adds a request to set the index position of a filter on a source on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public void AddSetSourceFilterIndexRequest(string sourceName, Guid canvasUuid, string filterName, int filterIndex)
        {
            this.AddSetSourceFilterIndexRequest(sourceName, (Guid?)canvasUuid, filterName, filterIndex);
        }

        /// <summary>
        /// Adds a request to set the index position of a filter on a source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public void AddSetSourceFilterIndexRequest(Guid sourceUuid, string filterName, int filterIndex)
        {
            this._requests.Add(new(new { sourceUuid, filterName, filterIndex }));
        }

        private void AddSetSourceFilterIndexRequest(string sourceName, Guid? canvasUuid, string filterName, int filterIndex)
        {
            this._requests.Add(new(new { sourceName, canvasUuid, filterName, filterIndex }));
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
            this.AddSetSourceFilterSettingsRequest(sourceName, null, filterName, filterSettings, overlay);
        }

        /// <summary>
        /// Adds a request to set the settings of a source filter on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter to set the settings of</param>
        /// <param name="filterSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public void AddSetSourceFilterSettingsRequest(string sourceName, Guid canvasUuid, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            this.AddSetSourceFilterSettingsRequest(sourceName, (Guid?)canvasUuid, filterName, filterSettings, overlay);
        }

        /// <summary>
        /// Adds a request to set the settings of a source filter.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to set the settings of</param>
        /// <param name="filterSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public void AddSetSourceFilterSettingsRequest(Guid sourceUuid, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            this._requests.Add(new(new { sourceUuid, filterName, filterSettings, overlay }));
        }

        private void AddSetSourceFilterSettingsRequest(string sourceName, Guid? canvasUuid, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            this._requests.Add(new(new { sourceName, canvasUuid, filterName, filterSettings, overlay }));
        }

        /// <summary>
        /// Adds a request to set the enable state of a source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public void AddSetSourceFilterEnabledRequest(string sourceName, string filterName, bool filterEnabled)
        {
            this.AddSetSourceFilterEnabledRequest(sourceName, null, filterName, filterEnabled);
        }

        /// <summary>
        /// Adds a request to set the enable state of a source filter on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public void AddSetSourceFilterEnabledRequest(string sourceName, Guid canvasUuid, string filterName, bool filterEnabled)
        {
            this.AddSetSourceFilterEnabledRequest(sourceName, (Guid?)canvasUuid, filterName, filterEnabled);
        }

        /// <summary>
        /// Adds a request to set the enable state of a source filter.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public void AddSetSourceFilterEnabledRequest(Guid sourceUuid, string filterName, bool filterEnabled)
        {
            this._requests.Add(new(new { sourceUuid, filterName, filterEnabled }));
        }

        private void AddSetSourceFilterEnabledRequest(string sourceName, Guid? canvasUuid, string filterName, bool filterEnabled)
        {
            this._requests.Add(new(new { sourceName, canvasUuid, filterName, filterEnabled }));
        }
    }
}