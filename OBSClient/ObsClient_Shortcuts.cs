namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;

    // Shortcuts: methods that are not requests of the protocol, but make a request that has a choice of fields easier to use.
    // They call the generated methods of the request.
    public partial class ObsClient
    {
        /// <summary>
        /// Sets the volume setting of an input, in mul.
        /// </summary>
        /// <param name="input">The input to set the volume of, by name or by UUID.</param>
        /// <param name="inputVolumeMul">Volume setting in mul.</param>
        public async Task SetInputVolumeMul(InputRef input, float inputVolumeMul)
        {
            await this.SetInputVolume(input, inputVolumeMul: inputVolumeMul).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the volume setting of an input, in dB.
        /// </summary>
        /// <param name="input">The input to set the volume of, by name or by UUID.</param>
        /// <param name="inputVolumeDb">Volume setting in dB.</param>
        public async Task SetInputVolumeDb(InputRef input, float inputVolumeDb)
        {
            await this.SetInputVolume(input, inputVolumeDb: inputVolumeDb).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens a projector for a specific output video mix on a monitor.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open.</param>
        /// <param name="monitorIndex">Monitor index, use <see cref="GetMonitorList"/> to obtain index. Use -1 for windowed mode.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenVideoMixProjectorOnMonitor(MixType videoMixType, int monitorIndex)
        {
            await this.OpenVideoMixProjector(videoMixType, monitorIndex).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens a windowed projector for a specific output video mix.
        /// </summary>
        /// <param name="videoMixType">Type of mix to open.</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. See <see cref="GetGeometry(int, int, int, int, int, bool, bool, int)"/>.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenVideoMixProjectorWindow(MixType videoMixType, string projectorGeometry)
        {
            await this.OpenVideoMixProjector(videoMixType, projectorGeometry: projectorGeometry).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens a projector for a source on a monitor.
        /// </summary>
        /// <param name="source">The source to open a projector for, by name or by UUID.</param>
        /// <param name="monitorIndex">Monitor index, use <see cref="GetMonitorList"/> to obtain index. Use -1 for windowed mode.</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in, if using the name of the source.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenSourceProjectorOnMonitor(SourceRef source, int monitorIndex, Guid? canvasUuid = null)
        {
            await this.OpenSourceProjector(source, monitorIndex, canvasUuid: canvasUuid).ConfigureAwait(false);
        }

        /// <summary>
        /// Opens a windowed projector for a source.
        /// </summary>
        /// <param name="source">The source to open a projector for, by name or by UUID.</param>
        /// <param name="projectorGeometry">Size/Position data for a windowed projector, in Qt Base64 encoded format. See <see cref="GetGeometry(int, int, int, int, int, bool, bool, int)"/>.</param>
        /// <param name="canvasUuid">UUID of the canvas the source is in, if using the name of the source.</param>
        /// <remarks>Note: This request serves to provide feature parity with 4.x. It is very likely to be changed/deprecated in a future release.</remarks>
        public async Task OpenSourceProjectorWindow(SourceRef source, string projectorGeometry, Guid? canvasUuid = null)
        {
            await this.OpenSourceProjector(source, projectorGeometry: projectorGeometry, canvasUuid: canvasUuid).ConfigureAwait(false);
        }
    }
}
