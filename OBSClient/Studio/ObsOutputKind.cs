namespace OBSStudioClient.Studio
{
    /// <summary>
    /// The outputs of OBS Studio that the model keeps: what it sends somewhere, or writes to disk.
    /// </summary>
    public enum ObsOutputKind
    {
        /// <summary>
        /// The stream to the streaming service.
        /// </summary>
        Stream,

        /// <summary>
        /// The recording to a file.
        /// </summary>
        Record,

        /// <summary>
        /// The virtual camera.
        /// </summary>
        VirtualCamera,

        /// <summary>
        /// The replay buffer.
        /// </summary>
        ReplayBuffer,
    }
}
