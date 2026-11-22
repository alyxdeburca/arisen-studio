#if SELFCHECK
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ArisenStudio.Forms.Dialogs.Details;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Checks the "Installation Files" collapse toggle on each download row of GameSaveDialog. The dialog is constructed
    /// and filled by calling its private ShowGameSaveDetails; it is never shown (OnLoad does not run, so nothing is sized).
    /// A control inside a form that is not shown reports Visible as false whatever it was set to, so the check reads the
    /// row height (the value the toggle writes) and not ListBox.Visible.
    /// </summary>
    internal static class GameSaveDialogChecks
    {
        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("GameSaveDialog.CollapseToggle", CollapseToggle));
            cases.Add(new SelfCheckCase("GameSaveDialog.CollapseToggleEmptyPaths", CollapseToggleEmptyPaths));
            cases.Add(new SelfCheckCase("GameSaveDialog.NoItem", NoItem));
        }

        private static DownloadFiles File(string name, params string[] paths)
        {
            return new DownloadFiles
            {
                Name = name,
                Region = "USA",
                Version = "1.00",
                LastUpdated = new DateTime(2024, 1, 1),
                Url = "https://example.invalid/" + name,
                InstallPaths = paths == null ? null : new List<string>(paths)
            };
        }

        private static GameSaveDialog Build(List<DownloadFiles> files)
        {
            SettingsData settings = new SettingsData();
            settings.UseRelativeTimes = false;

            GameSaveDialog dialog = new GameSaveDialog
            {
                Settings = settings,
                CategoryType = CategoryType.GameSave,
                GameSaveItem = new GameSaveItemData
                {
                    Platform = "PlayStation3",
                    Id = 1,
                    CategoryId = "1",
                    Name = "Sample Save",
                    Region = "USA",
                    LastUpdated = new DateTime(2024, 1, 1),
                    CreatedBy = "Sample Author",
                    SubmittedBy = "Sample Submitter",
                    Version = "1.00",
                    GameMode = "Single Player",
                    Description = "A sample save description.",
                    DownloadFiles = files
                }
            };
            SelfCheckReflect.Call(dialog, "ShowGameSaveDetails");
            return dialog;
        }

        private static TableLayoutPanel Rows(GameSaveDialog dialog)
        {
            return SelfCheckReflect.GetField<TableLayoutPanel>(dialog, "_downloadsList");
        }

        private static Control At(TableLayoutPanel table, int row)
        {
            foreach (Control control in table.Controls)
            {
                if (table.GetRow(control) == row)
                {
                    return control;
                }
            }
            throw new SelfCheckFailure("nothing in row " + row + " of the table");
        }

        private static Button ToggleOf(TableLayoutPanel row)
        {
            Control actions = At(row, 3);
            foreach (Control control in actions.Controls)
            {
                Button button = control as Button;
                if (button != null && button.Text == "Installation Files")
                {
                    return button;
                }
            }
            throw new SelfCheckFailure("no 'Installation Files' button in the actions row");
        }

        private static float ExpandedHeight(GameSaveDialog dialog, int pathCount)
        {
            int h = dialog.Font.Height;
            int pad = h / 2;
            return h * Math.Min(Math.Max(1, pathCount), 8) + pad;
        }

        private static void CollapseToggle()
        {
            using (GameSaveDialog dialog = Build(new List<DownloadFiles> { File("one.zip", "/a", "/b", "/c"), File("two.zip", "/x") }))
            {
                TableLayoutPanel list = Rows(dialog);
                SelfCheckAssert.Equal(2, list.Controls.Count, "one row per download file");

                TableLayoutPanel first = (TableLayoutPanel)At(list, 0);
                TableLayoutPanel second = (TableLayoutPanel)At(list, 1);
                ListBox firstPaths = (ListBox)At(first, 4);
                Button toggle = ToggleOf(first);

                SelfCheckAssert.Equal(3, firstPaths.Items.Count, "install paths listed in the first row");
                SelfCheckAssert.Equal(0F, first.RowStyles[4].Height, "first row starts collapsed");
                SelfCheckAssert.Equal(0F, second.RowStyles[4].Height, "second row starts collapsed");

                // Expand.
                SelfCheckReflect.RaiseClick(toggle);
                SelfCheckAssert.Equal(ExpandedHeight(dialog, 3), first.RowStyles[4].Height, "first row expanded height");
                SelfCheckAssert.Equal(0F, second.RowStyles[4].Height, "toggling one row leaves the other alone");

                // Collapse: 20, never 0 (a zero-height ListBox cannot be drawn on Mono).
                SelfCheckReflect.RaiseClick(toggle);
                SelfCheckAssert.Equal(20F, first.RowStyles[4].Height, "first row collapsed height");

                // Expand again, then toggle the second row on its own.
                SelfCheckReflect.RaiseClick(toggle);
                SelfCheckAssert.Equal(ExpandedHeight(dialog, 3), first.RowStyles[4].Height, "first row expanded again");

                SelfCheckReflect.RaiseClick(ToggleOf(second));
                SelfCheckAssert.Equal(ExpandedHeight(dialog, 1), second.RowStyles[4].Height, "second row expanded height");
                SelfCheckAssert.Equal(ExpandedHeight(dialog, 3), first.RowStyles[4].Height, "first row unchanged by the second toggle");
            }
        }

        private static void CollapseToggleEmptyPaths()
        {
            // Null and empty install path lists: the list is empty and the expanded height is one line plus padding.
            using (GameSaveDialog dialog = Build(new List<DownloadFiles> { File("none.zip", (string[])null), File("empty.zip") }))
            {
                TableLayoutPanel list = Rows(dialog);
                SelfCheckAssert.Equal(2, list.Controls.Count, "one row per download file");

                for (int i = 0; i < 2; i++)
                {
                    TableLayoutPanel row = (TableLayoutPanel)At(list, i);
                    SelfCheckAssert.Equal(0, ((ListBox)At(row, 4)).Items.Count, "row " + i + " lists no paths");

                    Button toggle = ToggleOf(row);
                    SelfCheckReflect.RaiseClick(toggle);
                    SelfCheckAssert.Equal(ExpandedHeight(dialog, 0), row.RowStyles[4].Height, "row " + i + " expanded height");
                    SelfCheckReflect.RaiseClick(toggle);
                    SelfCheckAssert.Equal(20F, row.RowStyles[4].Height, "row " + i + " collapsed height");
                }
            }
        }

        private static void NoItem()
        {
            // No download files at all, and no game save at all: no rows, no throw.
            using (GameSaveDialog dialog = Build(null))
            {
                SelfCheckAssert.Equal(0, Rows(dialog).Controls.Count, "no download rows without download files");
            }

            using (GameSaveDialog dialog = new GameSaveDialog())
            {
                SelfCheckReflect.Call(dialog, "ShowGameSaveDetails");
                SelfCheckAssert.Equal(0, Rows(dialog).Controls.Count, "no download rows without a game save");
            }
        }
    }
}
#endif
