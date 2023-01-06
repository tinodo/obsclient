namespace SampleWindowsAppliation
{
    using OBSStudioClient.Exceptions;

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);
            Application.ThreadException += new ThreadExceptionEventHandler(MyHandler2);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }

        static void MyHandler2(object sender, ThreadExceptionEventArgs args)
        {
            if (args.Exception is ObsResponseException obsResponseException)
            {
                MessageBox.Show($"{obsResponseException.ErrorCode}: {obsResponseException.ErrorMessage}", "OBSResponseException");
            }
            else if (args.Exception is ObsClientException obsClientException)
            {
                MessageBox.Show(obsClientException.Message, "OBSClientException");
            }
            else
            {
                Exception e = (Exception)args.Exception;
                MessageBox.Show(e.Message, args.Exception.GetType().ToString());
            }
        }

        static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is ObsResponseException obsResponseException)
            {
                MessageBox.Show($"{obsResponseException.ErrorCode}: {obsResponseException.ErrorMessage}", "OBSResponseException");
            }
            else if (args.ExceptionObject is ObsClientException obsClientException)
            {
                MessageBox.Show(obsClientException.Message, "OBSClientException");
            }
            else
            {
                Exception e = (Exception)args.ExceptionObject;
                MessageBox.Show(e.Message, args.ExceptionObject.GetType().ToString());
            }
        }
    }
}