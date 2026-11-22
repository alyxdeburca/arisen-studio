#if SELFCHECK
using System;
using System.Collections.Generic;
using System.IO;
using ArisenStudio.Database;
using ArisenStudio.Extensions;
using ArisenStudio.Models.Database;
using ArisenStudio.Runtime;
using Humanizer;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Q-036, throw variant. A blank trainer name must throw ArgumentNullException (the old app's behaviour) before any
    /// filesystem call, so the title folder and the files in it survive. Trainer only.
    /// </summary>
    internal static class BlankNameArchiveChecks
    {
        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("BlankName.ArchiveCleanupKeepsTitleFolder", ArchiveCleanupKeepsTitleFolder));
        }

        private static void ArchiveCleanupKeepsTitleFolder()
        {
            char sep = Path.DirectorySeparatorChar;
            string root = Path.Combine(Path.GetTempPath(), "arisen-selfcheck-blankname-" + Guid.NewGuid().ToString("N"));
            string savedDownloads = AppHost.Settings.PathDownloads;
            try
            {
                // Absolute temp folder. Every path this case touches is under it; the user's download folder is never used.
                AppHost.Settings.PathDownloads = root + sep;

                // file:// URL to a file that does not exist. The throw variant never reaches the download; the pre-b107 file does, and it fails offline.
                string missing = new Uri(Path.Combine(root, "missing.zip")).AbsoluteUri;

                TrainerGameItem game = new TrainerGameItem { TitleId = "TITLE0001" };
                TrainerItem trainer = new TrainerItem { Name = "", Type = "aurora", Url = missing };

                // Title folder, built the same way DownloadDataDirectory builds it, but without calling it (it throws for a blank name).
                string titleFolder = Path.Combine(AppHost.Settings.PathDownloads.GetFullPath(AppHost.Settings.PathAppData), game.GetPlatform().Humanize(), "Trainers", "TITLE0001");
                SelfCheckAssert.True(titleFolder.StartsWith(root, StringComparison.Ordinal), "title folder left the temp root: " + titleFolder);

                Directory.CreateDirectory(titleFolder);
                string keep = Path.Combine(titleFolder, "keep.txt");
                File.WriteAllText(keep, "keep");

                Exception thrown = null;
                try
                {
                    game.DownloadArchiveFiles(trainer).GetAwaiter().GetResult();
                }
                catch (ArgumentNullException ex)
                {
                    thrown = ex;
                }

                SelfCheckAssert.True(File.Exists(keep), "keep.txt in the title folder was deleted by archive cleanup");
                SelfCheckAssert.NotNull(thrown, "blank trainer name: the call returned without throwing");
                SelfCheckAssert.True(thrown is ArgumentNullException, "blank trainer name: expected ArgumentNullException, got " + thrown.GetType().FullName);
            }
            finally
            {
                AppHost.Settings.PathDownloads = savedDownloads;
                IoExtensions.DeleteDirectory(root);
            }
        }
    }
}
#endif
