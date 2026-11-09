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

        /// <summary>
        /// The thread pool of the test process is given room for the tests that keep its threads busy. The pool starts with as many threads as the machine has processors (four on the
        /// Linux and Windows runners of GitHub Actions, three on its macOS runner), and does not add one while those are busy until nothing has been taken from its queue for a while: for half
        /// a second, and, on a machine whose processors are busy, for one second for every thread that it aims at (four seconds with four). The test platform keeps two of those threads for
        /// the whole run (one reads the messages of the test run, and in the other the runner of xUnit waits for its end), and many tests keep one busy on purpose: a handler of an event, or a
        /// hook, that waits until the test lets it go, to see what the library does meanwhile. Two of those tests at the same time are enough to make the pool stand still. For four seconds
        /// nothing that needs the pool runs (the answers of the stand-in for OBS Studio, the loops of the model), the requests whose time is up meanwhile end with a timeout, and a test fails:
        /// another one each time, and only on a machine with few processors. The pool is told to start threads at once, up to sixteen, or four for every processor if that is more: the tests
        /// that run side by side keep a few busy together, not more.
        /// </summary>
        [ModuleInitializer]
        internal static void GiveTheThreadPoolRoomForTheTestsThatKeepItsThreadsBusy()
        {
            var wanted = Math.Max(16, Environment.ProcessorCount * 4);
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.SetMinThreads(Math.Max(workerThreads, wanted), completionPortThreads);
        }
    }
}
