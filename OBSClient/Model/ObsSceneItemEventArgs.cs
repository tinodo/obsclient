namespace OBSStudioClient.Model
{
    /// <summary>
    /// Describes something that happened to a scene item of an <see cref="ObsModel"/>.
    /// </summary>
    public sealed class ObsSceneItemEventArgs : EventArgs
    {
        internal ObsSceneItemEventArgs(ObsSceneItem item)
        {
            this.Item = item;
        }

        /// <summary>
        /// Gets the scene item that it happened to.
        /// </summary>
        public ObsSceneItem Item { get; }
    }
}
