namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>
    /// The virtual time of a simulated OBS Studio. It starts at zero and only <see cref="SimObs"/> moves it, so nothing in the
    /// simulation depends on the wall clock.
    /// </summary>
    internal sealed class SimClock
    {
        /// <summary>Gets the current virtual time.</summary>
        public TimeSpan Now { get; private set; }

        /// <summary>Moves the clock forward. Only <see cref="SimObs"/> calls this.</summary>
        internal void MoveTo(TimeSpan time)
        {
            if (time < Now)
            {
                throw new InvalidOperationException($"The virtual clock cannot go back from {Now} to {time}.");
            }

            Now = time;
        }
    }
}
