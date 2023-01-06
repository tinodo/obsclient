namespace OBSStudioClient.Enums
{
    /// <summary>
    /// Event Subscriptions.
    /// </summary>
    [Flags]
    public enum EventSubscriptions
    {
        /// <summary>
        /// No Events
        /// </summary>
        None = 0,

        /// <summary>
        /// General Events
        /// </summary>
        General = 1 << 0,

        /// <summary>
        /// Config Events
        /// </summary>
        Config = 1 << 1,

        /// <summary>
        /// Scenes Events
        /// </summary>
        Scenes = 1 << 2,

        /// <summary>
        /// Inputs Events
        /// </summary>
        Inputs = 1 << 3,

        /// <summary>
        /// Transition Events
        /// </summary>
        Transitions = 1 << 4,

        /// <summary>
        /// Filters Events
        /// </summary>
        Filters = 1 << 5,

        /// <summary>
        /// Outputs Events
        /// </summary>
        Outputs = 1 << 6,

        /// <summary>
        /// Scene Items Events
        /// </summary>
        SceneItems = 1 << 7,

        /// <summary>
        /// Media Inputs Events
        /// </summary>
        MediaInputs = 1 << 8,

        /// <summary>
        /// Vendors Events
        /// </summary>
        Vendors = 1 << 9,

        /// <summary>
        /// UI Events
        /// </summary>
        Ui = 1 << 10,

        /// <summary>
        /// All non-high-volume Events
        /// </summary>
        All = General | Config | Scenes | Inputs | Transitions | Filters | Outputs | SceneItems | MediaInputs | Vendors | Ui,

        /// <summary>
        /// Input Volume Meters Events
        /// </summary>
        /// <remarks>
        /// High volume
        /// </remarks>
        InputVolumeMeters = 1 << 16,

        /// <summary>
        /// Input Active State Changed Events
        /// </summary>
        /// <remarks>
        /// High volume
        /// </remarks>
        InputActiveStateChanged = 1 << 17,

        /// <summary>
        /// Input Show State Changed Events
        /// </summary>
        /// <remarks>
        /// High volume
        /// </remarks>
        InputShowStateChanged = 1 << 18,

        /// <summary>
        /// Scene Item Transform Changed Events
        /// </summary>
        /// <remarks>
        /// High volume
        /// </remarks>
        SceneItemTransformChanged = 1 << 19,
    }
}
