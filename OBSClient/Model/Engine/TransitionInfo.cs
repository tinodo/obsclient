namespace OBSStudioClient.Model.Engine
{
    /// <summary>
    /// A scene transition of OBS Studio, as the model holds it: the current one.
    /// </summary>
    /// <param name="Name">The name of the transition.</param>
    /// <param name="Uuid">The UUID of the transition.</param>
    /// <param name="Kind">The kind of the transition (<c>fade_transition</c>).</param>
    /// <param name="IsFixed">Whether the transition has a duration of its own that can not be set (a cut).</param>
    /// <param name="IsConfigurable">Whether the transition has settings.</param>
    internal sealed record TransitionInfo(string Name, Guid Uuid, string Kind, bool IsFixed, bool IsConfigurable);
}
