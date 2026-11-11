namespace OBSStudioClient.Model
{
    /// <summary>
    /// Which of the two fields of an interlaced frame comes first (see <see cref="ObsDeinterlaceMode"/>).
    /// </summary>
    public enum ObsDeinterlaceFieldOrder
    {
        /// <summary>
        /// The top field comes first (<em>Top Field First</em> in OBS Studio).
        /// </summary>
        TopFieldFirst,

        /// <summary>
        /// The bottom field comes first (<em>Bottom Field First</em>).
        /// </summary>
        BottomFieldFirst,
    }
}
