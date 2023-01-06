namespace OBSStudioClient.Enums
{
    /// <summary>
    /// Key Modifiers
    /// </summary>
    public enum KeyModifier
    {
        /// <summary>
        /// No Key Modifier.
        /// </summary>
        None = 0,

        /// <summary>
        /// The CONTROL Key.
        /// </summary>
        Control = 1,

        /// <summary>
        /// The SHIFT Key.
        /// </summary>
        Shift = 2,

        /// <summary>
        /// The ALT Key.
        /// </summary>
        Alt = 4,

        /// <summary>
        /// The COMMAND Key.
        /// </summary>
        /// <remarks>
        /// Typically for MacOS.
        /// </remarks>
        Command = 8,
    }
}
