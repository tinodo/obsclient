namespace OBSStudioClient.Enums
{
    /// <summary>
    /// Op Codes.
    /// </summary>
    public enum OpCode
    {
        /// <summary>
        /// Hello.
        /// </summary>
        Hello = 0,

        /// <summary>
        /// Identify.
        /// </summary>
        Identify = 1,

        /// <summary>
        /// Identified.
        /// </summary>
        Identified = 2,

        /// <summary>
        /// Reidentify.
        /// </summary>
        Reidentify = 3,

        /// <summary>
        /// Event.
        /// </summary>
        Event = 5,

        /// <summary>
        /// Request.
        /// </summary>
        Request = 6,

        /// <summary>
        /// RequestResponse.
        /// </summary>
        RequestResponse = 7,

        /// <summary>
        /// RequestBatch.
        /// </summary>
        RequestBatch = 8,

        /// <summary>
        /// RequestBatchResponse.
        /// </summary>
        RequestBatchResponse = 9,
    }
}
