#if SELFCHECK
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Models.Database;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// TransferDialog construction. The dialog is built and never shown: OnLoad starts the transfer, so it must not run.
    /// </summary>
    internal static class TransferDialogChecks
    {
        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("TransferDialog.Construct", Construct));
            cases.Add(new SelfCheckCase("TransferDialog.Properties", Properties));
        }

        private static Control Find(Control parent, string name)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name == name)
                {
                    return control;
                }
            }
            throw new SelfCheckFailure("control '" + name + "' not found");
        }

        private static void Construct()
        {
            using (TransferDialog dialog = new TransferDialog())
            {
                SelfCheckAssert.Equal("Transfer Dialog", dialog.Text, "dialog title before load");
                SelfCheckAssert.Equal(FormBorderStyle.FixedDialog, dialog.FormBorderStyle, "border style");
                SelfCheckAssert.True(!dialog.ControlBox, "ControlBox should be off");
                SelfCheckAssert.True(!dialog.MaximizeBox && !dialog.MinimizeBox, "no maximise or minimise box");
                SelfCheckAssert.True(!dialog.IsHandleCreated, "construction must not create a window handle");

                SelfCheckAssert.Equal(6, dialog.Controls.Count, "child control count");
                SelfCheckAssert.Equal("Cancel", Find(dialog, "ButtonCancel").Text, "cancel button text");
                SelfCheckAssert.Equal("Open Folder", Find(dialog, "ButtonOpenFolder").Text, "open folder button text");
                SelfCheckAssert.True(!Find(dialog, "ButtonOpenFolder").Visible, "open folder button hidden until the transfer ends");
                SelfCheckAssert.Equal("Preparing...", ((Label)Find(dialog, "LabelStatus")).Text.Trim(), "initial status text");

                ProgressBar bar = (ProgressBar)Find(dialog, "ProgressBarStatus");
                SelfCheckAssert.Equal(0, bar.Minimum, "progress minimum");
                SelfCheckAssert.Equal(100, bar.Maximum, "progress maximum");
            }
        }

        private static void Properties()
        {
            using (TransferDialog dialog = new TransferDialog())
            {
                SelfCheckAssert.NotNull(dialog.Settings, "Settings default");
                SelfCheckAssert.NotNull(dialog.ConsoleOperations, "ConsoleOperations default");
                SelfCheckAssert.True(dialog.ConsoleOperations is TransferNotConnectedOperations, "ConsoleOperations should default to TransferNotConnectedOperations (reports \"Connect to a console first\")");
                SelfCheckAssert.True(dialog.Language == null, "Language should default to null");
                SelfCheckAssert.True(dialog.ConsoleProfile == null, "ConsoleProfile should default to null");
                SelfCheckAssert.True(!dialog.IsSuccessful, "IsSuccessful should start false");
                SelfCheckAssert.Equal(string.Empty, dialog.LocalPath, "LocalPath default");
                SelfCheckAssert.True(!dialog.CancellationToken.IsCancellationRequested, "the token must not start cancelled");

                CategoryItem category = new CategoryItem { Id = "BLUS30001", Title = "Sample Game", Type = "game" };
                ModItemData mod = new ModItemData { Name = "Sample Mod" };
                dialog.TransferType = TransferType.DownloadMods;
                dialog.Category = category;
                dialog.ModItem = mod;
                dialog.GameRegion = "USA";
                dialog.LocalPath = "/tmp/arisen-selfcheck-unused";

                SelfCheckAssert.Equal(TransferType.DownloadMods, dialog.TransferType, "TransferType round trip");
                SelfCheckAssert.True(object.ReferenceEquals(dialog.Category, category), "Category round trip");
                SelfCheckAssert.True(object.ReferenceEquals(dialog.ModItem, mod), "ModItem round trip");
                SelfCheckAssert.Equal("USA", dialog.GameRegion, "GameRegion round trip");
                SelfCheckAssert.Equal("/tmp/arisen-selfcheck-unused", dialog.LocalPath, "LocalPath round trip");

                // Setting the properties must not start anything: no handle, still the construction title.
                SelfCheckAssert.True(!dialog.IsHandleCreated, "setting properties must not create a window handle");
                SelfCheckAssert.Equal("Transfer Dialog", dialog.Text, "title is only set when the dialog loads");
            }
        }
    }
}
#endif
