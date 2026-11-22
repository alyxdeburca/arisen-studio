#if SELFCHECK
using System.Collections.Generic;
using ArisenStudio.Services;
using ArisenStudio.Services.Extra;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Wraps the three existing in-memory self-checks (each Run returns the list of failure texts, empty when all passed).
    /// They touch no file, network or console, and need no display.
    /// </summary>
    internal static class ServicesChecks
    {
        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("Services.SelfCheck", () => FromFailures(ServicesSelfCheck.Run())));
            cases.Add(new SelfCheckCase("Services.SelfCheckBatch2", () => FromFailures(ServicesSelfCheckBatch2.Run())));
            cases.Add(new SelfCheckCase("Services.ExtraSelfCheck", () => FromFailures(ServicesExtraSelfCheck.Run())));
        }

        private static void FromFailures(List<string> failures)
        {
            if (failures == null)
            {
                throw new SelfCheckFailure("Run() returned null");
            }

            if (failures.Count > 0)
            {
                int shown = failures.Count < 3 ? failures.Count : 3;
                string text = string.Join("; ", failures.GetRange(0, shown).ToArray());
                throw new SelfCheckFailure(failures.Count + " failure(s): " + text + (failures.Count > shown ? "; ..." : string.Empty));
            }
        }
    }
}
#endif
