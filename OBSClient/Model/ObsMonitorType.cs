namespace OBSStudioClient.Model
{
    /// <summary>
    /// What OBS Studio does with the audio of an input besides sending it to the program: whether it plays it on the monitoring device (the headphones of the person at the computer).
    /// </summary>
    /// <remarks>
    /// OBS Studio 33 (libobs) no longer has three monitor types but a switch that is on or off, and it turns what an application asks for into one of the two: <see cref="MonitorOnly"/> becomes
    /// <see cref="MonitorAndOutput"/> (a test with OBS Studio 33.0.0 shows it: the input held <see cref="MonitorAndOutput"/> after it was told <see cref="MonitorOnly"/>). Older versions of OBS Studio keep the three. The
    /// <see cref="ObsInput.MonitorType"/> of the model is what OBS Studio holds, so it can differ from what was asked for.
    /// </remarks>
    public enum ObsMonitorType
    {
        /// <summary>
        /// The input is not monitored (<em>Monitor Off</em> in OBS Studio).
        /// </summary>
        Off,

        /// <summary>
        /// The input is played on the monitoring device and not sent to the program (<em>Monitor Only (mute output)</em>).
        /// </summary>
        MonitorOnly,

        /// <summary>
        /// The input is played on the monitoring device and sent to the program (<em>Monitor and Output</em>).
        /// </summary>
        MonitorAndOutput,
    }
}
