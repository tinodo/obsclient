namespace OBSStudioClient.Enums
{
    /// <summary>
    /// Batch Execution Types.
    /// </summary>
    public enum RequestBatchExecutionType
    {
        /// <summary>
        /// None.
        /// </summary>
        None = -1,

        /// <summary>
        /// Serial, in real time.
        /// </summary>
        SerialRealtime = 0,

        /// <summary>
        /// Serial, per frame.
        /// </summary>
        SerialFrame = 1,

        /// <summary>
        /// Parallel.
        /// </summary>
        Parallel = 2,
    }
}
