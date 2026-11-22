#if SELFCHECK
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Forms.Pages.Library;
using ArisenStudio.Shell;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// ShellForm page switching. The shell is constructed and never shown (no handle, no Load event). Each page is built,
    /// registered under its ShellPageKeys value in place of the shell's placeholder, and shown with ShellForm.ShowPage.
    /// The About page is the one the shell builds itself. OnPageShown runs for each page as it becomes visible; the file
    /// manager page lists the user's home folder then (local disk only), and no page opens a connection or a window.
    /// </summary>
    internal static class ShellPageChecks
    {
        private sealed class PageSpec
        {
            public PageSpec(string key, string label, Func<Control> create)
            {
                Key = key;
                Label = label;
                Create = create;
            }

            public string Key { get; private set; }

            public string Label { get; private set; }

            // Null for the About page, which the shell builds itself.
            public Func<Control> Create { get; private set; }
        }

        private static readonly PageSpec[] Specs = new PageSpec[]
        {
            new PageSpec(ShellPageKeys.About, "AboutPage", null),
            new PageSpec(ShellPageKeys.Downloads, "DownloadsPage", () => new DownloadsPage()),
            new PageSpec(ShellPageKeys.InstalledFiles, "InstalledFilesPage", () => new InstalledFilesPage()),
            new PageSpec(ShellPageKeys.FileManager, "FileManagerPage", () => new FileManagerPage()),
            new PageSpec(ShellPageKeys.Settings, "SettingsPage", () => new SettingsPage()),
            new PageSpec(ShellPageKeys.CustomMods, "CustomModsPage", () => new CustomModsPage()),
            new PageSpec(ShellPageKeys.GameMods, "GameModsPage", () => new GameModsPage()),
            new PageSpec(ShellPageKeys.Homebrew, "HomebrewPage", () => new HomebrewPage()),
            new PageSpec(ShellPageKeys.Trainers, "TrainersPage", () => new TrainersPage()),
            new PageSpec(ShellPageKeys.Resources, "ResourcesPage", () => new ResourcesPage()),
            new PageSpec(ShellPageKeys.PkgStore, "PkgStorePage", () => new PkgStorePage()),
            new PageSpec(ShellPageKeys.Games, "GamesPage", () => new GamesPage()),
            new PageSpec(ShellPageKeys.GameSaves, "GameSavesPage", () => new GameSavesPage()),
            new PageSpec(ShellPageKeys.GameCheats, "GameCheatsPage", () => new GameCheatsPage())
        };

        public static void AddTo(List<SelfCheckCase> cases)
        {
            foreach (PageSpec spec in Specs)
            {
                PageSpec captured = spec;
                cases.Add(new SelfCheckCase("ShellForm.ShowPage." + spec.Label, () => ShowOne(captured)));
            }

            cases.Add(new SelfCheckCase("ShellForm.ShowPage.UnknownKey", UnknownKey));
            cases.Add(new SelfCheckCase("ShellForm.ShowPage.SwitchCycle", SwitchCycle));
        }

        // Builds the page for a spec and puts it in the shell under its key. The About page is already there.
        private static Control Register(ShellForm shell, PageSpec spec)
        {
            if (spec.Create == null)
            {
                SelfCheckAssert.NotNull(shell.About, "ShellForm.About");
                return shell.About;
            }

            Control page = spec.Create();
            SelfCheckAssert.NotNull(page, spec.Label + " constructor result");
            shell.Pages.RegisterPage(spec.Key, page);
            return page;
        }

        private static void AssertShown(ShellForm shell, string key, Control page, string step)
        {
            SelfCheckAssert.Equal(key, shell.Pages.ActiveKey, step + ": active page key");
            SelfCheckAssert.True(object.ReferenceEquals(page.Parent, shell.Pages), step + ": the page is not a child of the page host");
            SelfCheckAssert.Equal(DockStyle.Fill, page.Dock, step + ": page dock style");
            SelfCheckAssert.Equal(1, shell.Pages.Controls.Count, step + ": page host child count (only the active page is a child)");
        }

        private static void ShowOne(PageSpec spec)
        {
            using (ShellForm shell = new ShellForm())
            {
                Control page = Register(shell, spec);
                SelfCheckAssert.True(shell.Pages.HasPage(spec.Key), "page is registered under '" + spec.Key + "'");

                bool shown = shell.ShowPage(spec.Key);
                SelfCheckAssert.True(shown, "ShowPage('" + spec.Key + "') returned false");
                AssertShown(shell, spec.Key, page, "first show");

                // Away to another page and back. The other page must leave the host, this one must come back.
                string other = spec.Key == ShellPageKeys.About ? ShellPageKeys.Downloads : ShellPageKeys.About;
                SelfCheckAssert.True(shell.ShowPage(other), "ShowPage('" + other + "') returned false");
                SelfCheckAssert.Equal(other, shell.Pages.ActiveKey, "active key after switching away");
                SelfCheckAssert.True(!object.ReferenceEquals(page.Parent, shell.Pages), "the page stayed in the host after switching away");

                SelfCheckAssert.True(shell.ShowPage(spec.Key), "second ShowPage('" + spec.Key + "') returned false");
                AssertShown(shell, spec.Key, page, "show again");
            }
        }

        private static void UnknownKey()
        {
            using (ShellForm shell = new ShellForm())
            {
                SelfCheckAssert.True(shell.ShowPage(ShellPageKeys.About), "ShowPage(About)");
                SelfCheckAssert.True(!shell.ShowPage("no-such-page"), "an unknown key should return false");
                SelfCheckAssert.True(!shell.ShowPage(null), "a null key should return false");
                SelfCheckAssert.Equal(ShellPageKeys.About, shell.Pages.ActiveKey, "an unknown key must not change the active page");
            }
        }

        // All fourteen pages in one shell: forward through the list, then back through it.
        private static void SwitchCycle()
        {
            using (ShellForm shell = new ShellForm())
            {
                Dictionary<string, Control> pages = new Dictionary<string, Control>();
                foreach (PageSpec spec in Specs)
                {
                    pages[spec.Key] = Register(shell, spec);
                }

                for (int i = 0; i < Specs.Length; i++)
                {
                    Visit(shell, pages, Specs[i], "forward " + Specs[i].Label);
                }

                for (int i = Specs.Length - 1; i >= 0; i--)
                {
                    Visit(shell, pages, Specs[i], "backward " + Specs[i].Label);
                }
            }
        }

        private static void Visit(ShellForm shell, Dictionary<string, Control> pages, PageSpec spec, string step)
        {
            SelfCheckAssert.True(shell.ShowPage(spec.Key), step + ": ShowPage returned false");
            AssertShown(shell, spec.Key, pages[spec.Key], step);

            foreach (KeyValuePair<string, Control> other in pages)
            {
                if (other.Key != spec.Key)
                {
                    SelfCheckAssert.True(!object.ReferenceEquals(other.Value.Parent, shell.Pages), step + ": '" + other.Key + "' is still a child of the page host");
                }
            }
        }
    }
}
#endif
