namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets an array of all available source filter kinds.
        /// </summary>
        /// <returns>Array of source filter kinds</returns>
        public async Task<string[]> GetSourceFilterKindList()
        {
            return (await this.SendRequestAsync<FilterKindListResponse>()).SourceFilterKinds;
        }

        /// <summary>
        /// Gets an array of all of a source's filters.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <returns>Array of <see cref="Filter"/></returns>
        public async Task<Filter[]> GetSourceFilterList(string sourceName)
        {
            return await this.GetSourceFilterList(sourceName, null, null);
        }

        /// <summary>
        /// Gets an array of all of a source's filters on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <returns>Array of <see cref="Filter"/></returns>
        public async Task<Filter[]> GetSourceFilterList(string sourceName, Guid canvasUuid)
        {
            return await this.GetSourceFilterList(sourceName, canvasUuid, null);
        }

        /// <summary>
        /// Gets an array of all of a source's filters.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source</param>
        /// <returns>Array of <see cref="Filter"/></returns>
        public async Task<Filter[]> GetSourceFilterList(Guid sourceUuid)
        {
            return await this.GetSourceFilterList(null, null, sourceUuid);
        }

        private async Task<Filter[]> GetSourceFilterList(string? sourceName, Guid? canvasUuid, Guid? sourceUuid)
        {
            return (await this.SendRequestAsync<FiltersResponse>(new { sourceName, canvasUuid, sourceUuid })).Filters;
        }

        /// <summary>
        /// Gets the default settings for a filter kind.
        /// </summary>
        /// <param name="filterKind">Filter kind to get the default settings for</param>
        /// <returns>Object of default settings for the filter kind</returns>
        public async Task<Dictionary<string, object>> GetSourceFilterDefaultSettings(string filterKind)
        {
            return (await this.SendRequestAsync<DefaultFilterSettingsResponse>(new { filterKind })).DefaultFilterSettings;
        }

        /// <summary>
        /// Creates a new filter, adding it to the specified source.
        /// </summary>
        /// <param name="sourceName">Name of the source to add the filter to</param>
        /// <param name="filterName">Name of the new filter to be created</param>
        /// <param name="filterKind">The kind of filter to be created</param>
        /// <param name="filterSettings">Settings object to initialize the filter with</param>
        public async Task CreateSourceFilter(string sourceName, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            await this.CreateSourceFilter(sourceName, null, null, filterName, filterKind, filterSettings);
        }

        /// <summary>
        /// Creates a new filter, adding it to the specified source on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source to add the filter to</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the new filter to be created</param>
        /// <param name="filterKind">The kind of filter to be created</param>
        /// <param name="filterSettings">Settings object to initialize the filter with</param>
        public async Task CreateSourceFilter(string sourceName, Guid canvasUuid, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            await this.CreateSourceFilter(sourceName, canvasUuid, null, filterName, filterKind, filterSettings);
        }

        /// <summary>
        /// Creates a new filter, adding it to the specified source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source to add the filter to</param>
        /// <param name="filterName">Name of the new filter to be created</param>
        /// <param name="filterKind">The kind of filter to be created</param>
        /// <param name="filterSettings">Settings object to initialize the filter with</param>
        public async Task CreateSourceFilter(Guid sourceUuid, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            await this.CreateSourceFilter(null, null, sourceUuid, filterName, filterKind, filterSettings);
        }

        private async Task CreateSourceFilter(string? sourceName, Guid? canvasUuid, Guid? sourceUuid, string filterName, string filterKind, Dictionary<string, object>? filterSettings)
        {
            await this.SendRequestAsync(new { sourceName, canvasUuid, sourceUuid, filterName, filterKind, filterSettings });
        }

        /// <summary>
        /// Removes a filter from a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public async Task RemoveSourceFilter(string sourceName, string filterName)
        {
            await this.RemoveSourceFilter(sourceName, null, null, filterName);
        }

        /// <summary>
        /// Removes a filter from a source on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public async Task RemoveSourceFilter(string sourceName, Guid canvasUuid, string filterName)
        {
            await this.RemoveSourceFilter(sourceName, canvasUuid, null, filterName);
        }

        /// <summary>
        /// Removes a filter from a source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to remove</param>
        public async Task RemoveSourceFilter(Guid sourceUuid, string filterName)
        {
            await this.RemoveSourceFilter(null, null, sourceUuid, filterName);
        }

        private async Task RemoveSourceFilter(string? sourceName, Guid? canvasUuid, Guid? sourceUuid, string filterName)
        {
            await this.SendRequestAsync(new { sourceName, canvasUuid, sourceUuid, filterName });
        }

        /// <summary>
        /// Sets the name of a source filter (rename).
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public async Task SetSourceFilterName(string sourceName, string filterName, string newFilterName)
        {
            await this.SetSourceFilterName(sourceName, null, null, filterName, newFilterName);
        }

        /// <summary>
        /// Sets the name of a source filter on a canvas (rename).
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public async Task SetSourceFilterName(string sourceName, Guid canvasUuid, string filterName, string newFilterName)
        {
            await this.SetSourceFilterName(sourceName, canvasUuid, null, filterName, newFilterName);
        }

        /// <summary>
        /// Sets the name of a source filter (rename).
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Current name of the filter</param>
        /// <param name="newFilterName">New name for the filter</param>
        public async Task SetSourceFilterName(Guid sourceUuid, string filterName, string newFilterName)
        {
            await this.SetSourceFilterName(null, null, sourceUuid, filterName, newFilterName);
        }

        private async Task SetSourceFilterName(string? sourceName, Guid? canvasUuid, Guid? sourceUuid, string filterName, string newFilterName)
        {
            await this.SendRequestAsync(new { sourceName, canvasUuid, sourceUuid, filterName, newFilterName });
        }

        /// <summary>
        /// Gets the info for a specific source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponse"/></returns>
        public async Task<SourceFilterResponse> GetSourceFilter(string sourceName, string filterName)
        {
            return await this.GetSourceFilter(sourceName, null, null, filterName);
        }

        /// <summary>
        /// Gets the info for a specific source filter on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponse"/></returns>
        public async Task<SourceFilterResponse> GetSourceFilter(string sourceName, Guid canvasUuid, string filterName)
        {
            return await this.GetSourceFilter(sourceName, canvasUuid, null, filterName);
        }

        /// <summary>
        /// Gets the info for a specific source filter.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source</param>
        /// <param name="filterName">Name of the filter</param>
        /// <returns>A <see cref="SourceFilterResponse"/></returns>
        public async Task<SourceFilterResponse> GetSourceFilter(Guid sourceUuid, string filterName)
        {
            return await this.GetSourceFilter(null, null, sourceUuid, filterName);
        }

        private async Task<SourceFilterResponse> GetSourceFilter(string? sourceName, Guid? canvasUuid, Guid? sourceUuid, string filterName)
        {
            return await this.SendRequestAsync<SourceFilterResponse>(new { sourceName, canvasUuid, sourceUuid, filterName });
        }

        /// <summary>
        /// Sets the index position of a filter on a source.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public async Task SetSourceFilterIndex(string sourceName, string filterName, int filterIndex)
        {
            await this.SetSourceFilterIndex(sourceName, null, null, filterName, filterIndex);
        }

        /// <summary>
        /// Sets the index position of a filter on a source on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public async Task SetSourceFilterIndex(string sourceName, Guid canvasUuid, string filterName, int filterIndex)
        {
            await this.SetSourceFilterIndex(sourceName, canvasUuid, null, filterName, filterIndex);
        }

        /// <summary>
        /// Sets the index position of a filter on a source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterIndex">New index position of the filter (>= 0)</param>
        public async Task SetSourceFilterIndex(Guid sourceUuid, string filterName, int filterIndex)
        {
            await this.SetSourceFilterIndex(null, null, sourceUuid, filterName, filterIndex);
        }

        private async Task SetSourceFilterIndex(string? sourceName, Guid? canvasUuid, Guid? sourceUuid, string filterName, int filterIndex)
        {
            await this.SendRequestAsync(new { sourceName, canvasUuid, sourceUuid, filterName, filterIndex });
        }

        /// <summary>
        /// Sets the settings of a source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to set the settings of</param>
        /// <param name="filterSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public async Task SetSourceFilterSettings(string sourceName, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            await this.SetSourceFilterSettings(sourceName, null, null, filterName, filterSettings, overlay);
        }

        /// <summary>
        /// Sets the settings of a source filter on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter to set the settings of</param>
        /// <param name="filterSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public async Task SetSourceFilterSettings(string sourceName, Guid canvasUuid, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            await this.SetSourceFilterSettings(sourceName, canvasUuid, null, filterName, filterSettings, overlay);
        }

        /// <summary>
        /// Sets the settings of a source filter.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter to set the settings of</param>
        /// <param name="filterSettings">Object of settings to apply</param>
        /// <param name="overlay">True == apply the settings on top of existing ones, False == reset the input to its defaults, then apply settings.</param>
        public async Task SetSourceFilterSettings(Guid sourceUuid, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            await this.SetSourceFilterSettings(null, null, sourceUuid, filterName, filterSettings, overlay);
        }

        private async Task SetSourceFilterSettings(string? sourceName, Guid? canvasUuid, Guid? sourceUuid, string filterName, Dictionary<string, object> filterSettings, bool overlay = true)
        {
            await this.SendRequestAsync(new { sourceName, canvasUuid, sourceUuid, filterName, filterSettings, overlay });
        }

        /// <summary>
        /// Sets the enable state of a source filter.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public async Task SetSourceFilterEnabled(string sourceName, string filterName, bool filterEnabled)
        {
            await this.SetSourceFilterEnabled(sourceName, null, null, filterName, filterEnabled);
        }

        /// <summary>
        /// Sets the enable state of a source filter on a canvas.
        /// </summary>
        /// <param name="sourceName">Name of the source the filter is on</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public async Task SetSourceFilterEnabled(string sourceName, Guid canvasUuid, string filterName, bool filterEnabled)
        {
            await this.SetSourceFilterEnabled(sourceName, canvasUuid, null, filterName, filterEnabled);
        }

        /// <summary>
        /// Sets the enable state of a source filter.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source the filter is on</param>
        /// <param name="filterName">Name of the filter</param>
        /// <param name="filterEnabled">New enable state of the filter</param>
        public async Task SetSourceFilterEnabled(Guid sourceUuid, string filterName, bool filterEnabled)
        {
            await this.SetSourceFilterEnabled(null, null, sourceUuid, filterName, filterEnabled);
        }

        private async Task SetSourceFilterEnabled(string? sourceName, Guid? canvasUuid, Guid? sourceUuid, string filterName, bool filterEnabled)
        {
            await this.SendRequestAsync(new { sourceName, canvasUuid, sourceUuid, filterName, filterEnabled });
        }
    }
}
