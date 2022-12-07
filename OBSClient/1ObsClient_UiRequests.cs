namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets whether studio is enabled.
        /// </summary>
        /// <returns>Whether studio mode is enabled</returns>
        public bool GetStudioModeEnabled()
        {
            return this.SendRequest<StudioModeEnabledData>().StudioModeEnabled;
        }

        /// <summary>
        /// Enables or disables studio mode
        /// </summary>
        /// <param name="studioModeEnabled">True == Enabled, False == Disabled</param>
        public void SetStudioModeEnabled(bool studioModeEnabled)
        {
            this.SendRequest(new { studioModeEnabled });
        }

        /// <summary>
        /// Opens the properties dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public void OpenInputPropertiesDialog(string inputName)
        {
            this.SendRequest(new { inputName });
        }

        /// <summary>
        /// Opens the filters dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public void OpenInputFiltersDialog(string inputName)
        {
            this.SendRequest(new { inputName });
        }

        /// <summary>
        /// Opens the interact dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public void OpenInputInteractDialog(string inputName)
        {
            this.SendRequest(new { inputName });
        }

        /// <summary>
        /// Gets a list of connected monitors and information about them.
        /// </summary>
        /// <returns>a list of detected monitors with some information</returns>
        public Monitor[] GetMonitorList()
        {
            return this.SendRequest<MonitorListResponseData>().Monitors;
        }

        /// <summary>
        /// Opens a projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index. Use -1 for windowed mode</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public void OpenVideoMixProjectorOnMonitor(MixType videoMixType, int monitorIndex)
        {
            this.OpenVideoMixProjector(videoMixType, monitorIndex, null);
        }

        /// <summary>
        /// Opens a projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format.  See <see cref="GetGeometry(int, int, int, int, int, bool, bool, int)"/>.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public void OpenVideoMixProjectorWindow(MixType videoMixType, string projectorGeometry)
        {
            this.OpenVideoMixProjector(videoMixType, null, projectorGeometry);
        }

        /// <summary>
        /// Opens a projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. Mutually exclusive with monitorIndex</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        private void OpenVideoMixProjector(MixType videoMixType, int? monitorIndex, string? projectorGeometry)
        {
            this.SendRequest(new { videoMixType, monitorIndex, projectorGeometry });
        }

        /// <summary>
        /// Opens a projector for a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to open a projector for</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index. Use -1 for windowed mode.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public void OpenSourceProjectorOnMonitor(string sourceName, int monitorIndex)
        {
            this.OpenSourceProjector(sourceName, monitorIndex, null);
        }

        /// <summary>
        /// Opens a projector for a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to open a projector for</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. See <see cref="GetGeometry(int, int, int, int, int, bool, bool, int)"/>.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public void OpenSourceProjectorWindow(string sourceName, string projectorGeometry)
        {
            this.OpenSourceProjector(sourceName, null, projectorGeometry);
        }

        /// <summary>
        /// Opens a projector for a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to open a projector for</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. Mutually exclusive with monitorIndex</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        private void OpenSourceProjector(string sourceName, int? monitorIndex = -1, string? projectorGeometry = null)
        {
            this.SendRequest(new { sourceName, monitorIndex, projectorGeometry });
        }
    }
}
