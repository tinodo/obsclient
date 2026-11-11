namespace OBSStudioClient.Model
{
    /// <summary>
    /// Where OBS Studio keeps the data that it holds for an application (<see cref="ObsModel.GetPersistentDataAsync"/> and <see cref="ObsModel.SetPersistentDataAsync"/>).
    /// </summary>
    public enum ObsDataRealm
    {
        /// <summary>
        /// For all profiles: it is the same whichever profile is current (<c>persistent_data.json</c> in the configuration of obs-websocket).
        /// </summary>
        Global,

        /// <summary>
        /// For the profile that is current: another profile has other data (<c>obsWebSocketPersistentData.json</c> in the folder of the profile).
        /// </summary>
        Profile,
    }
}
