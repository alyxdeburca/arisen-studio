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
    /// --timeout seconds (per check, default 30; a check that blocks, for example on a message box, fails with "timeout"),
    /// --source-root folder (the Mono source folder, for the one check that reads a source file).
    /// </summary>
    internal static class SelfCheckRunner
    {
        private static readonly object Gate = new object();
        private static string _current;
        private static long _deadlineTicks;
        private static string _sourceRoot;

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
            int timeoutSeconds = 30;
            string timeoutText = ArgValue(args, "--timeout");
            int parsed;
            if (timeoutText != null && int.TryParse(timeoutText, out parsed) && parsed > 0)
            {
                timeoutSeconds = parsed;
            }
            _sourceRoot = ArgValue(args, "--source-root") ?? Environment.GetEnvironmentVariable("ARISEN_SOURCE_ROOT");

            List<SelfCheckCase> cases = new List<SelfCheckCase>();
            ServicesChecks.AddTo(cases);
            GameRegionsXboxHdKeyChecks.AddTo(cases);
            GameSaveDialogChecks.AddTo(cases);
            ShellPageChecks.AddTo(cases);
            TransferDialogChecks.AddTo(cases);
            IntegrationChecks.AddTo(cases);

            if (listOnly)
            {
                foreach (SelfCheckCase listed in cases)
                {
                    Console.WriteLine(listed.Name);
                }
                return 0;
            }

            StartWatchdog(timeoutSeconds);

            int total = 0;
            int failed = 0;
            foreach (SelfCheckCase check in cases)
            {
                if (only != null && check.Name.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                total++;
                lock (Gate)
                {
                    _current = check.Name;
                    _deadlineTicks = DateTime.UtcNow.AddSeconds(timeoutSeconds).Ticks;
                }

                string reason = null;
                try
                {
                    check.Body();
                }
                catch (Exception ex)
                {
                    reason = Describe(ex);
                }

                lock (Gate)
                {
                    _current = null;
                }

                if (reason == null)
                {
                    Console.WriteLine("SELFCHECK " + check.Name + " PASS");
                }
                else
                {
                    failed++;
                    Console.WriteLine("SELFCHECK " + check.Name + " FAIL " + reason);
                }
                Console.Out.Flush();
            }

            if (total == 0)
            {
                Console.WriteLine("SELFCHECK Runner FAIL no check matched" + (only == null ? string.Empty : " --only " + only));
                return 1;
            }

            Console.WriteLine("SELFCHECK-SUMMARY " + (total - failed) + " passed, " + failed + " failed, " + total + " total");
            return failed == 0 ? 0 : 1;
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

        private static string ArgValue(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        // A check that blocks (a modal message box, a wait) would hang the run. The watchdog fails it and ends the process.
        private static void StartWatchdog(int timeoutSeconds)
        {
            Thread watchdog = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(500);
                    string blocked = null;
                    lock (Gate)
                    {
                        if (_current != null && DateTime.UtcNow.Ticks > _deadlineTicks)
                        {
                            blocked = _current;
                        }
                    }

                    if (blocked != null)
                    {
                        Console.WriteLine("SELFCHECK " + blocked + " FAIL timeout after " + timeoutSeconds + " s (blocked, perhaps on a message box)");
                        Console.Out.Flush();
                        Environment.Exit(1);
                    }
                }
            });
            watchdog.IsBackground = true;
            watchdog.Name = "SelfCheckWatchdog";
            watchdog.Start();
        }
    }
}
#endif
