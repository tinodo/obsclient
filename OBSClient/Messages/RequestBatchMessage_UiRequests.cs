namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get whether studio is enabled.
        /// </summary>
        /// <returns>Whether studio mode is enabled</returns>
        public void AddGetStudioModeEnabledRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to enable or disable studio mode.
        /// </summary>
        /// <param name="studioModeEnabled">True == Enabled, False == Disabled</param>
        public void AddSetStudioModeEnabledRequest(bool studioModeEnabled)
        {
            this.Requests.Add(new(new { studioModeEnabled }));
        }

        /// <summary>
        /// Adds a request to open the properties dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public void AddOpenInputPropertiesDialogRequest(string inputName)
        {
            this.Requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to open the filters dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public void AddOpenInputFiltersDialogRequest(string inputName)
        {
            this.Requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to open the interact dialog of an input.
        /// </summary>
        /// <param name="inputName">Name of the input to open the dialog of</param>
        public void AddOpenInputInteractDialogRequest(string inputName)
        {
            this.Requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to get a list of connected monitors and information about them.
        /// </summary>
        /// <returns>a list of detected monitors with some information</returns>
        public void AddGetMonitorListRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to open a projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. Mutually exclusive with monitorIndex</param>
        /// <remarks>
        /// Use either monitorIndex or projectGeometry. One of both should be null, not both.
        /// Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.
        /// </remarks>
        public void AddOpenVideoMixProjectorRequest(MixType videoMixType, int? monitorIndex, string? projectorGeometry)
        {
            this.Requests.Add(new(new { videoMixType, monitorIndex, projectorGeometry }));
        }

        /// <summary>
        /// Adds a request to open a projector for a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to open a projector for</param>
        /// <param name="monitorIndex">Monitor index, use GetMonitorList to obtain index</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. Mutually exclusive with monitorIndex</param>
        /// <remarks>
        /// Use either monitorIndex or projectGeometry. One of both should be null, not both.
        /// Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.
        /// </remarks>
        public void AddOpenSourceProjectorRequest(string sourceName, int? monitorIndex = -1, string? projectorGeometry = null)
        {
            this.Requests.Add(new(new { sourceName, monitorIndex, projectorGeometry }));
        }
    }
}
