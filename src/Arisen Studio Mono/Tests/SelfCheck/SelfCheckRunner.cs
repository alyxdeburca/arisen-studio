#if SELFCHECK
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// One named check. The body throws (SelfCheckFailure or any other exception) to fail, and returns to pass.
    /// </summary>
    internal sealed class SelfCheckCase
    {
        public SelfCheckCase(string name, Action body)
        {
            Name = name;
            Body = body;
        }

        public string Name { get; private set; }

        public Action Body { get; private set; }
    }

    /// <summary>
    /// Thrown by <see cref="SelfCheckAssert" /> when an assertion is false.
    /// </summary>
    internal sealed class SelfCheckFailure : Exception
    {
        public SelfCheckFailure(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Assertions for the check bodies. Each one throws SelfCheckFailure with a one-line reason.
    /// </summary>
    internal static class SelfCheckAssert
    {
        public static void True(bool condition, string what)
        {
            if (!condition)
            {
                throw new SelfCheckFailure(what);
            }
        }

        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new SelfCheckFailure(what + ": expected <" + Show(expected) + "> but was <" + Show(actual) + ">");
            }
        }

        public static void NotNull(object value, string what)
        {
            if (value == null)
            {
                throw new SelfCheckFailure(what + ": was null");
            }
        }

        private static string Show(object value)
        {
            return value == null ? "null" : value.ToString();
        }
    }

    /// <summary>
    /// Deterministic runtime self-check runner. Runs the Services self-checks and the form checks one after the other,
    /// prints one line per check, and returns 0 only when every check passed. No network, no console, no window is shown.
    /// WinForms still needs a display connection to create controls: on a headless machine run it under xvfb-run.
    /// Command line (all optional): --only substring (run only checks whose name contains it), --list (print the names),
    /// --timeout seconds (per check, default 5). A check that does not finish within its timeout is judged FAIL
    /// "timed out after N s"; its thread is left running and the run continues with the next check.
    /// --source-root folder (the Mono source folder, for the one check that reads a source file).
    /// The whole run has a last-resort limit of 20 minutes. When it fires, every check not yet judged is printed as
    /// FAIL "not run" and the process ends with exit code 1.
    /// </summary>
    internal static class SelfCheckRunner
    {
        private const int DefaultTimeoutSeconds = 5;
        private const int WholeRunLimitSeconds = 1200;

        private static readonly object Gate = new object();
        private static string _sourceRoot;

        // All of the following are read and written only under Gate.
        private static List<SelfCheckCase> _selected;
        private static int _judged;
        private static int _passed;
        private static bool _finished;

        /// <summary>
        /// The Mono source folder given with --source-root (or ARISEN_SOURCE_ROOT), or null.
        /// </summary>
        internal static string SourceRoot
        {
            get { return _sourceRoot; }
        }

        internal static int Run(string[] args)
        {
            if (args == null)
            {
                args = new string[0];
            }

            string only = ArgValue(args, "--only");
            bool listOnly = Array.IndexOf(args, "--list") >= 0;
            int timeoutSeconds = DefaultTimeoutSeconds;
            string timeoutText = ArgValue(args, "--timeout");
            int parsed;
            if (timeoutText != null && int.TryParse(timeoutText, out parsed) && parsed > 0)
            {
                timeoutSeconds = parsed;
            }
            _sourceRoot = ArgValue(args, "--source-root") ?? Environment.GetEnvironmentVariable("ARISEN_SOURCE_ROOT");

            List<SelfCheckCase> cases = new List<SelfCheckCase>();
            ServicesChecks.AddTo(cases);
            AnnouncementsTextChecks.AddTo(cases);
            GameRegionsXboxHdKeyChecks.AddTo(cases);
            GameSaveDialogChecks.AddTo(cases);
            ShellPageChecks.AddTo(cases);
            TransferDialogChecks.AddTo(cases);
            IntegrationChecks.AddTo(cases);
            FakeConsoleServerChecks.AddTo(cases);
            StartupGateChecks.AddTo(cases);
            FakeServerPs3ControlChecks.AddTo(cases);
            BlankNameArchiveChecks.AddTo(cases);

            if (listOnly)
            {
                foreach (SelfCheckCase listed in cases)
                {
                    Console.WriteLine(listed.Name);
                }
                return 0;
            }

            List<SelfCheckCase> selected = new List<SelfCheckCase>();
            foreach (SelfCheckCase check in cases)
            {
                if (only == null || check.Name.IndexOf(only, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selected.Add(check);
                }
            }

            if (selected.Count == 0)
            {
                Console.WriteLine("SELFCHECK Runner FAIL no check matched" + (only == null ? string.Empty : " --only " + only));
                return 1;
            }

            lock (Gate)
            {
                _selected = selected;
                _judged = 0;
                _passed = 0;
                _finished = false;
            }
            StartWatchdog();

            foreach (SelfCheckCase check in selected)
            {
                string reason = RunWithTimeout(check, timeoutSeconds);

                lock (Gate)
                {
                    if (_finished)
                    {
                        // The watchdog has already reported this run and is ending the process.
                        return 1;
                    }

                    _judged++;
                    if (reason == null)
                    {
                        _passed++;
                        Console.WriteLine("SELFCHECK " + check.Name + " PASS");
                    }
                    else
                    {
                        Console.WriteLine("SELFCHECK " + check.Name + " FAIL " + reason);
                    }
                    Console.Out.Flush();
                }
            }

            lock (Gate)
            {
                if (_finished)
                {
                    return 1;
                }

                _finished = true;
                PrintSummaryLocked();
                return _passed == _selected.Count ? 0 : 1;
            }
        }

        /// <summary>
        /// One-line reason for a failed check. Unwraps reflection wrappers so the real exception shows.
        /// </summary>
        internal static string Describe(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            string text = ex is SelfCheckFailure ? ex.Message : ex.GetType().Name + ": " + ex.Message;
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length == 0 ? ex.GetType().Name : text;
        }

        // Runs one check on its own background thread and waits up to timeoutSeconds. Returns null on pass, else the reason.
        // A check that is still running at the timeout keeps its thread (background, so it does not hold the process open).
        private static string RunWithTimeout(SelfCheckCase check, int timeoutSeconds)
        {
            CheckRun run = new CheckRun();
            Thread thread = new Thread(() => run.Execute(check));
            thread.IsBackground = true;
            thread.Name = "SelfCheck " + check.Name;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (thread.Join(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                return run.Reason;
            }
            return "timed out after " + timeoutSeconds + " s";
        }

        // Caller holds Gate.
        private static void PrintSummaryLocked()
        {
            int total = _selected.Count;
            Console.WriteLine("SELFCHECK-SUMMARY " + _passed + " passed, " + (total - _passed) + " failed, " + total + " total");
            Console.Out.Flush();
        }

        private static string ArgValue(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        // Last-resort limit for the whole run. When it fires, every check not yet judged is reported as FAIL "not run".
        private static void StartWatchdog()
        {
            DateTime limit = DateTime.UtcNow.AddSeconds(WholeRunLimitSeconds);
            Thread watchdog = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(500);
                    lock (Gate)
                    {
                        if (_finished)
                        {
                            return;
                        }
                        if (DateTime.UtcNow < limit)
                        {
                            continue;
                        }

                        _finished = true;
                        for (int i = _judged; i < _selected.Count; i++)
                        {
                            Console.WriteLine("SELFCHECK " + _selected[i].Name + " FAIL not run");
                        }
                        PrintSummaryLocked();
                    }
                    Environment.Exit(1);
                }
            });
            watchdog.IsBackground = true;
            watchdog.Name = "SelfCheckWatchdog";
            watchdog.Start();
        }

        // Holds the outcome of one check body. Written on the check's thread, read by the runner after Join.
        private sealed class CheckRun
        {
            public string Reason;

            public void Execute(SelfCheckCase check)
            {
                try
                {
                    check.Body();
                }
                catch (Exception ex)
                {
                    Reason = Describe(ex);
                }
            }
        }
    }
}
#endif
