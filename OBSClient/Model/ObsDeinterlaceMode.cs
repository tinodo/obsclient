namespace OBSStudioClient.Model
{
    /// <summary>
    /// How OBS Studio removes the interlacing of a video that comes as interlaced fields (some capture cards and cameras). Only inputs that deliver their frames from outside
    /// (the <em>async</em> inputs: capture devices, media) can be deinterlaced.
    /// </summary>
    public enum ObsDeinterlaceMode
    {
        /// <summary>
        /// No deinterlacing.
        /// </summary>
        Disabled,

        /// <summary>
        /// One of the two fields is dropped and the other one is used.
        /// </summary>
        Discard,

        /// <summary>
        /// The fields are shown one after the other, as a television did.
        /// </summary>
        Retro,

        /// <summary>
        /// The two fields are blended.
        /// </summary>
        Blend,

        /// <summary>
        /// The two fields are blended, and the result is made for each field (twice the frame rate).
        /// </summary>
        Blend2x,

        /// <summary>
        /// The missing lines are made by interpolation.
        /// </summary>
        Linear,

        /// <summary>
        /// The missing lines are made by interpolation, for each field (twice the frame rate).
        /// </summary>
        Linear2x,

        /// <summary>
        /// Yadif (<em>yet another deinterlacing filter</em>), which looks at the lines of the fields before and after.
        /// </summary>
        Yadif,

        /// <summary>
        /// Yadif, for each field (twice the frame rate).
        /// </summary>
        Yadif2x,
    }
}
