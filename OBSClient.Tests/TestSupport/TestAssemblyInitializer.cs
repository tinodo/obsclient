namespace OBSStudioClient.Tests.TestSupport
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// What is set once for all the tests of the assembly.
    /// </summary>
    internal static class TestAssemblyInitializer
    {
        /// <summary>
        /// The client gives the opening of a connection two seconds (the TCP connection and the upgrade to a WebSocket), and then returns false. That is far more than a
        /// connection to the machine itself takes, and not on a machine that is busy: a runner of a CI service that runs the tests of many classes at once, or a
        /// workstation that runs a campaign of random walks, can take longer, and the test then waits for a handshake that is never going to come, until its own limit
        /// (ten seconds) ends it. The tests are given time to open the connection, and fail when something else is wrong.
        /// </summary>
        [ModuleInitializer]
        internal static void AllowTheConnectionsOfTheTestsTimeToOpen()
        {
            ObsClient.DefaultConnectTimeout = TimeSpan.FromSeconds(30);
        }
    }
}
