namespace OBSStudioClient.Model.Engine
{
    /// <summary>
    /// What the notification of a change of the settings of a filter carries: the filter, by the id that the model gave it (OBS Studio gives a filter none), and the settings that OBS Studio said.
    /// </summary>
    /// <param name="FilterId">The id of the filter (<see cref="FilterNode.Id"/>).</param>
    /// <param name="Settings">The settings of the filter, as OBS Studio said them.</param>
    internal sealed record FilterSettingsNotice(long FilterId, SettingsValue Settings);
}
