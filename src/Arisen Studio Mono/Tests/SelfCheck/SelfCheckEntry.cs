#if SELFCHECK && SELFCHECK_MAIN
using System;
using System.Windows.Forms;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Entry point for a self-check build. It exists only when both SELFCHECK and SELFCHECK_MAIN are defined, and that
    /// build must also name this class as the startup object (msbuild -p:StartupObject=ArisenStudio.Tests.SelfCheck.SelfCheckEntry),
    /// because Program already has a Main. A normal build, or one that defines only SELFCHECK, does not see this file.
    /// To call the runner from Program instead, call SelfCheckRunner.Run(args) and return its value.
    /// </summary>
    internal static class SelfCheckEntry
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Same order as Program.Main: this must run before the first control is created.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
            {
                Console.WriteLine("SELFCHECK Runner FAIL unhandled UI thread exception: " + SelfCheckRunner.Describe(e.Exception));
                Environment.Exit(1);
            };

            try
            {
                return SelfCheckRunner.Run(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SELFCHECK Runner FAIL " + SelfCheckRunner.Describe(ex));
                return 1;
            }
        }
    }
}
#endif
