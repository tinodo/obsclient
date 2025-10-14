namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets whether studio is enabled.
        /// </summary>
        /// <returns>Whether studio mode is enabled</returns>
        public async Task<bool> GetStudioModeEnabled()
        {
            return (await this.SendRequestAsync<StudioModeEnabledResponse>()).StudioModeEnabled;
        }

        /// <summary>
        /// Enables or disables studio mode.
        /// </summary>
        /// <param name="studioModeEnabled">True == Enabled, False == Disabled</param>
        public async Task SetStudioModeEnabled(bool studioModeEnabled)
        {
            await this.SendRequestAsync(new { studioModeEnabled });
        }

        /// <summary>
        /// Opens the properties dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public async Task OpenInputPropertiesDialog(string inputName)
        {
            await this.OpenInputPropertiesDialog(inputName, null);
        }

        /// <summary>
        /// Opens the properties dialog of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to open the dialog of</param>
        public async Task OpenInputPropertiesDialog(Guid inputUuid)
        {
            await this.OpenInputPropertiesDialog(null, inputUuid);
        }

        private async Task OpenInputPropertiesDialog(string? inputName, Guid? inputUuid)
        {
            await this.SendRequestAsync(new { inputName, inputUuid });
        }

        /// <summary>
        /// Opens the filters dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public async Task OpenInputFiltersDialog(string inputName)
        {
            await this.OpenInputFiltersDialog(inputName, null);
        }

        /// <summary>
        /// Opens the filters dialog of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to open the dialog of</param>
        public async Task OpenInputFiltersDialog(Guid inputUuid)
        {
            await this.OpenInputFiltersDialog(null, inputUuid);
        }

        private async Task OpenInputFiltersDialog(string? inputName, Guid? inputUuid)
        {
            await this.SendRequestAsync(new { inputName, inputUuid });
        }

        /// <summary>
        /// Opens the interact dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public async Task OpenInputInteractDialog(string inputName)
        {
            await this.OpenInputInteractDialog(inputName, null);
        }

        /// <summary>
        /// Opens the interact dialog of an input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the input to open the dialog of</param>
        public async Task OpenInputInteractDialog(Guid inputUuid)
        {
            await this.OpenInputInteractDialog(null, inputUuid);
        }

        private async Task OpenInputInteractDialog(string? inputName, Guid? inputUuid)
        {
            await this.SendRequestAsync(new { inputName, inputUuid });
        }

        /// <summary>
        /// Gets a list of connected monitors and information about them.
        /// </summary>
        /// <returns>a list of detected monitors with some information</returns>
        public async Task<Monitor[]> GetMonitorList()
        {
            return (await this.SendRequestAsync<MonitorListResponse>()).Monitors;
        }

        /// <summary>
        /// Opens a projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index. Use -1 for windowed mode</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenVideoMixProjectorOnMonitor(MixType videoMixType, int monitorIndex)
        {
            await this.OpenVideoMixProjector(videoMixType, monitorIndex, null);
        }

        /// <summary>
        /// Opens a projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format.  See <see cref="GetGeometry(int, int, int, int, int, bool, bool, int)"/>.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenVideoMixProjectorWindow(MixType videoMixType, string projectorGeometry)
        {
            await this.OpenVideoMixProjector(videoMixType, null, projectorGeometry);
        }

        /// <summary>
        /// Opens a projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. Mutually exclusive with monitorIndex</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        private async Task OpenVideoMixProjector(MixType videoMixType, int? monitorIndex, string? projectorGeometry)
        {
            await this.SendRequestAsync(new { videoMixType, monitorIndex, projectorGeometry });
        }

        /// <summary>
        /// Opens a projector for a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to open a projector for</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index. Use -1 for windowed mode.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenSourceProjectorOnMonitor(string sourceName, int monitorIndex)
        {
            await this.OpenSourceProjector(sourceName, null, monitorIndex, null);
        }

        /// <summary>
        /// Opens a projector for a source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source to open a projector for</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index. Use -1 for windowed mode.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenSourceProjectorOnMonitor(Guid sourceUuid, int monitorIndex)
        {
            await this.OpenSourceProjector(null, sourceUuid, monitorIndex, null);
        }


        /// <summary>
        /// Opens a projector for a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to open a projector for</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. See <see cref="GetGeometry(int, int, int, int, int, bool, bool, int)"/>.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenSourceProjectorWindow(string sourceName, string projectorGeometry)
        {
            await this.OpenSourceProjector(sourceName, null, null, projectorGeometry);
        }

        /// <summary>
        /// Opens a projector for a source.
        /// </summary>
        /// <param name="sourceUuid">Uuid of the source to open a projector for</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. See <see cref="GetGeometry(int, int, int, int, int, bool, bool, int)"/>.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenSourceProjectorWindow(Guid sourceUuid, string projectorGeometry)
        {
            await this.OpenSourceProjector(null, sourceUuid, null, projectorGeometry);
        }


        private async Task OpenSourceProjector(string? sourceName, Guid? sourceUuid, int? monitorIndex = -1, string? projectorGeometry = null)
        {
            await this.SendRequestAsync(new { sourceName, sourceUuid, monitorIndex, projectorGeometry });
        }
    }
}
