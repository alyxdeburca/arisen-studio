#if SELFCHECK
using System;
using System.Collections.Generic;
using System.IO;
using ArisenStudio.Runtime.Startup;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Startup gate checks for Runtime/FullShellIntegration.cs (Startup 1 to 5) and the pure logic in Runtime/Startup/.
    /// Source checks read a file from --source-root (or the working directory). When the file is not found they throw a
    /// failure whose text starts with "SKIP: ". The runner has no SKIP outcome yet, so that shows as FAIL with a SKIP
    /// reason; it is never reported as PASS. Logic checks call only pure members: no network, no mutex, no file write,
    /// no user folder. Not registered: add <c>StartupGateChecks.AddTo(cases)</c> in SelfCheckRunner.Run to run them.
    /// </summary>
    internal static class StartupGateChecks
    {
        private const string Prefix = "StartupGate.";
        private const string FullShell = "Runtime/FullShellIntegration.cs";
        private const string IdleFile = "Runtime/Startup/IdleStatusTimer.cs";
        private const string MaintenanceFile = "Runtime/Startup/MaintenanceGate.cs";
        private const string WhatsNewFile = "Runtime/Startup/WhatsNewPolicy.cs";

        // Tokens that mean I/O, process-wide state or user-folder access. Matched only against code lines, not comments.
        private static readonly string[] IoTokens = new string[]
        {
            "File.", "Directory.", "HttpClient", "WebRequest", "Mutex", "Process.", "Registry",
            "AppHost", "AppBootstrapper", "UserFolders", "Console.", "GetFolderPath"
        };

        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase(Prefix + "GateDetectorSelfTest", () => GateDetectorSelfTest()));
            cases.Add(new SelfCheckCase(Prefix + "MutexGated", () => MutexGated()));
            cases.Add(new SelfCheckCase(Prefix + "StatusHookGated", () => StatusHookGated()));
            cases.Add(new SelfCheckCase(Prefix + "StatusAndWhatsNewOnlyThroughHook", () => StatusAndWhatsNewOnlyThroughHook()));
            cases.Add(new SelfCheckCase(Prefix + "CurrentGamePollGated", () => CurrentGamePollGated()));
            cases.Add(new SelfCheckCase(Prefix + "WizardFlagSmokeOff", () => WizardFlagSmokeOff()));
            cases.Add(new SelfCheckCase(Prefix + "MaintenanceDecide", () => MaintenanceDecide()));
            cases.Add(new SelfCheckCase(Prefix + "WhatsNewShowsOnce", () => WhatsNewShowsOnce()));
            cases.Add(new SelfCheckCase(Prefix + "IdleTimerConstructionInert", () => IdleTimerConstructionInert()));
            cases.Add(new SelfCheckCase(Prefix + "IdleTimerConstructorNoIo", () => IdleTimerConstructorNoIo()));
            cases.Add(new SelfCheckCase(Prefix + "StartupClassesNoIoTokens", () => StartupClassesNoIoTokens()));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Source checks: smoke gates in FullShellIntegration.cs
        // ------------------------------------------------------------------------------------------------------------

        // Pure self-test of the gate detector. Without it the source checks could pass without proving anything.
        private static void GateDetectorSelfTest()
        {
            string[] gated =
            {
                "private static void M(bool smokeMode)", "{", "    if (!smokeMode)", "    {", "        Work();", "    }", "}"
            };
            string[] ungated =
            {
                "private static void M(bool smokeMode)", "{", "    if (true)", "    {", "        Work();", "    }", "}"
            };
            string[] closedBefore =
            {
                "private static void M(bool smokeMode)", "{", "    if (!smokeMode)", "    {", "        Pre();", "    }", "    Work();", "}"
            };

            SelfCheckAssert.True(IsSmokeGated(gated, 4), "detector: an anchor inside if (!smokeMode) was not reported as gated");
            SelfCheckAssert.True(!IsSmokeGated(ungated, 4), "detector: an anchor inside if (true) was reported as gated");
            SelfCheckAssert.True(!IsSmokeGated(closedBefore, 6), "detector: a block closed before the anchor was reported as enclosing");
        }

        // Audit section C, "Single-instance guard": FullShellIntegration.cs (currently lines 126 to 131).
        private static void MutexGated()
        {
            string[] lines = ReadSource(FullShell);
            AssertGated(lines, FindOnce(lines, "guard = new SingleInstanceGuard();", "single-instance guard"), "single-instance guard creation");
            AssertGated(lines, FindOnce(lines, "if (!guard.TryAcquire())", "single-instance TryAcquire"), "single-instance TryAcquire");
        }

        // Audit section C, "Status request" hook: the Shown subscription that starts the status check and What's New.
        private static void StatusHookGated()
        {
            string[] lines = ReadSource(FullShell);
            AssertGated(lines, FindOnce(lines, "shell.Shown += OnShellShownStartup;", "status hook"), "status hook subscription");
        }

        // The status check, maintenance gate, What's New fetch and dialog run only inside OnShellShownStartup. Its only
        // subscription is the gated hook above, and nothing calls it directly, so these are gated through the hook.
        private static void StatusAndWhatsNewOnlyThroughHook()
        {
            string[] lines = ReadSource(FullShell);

            int definition = FindOnce(lines, "OnShellShownStartup(", "status handler");
            SelfCheckAssert.True(
                lines[definition].Trim().StartsWith("private static async void OnShellShownStartup(", StringComparison.Ordinal),
                "OnShellShownStartup( is called directly (line " + (definition + 1) + "), so it is not only reached through the gated hook");

            // Only the definition and the gated subscription may name the handler (-= unhooks are excluded).
            int handlerUses = CountCode(lines, "OnShellShownStartup", "-= OnShellShownStartup");
            SelfCheckAssert.True(handlerUses == 2, "OnShellShownStartup is named " + handlerUses + " time(s) in code besides -=; expected only the definition and the gated subscription");

            // Only the definition and the one call may name the What's New method.
            int whatsNewUses = CountCode(lines, "ShowWhatsNewAsync", null);
            SelfCheckAssert.True(whatsNewUses == 2, "ShowWhatsNewAsync is named " + whatsNewUses + " time(s) in code; expected the definition and one call");

            // The maintenance-exit close step (audit C, maintenance row) must sit inside the handler.
            int close = FindOnce(lines, "\"close after maintenance message\"", "maintenance close step");
            AssertInMethod(lines, close, "OnShellShownStartup(", "maintenance close step");

            AssertInMethod(lines, FindOnce(lines, "StartupStatusCheck.IsServerUpAsync(", "status request"), "OnShellShownStartup(", "status request");
            AssertInMethod(lines, FindOnce(lines, "MaintenanceGate.Decide(", "maintenance gate"), "OnShellShownStartup(", "maintenance gate");
            AssertInMethod(lines, FindOnce(lines, "ShowWhatsNewAsync(shell)", "What's New call"), "OnShellShownStartup(", "What's New call");
            AssertInMethod(lines, FindOnce(lines, "UpdateExtensions.GetAllReleasesData(", "What's New release fetch"), "ShowWhatsNewAsync(", "What's New release fetch");
            AssertInMethod(lines, FindOnce(lines, "new WhatsNewDialog(", "What's New dialog"), "ShowWhatsNewAsync(", "What's New dialog");
        }

        // Audit section C, "Current-game poll": FullShellIntegration.cs (currently lines 309 to 311).
        private static void CurrentGamePollGated()
        {
            string[] lines = ReadSource(FullShell);
            AssertGated(lines, FindOnce(lines, "StartCurrentGamePoll(shell, controller);", "current-game poll start"), "current-game poll start");
        }

        // Audit section C, "First-use wizard flag": the value must be the smoke inverse, so smoke mode never runs the wizard.
        private static void WizardFlagSmokeOff()
        {
            string[] lines = ReadSource(FullShell);
            FindOnce(lines, "RunSetupWizardOnFirstUse = !smokeMode", "first-use wizard flag");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Logic checks: pure calls only
        // ------------------------------------------------------------------------------------------------------------

        // MaintenanceGate.Decide(internetAvailable, statusOk): maintenance only when there is internet and the status is not OK.
        private static void MaintenanceDecide()
        {
            ExpectDecision(false, false, false, false);
            ExpectDecision(false, true, false, false);
            ExpectDecision(true, true, false, false);
            ExpectDecision(true, false, true, true);
        }

        private static void ExpectDecision(bool internetAvailable, bool statusOk, bool show, bool exit)
        {
            string call = "Decide(" + internetAvailable + ", " + statusOk + ")";
            MaintenanceDecision decision = MaintenanceGate.Decide(internetAvailable, statusOk);
            SelfCheckAssert.True(decision != null, call + " returned null");
            SelfCheckAssert.True(decision.ShowMaintenanceMessage == show, call + ".ShowMaintenanceMessage expected " + show + " but was " + decision.ShowMaintenanceMessage);
            SelfCheckAssert.True(decision.RequestExit == exit, call + ".RequestExit expected " + exit + " but was " + decision.RequestExit);
        }

        // Proves the latch: after one show, the stored flag stops further shows. This does NOT prove "once per version":
        // WhatsNewPolicy has no version input, and nothing in the project sets the flag back to true.
        private static void WhatsNewShowsOnce()
        {
            SelfCheckAssert.True(WhatsNewPolicy.ShouldShow(true, true), "first run with the flag set does not show");

            bool flagAfterShow = WhatsNewPolicy.FlagAfterShow();
            SelfCheckAssert.True(!flagAfterShow, "FlagAfterShow() is not false");

            SelfCheckAssert.True(!WhatsNewPolicy.ShouldShow(true, flagAfterShow), "a later run with the stored flag shows again");
            SelfCheckAssert.True(!WhatsNewPolicy.ShouldShow(false, true), "What's New shows without the internet branch");
            SelfCheckAssert.True(!WhatsNewPolicy.ShouldShow(true, false), "What's New shows with the flag false");
        }

        // Construction and the first calls do no work: no clock read, no timer, no tick.
        private static void IdleTimerConstructionInert()
        {
            int clockCalls = 0;
            Func<DateTime> clock = () =>
            {
                clockCalls++;
                return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            };
            int idleRaised = 0;

            using (IdleStatusTimer timer = new IdleStatusTimer(null, clock, false))
            {
                timer.Idle += (sender, args) => idleRaised++;

                SelfCheckAssert.True(clockCalls == 0, "construction read the clock " + clockCalls + " time(s)");
                SelfCheckAssert.True(!timer.IsRunning, "a new timer is running");
                SelfCheckAssert.True(!timer.TimeUntilNextTick().HasValue, "a new timer has a next tick");
                SelfCheckAssert.True(!timer.Poll(), "Poll raised a tick on a stopped timer");
                SelfCheckAssert.True(idleRaised == 0, "a tick was raised without Start");
                SelfCheckAssert.True(clockCalls == 0, "Poll on a stopped timer read the clock");
            }

            // The default constructor: Start is what creates the background timer, so construction alone must not run.
            using (IdleStatusTimer plain = new IdleStatusTimer())
            {
                SelfCheckAssert.True(!plain.IsRunning, "a default timer is running after construction");
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Source checks: no I/O in the three pure startup classes
        // ------------------------------------------------------------------------------------------------------------

        // The constructor body of IdleStatusTimer creates no timer and starts nothing. Field initialisers are covered
        // by StartupClassesNoIoTokens, which scans the whole file.
        private static void IdleTimerConstructorNoIo()
        {
            string[] lines = ReadSource(IdleFile);
            List<string> ctor = BlockFrom(lines, "public IdleStatusTimer(");
            SelfCheckAssert.True(ctor.Count > 2, "the IdleStatusTimer constructor body was not found");

            foreach (string line in ctor)
            {
                SelfCheckAssert.True(line.IndexOf("Threading.Timer(", StringComparison.Ordinal) < 0, "the constructor creates a timer: " + line.Trim());
                SelfCheckAssert.True(line.IndexOf("new Timer(", StringComparison.Ordinal) < 0, "the constructor creates a timer: " + line.Trim());
                SelfCheckAssert.True(line.IndexOf("Start(", StringComparison.Ordinal) < 0, "the constructor calls Start: " + line.Trim());
                foreach (string token in IoTokens)
                {
                    SelfCheckAssert.True(line.IndexOf(token, StringComparison.Ordinal) < 0, "the constructor has '" + token + "': " + line.Trim());
                }
            }
        }

        // The three pure classes contain no I/O, process-wide, or user-folder token in their code lines.
        private static void StartupClassesNoIoTokens()
        {
            string[] files = new string[] { IdleFile, MaintenanceFile, WhatsNewFile };
            foreach (string file in files)
            {
                string[] lines = ReadSource(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!IsCode(lines[i]))
                    {
                        continue;
                    }
                    foreach (string token in IoTokens)
                    {
                        SelfCheckAssert.True(
                            lines[i].IndexOf(token, StringComparison.Ordinal) < 0,
                            file + " line " + (i + 1) + " contains '" + token + "' (I/O, process-wide state or user-folder access)");
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------------------------------

        private static bool IsCode(string line)
        {
            string trimmed = line.Trim();
            return trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal);
        }

        private static string PreviousCodeLine(string[] lines, int index)
        {
            for (int j = index - 1; j >= 0; j--)
            {
                if (IsCode(lines[j]))
                {
                    return lines[j].Trim();
                }
            }
            return string.Empty;
        }

        // Headers of the blocks that enclose lines[index], innermost first, ending at the enclosing method (the first
        // header containing " static "). Walks upward by brace depth, so a block closed before the anchor is skipped.
        private static List<string> EnclosingHeaders(string[] lines, int index)
        {
            List<string> headers = new List<string>();
            int pending = 0;
            for (int i = index - 1; i >= 0 && i >= index - 400; i--)
            {
                if (!IsCode(lines[i]))
                {
                    continue;
                }

                string line = lines[i];
                for (int c = line.Length - 1; c >= 0; c--)
                {
                    if (line[c] == '}')
                    {
                        pending++;
                    }
                    else if (line[c] == '{')
                    {
                        if (pending > 0)
                        {
                            pending--;
                        }
                        else
                        {
                            string trimmed = line.Trim();
                            string header = trimmed == "{" ? PreviousCodeLine(lines, i) : trimmed;
                            headers.Add(header);
                            if (header.Contains(" static "))
                            {
                                return headers;
                            }
                        }
                    }
                }
            }
            return headers;
        }

        private static bool IsGateHeader(string header)
        {
            return header.StartsWith("if (", StringComparison.Ordinal) && header.Contains("!smokeMode");
        }

        private static bool IsSmokeGated(string[] lines, int index)
        {
            foreach (string header in EnclosingHeaders(lines, index))
            {
                if (IsGateHeader(header))
                {
                    return true;
                }
            }
            return false;
        }

        private static string EnclosingMethod(string[] lines, int index)
        {
            List<string> headers = EnclosingHeaders(lines, index);
            if (headers.Count == 0)
            {
                return string.Empty;
            }
            string last = headers[headers.Count - 1];
            return last.Contains(" static ") ? last : string.Empty;
        }

        // Number of code lines that contain text, and do not contain exclude (when exclude is not null).
        private static int CountCode(string[] lines, string text, string exclude)
        {
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (IsCode(lines[i]) && lines[i].Contains(text) && (exclude == null || !lines[i].Contains(exclude)))
                {
                    count++;
                }
            }
            return count;
        }

        // The one code line that contains text. Fails when the count is not exactly one.
        private static int FindOnce(string[] lines, string text, string what)
        {
            int found = -1;
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (IsCode(lines[i]) && lines[i].Contains(text))
                {
                    count++;
                    found = i;
                }
            }
            SelfCheckAssert.True(count == 1, what + ": expected exactly one code line with '" + text + "' but found " + count);
            return found;
        }

        private static void AssertGated(string[] lines, int index, string what)
        {
            SelfCheckAssert.True(IsSmokeGated(lines, index), what + " (line " + (index + 1) + ") is not inside an if that tests !smokeMode");
        }

        private static void AssertInMethod(string[] lines, int index, string methodText, string what)
        {
            string method = EnclosingMethod(lines, index);
            SelfCheckAssert.True(method.Contains(methodText), what + " (line " + (index + 1) + ") is not inside a method containing " + methodText + " (enclosing: " + method + ")");
        }

        // The lines from the first code line containing headerText to the close of its first brace block.
        private static List<string> BlockFrom(string[] lines, string headerText)
        {
            int start = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (IsCode(lines[i]) && lines[i].Contains(headerText))
                {
                    start = i;
                    break;
                }
            }
            SelfCheckAssert.True(start >= 0, "header not found in source: " + headerText);

            List<string> block = new List<string>();
            int depth = 0;
            bool opened = false;
            for (int i = start; i < lines.Length; i++)
            {
                block.Add(lines[i]);
                foreach (char c in lines[i])
                {
                    if (c == '{')
                    {
                        depth++;
                        opened = true;
                    }
                    else if (c == '}')
                    {
                        depth--;
                    }
                }
                if (opened && depth == 0)
                {
                    break;
                }
            }
            return block;
        }

        // Reads a source file as lines. Throws a "SKIP: " failure when the file is not found.
        private static string[] ReadSource(string relative)
        {
            string path = FindSource(relative);
            if (path == null)
            {
                throw new SelfCheckFailure("SKIP: source file " + relative + " not found (pass --source-root <Arisen Studio Mono folder>)");
            }

            string[] lines = File.ReadAllText(path).Split(new char[] { '\n' });
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimEnd('\r');
            }
            return lines;
        }

        private static string FindSource(string relative)
        {
            List<string> starts = new List<string>();
            if (SelfCheckRunner.SourceRoot != null)
            {
                starts.Add(SelfCheckRunner.SourceRoot);
            }
            starts.Add(Directory.GetCurrentDirectory());

            string native = relative.Replace('/', Path.DirectorySeparatorChar);
            foreach (string start in starts)
            {
                string folder = start;
                for (int depth = 0; depth < 8 && !string.IsNullOrEmpty(folder); depth++)
                {
                    string candidate = Path.Combine(folder, native);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                    string nested = Path.Combine(Path.Combine(folder, "Arisen Studio Mono"), native);
                    if (File.Exists(nested))
                    {
                        return nested;
                    }
                    folder = Path.GetDirectoryName(folder);
                }
            }
            return null;
        }
    }
}
#endif
