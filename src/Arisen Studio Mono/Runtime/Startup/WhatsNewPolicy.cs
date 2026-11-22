using Humanizer;
using System;

namespace ArisenStudio.Runtime.Startup
{
    /// <summary>
    /// Policy and text formatting for the What's New dialog shown once after an update.
    /// Ported from MainWindow.Load (old source: Forms/Windows/MainWindow.cs:218-222) and
    /// DialogExtensions.ShowWhatsNewDialog (old source: Extensions/DialogExtensions.cs:706-716).
    /// Pure: no UI, no I/O, no settings writes.
    /// </summary>
    public static class WhatsNewPolicy
    {
        /// <summary>
        /// Title shown in the window caption (same text as the old app).
        /// </summary>
        public const string Title = "Change Log - What's New";

        /// <summary>
        /// True when the What's New step should run.
        /// Old rule: it runs only inside the internet branch, and only when the flag is set.
        /// The flag defaults to true (SettingsData.FirstTimeOpenAfterUpdate).
        /// </summary>
        /// <param name="internetBranchEntered">True when the startup internet branch was entered.</param>
        /// <param name="firstTimeOpenAfterUpdate">The value of SettingsData.FirstTimeOpenAfterUpdate.</param>
        public static bool ShouldShow(bool internetBranchEntered, bool firstTimeOpenAfterUpdate)
        {
            return internetBranchEntered && firstTimeOpenAfterUpdate;
        }

        /// <summary>
        /// The flag value to store after the What's New step has run. The old code cleared it before
        /// showing the dialog, so it is false even if the dialog fails.
        /// The caller must store this value before showing the dialog.
        /// </summary>
        public static bool FlagAfterShow()
        {
            return false;
        }

        /// <summary>
        /// Subtitle in the form "{release name} ({published date})".
        /// Old code used DateTime.ToOrdinalWords(). The Mono shim formats the date as "d MMMM yyyy" (UNVERIFIED),
        /// so the wording may differ from the real Humanizer output.
        /// </summary>
        public static string FormatSubtitle(string releaseName, DateTimeOffset publishedAt)
        {
            return $"{releaseName} ({publishedAt.DateTime.ToOrdinalWords()})";
        }

        /// <summary>
        /// Release body with each "- " replaced by "• ", as the old app did.
        /// Returns null when the body is null. The old code threw a NullReferenceException here, which it caught
        /// and logged, so no dialog was shown. The caller must not show the dialog when this returns null.
        /// </summary>
        public static string FormatBody(string body)
        {
            return body == null ? null : body.Replace("- ", "• ");
        }
    }
}
