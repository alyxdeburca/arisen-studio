using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ArisenStudio.Io;
using ArisenStudio.Runtime;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// The <c>--page &lt;key&gt;</c> switch. Opens the shell in smoke mode (temp settings folder, no database load, no
    /// network), selects one page with <see cref="ShellForm.ShowPage" />, lays it out, and saves the whole shell as
    /// <c>&lt;capture folder&gt;/&lt;key&gt;.png</c>. Same checks as <see cref="PageCaptureHarness" /> for one page: the
    /// page must be registered, must not be a <see cref="PlaceholderPage" />, and is reported with its zero-size controls.
    /// <para>
    /// Prints <c>PAGE-CAPTURE-OK &lt;key&gt;</c> (exit 0) or <c>PAGE-CAPTURE-FAIL &lt;key&gt; &lt;reason&gt;</c> (exit 1).
    /// A zero-size control does not fail the capture: it prints <c>PAGE-CAPTURE-WARN</c> first, then OK.
    /// </para>
    /// <para>
    /// If the page holds a <c>TabControl</c> (PageTabs wraps one, and the page's first one is used), each tab is selected in
    /// turn and saved: tab 0 as <c>&lt;key&gt;.png</c>, the others as <c>&lt;key&gt;-&lt;tab&gt;.png</c>. Prints one
    /// <c>PAGE-CAPTURE-OK &lt;key&gt; &lt;tab&gt;</c> per tab, or <c>PAGE-CAPTURE-FAIL &lt;key&gt; &lt;tab&gt; &lt;reason&gt;</c> for a
    /// tab that fails; the other tabs still run and the exit code is 1 if any tab failed.
    /// </para>
    /// UI thread only. Nothing here was compiled or run.
    /// </summary>
    internal static class PageCaptureRunner
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 800;

        /// <summary>Captures one page and returns the process exit code: 0 on success, 1 on failure.</summary>
        /// <param name="key">A ShellPageKeys value. Anything else is rejected before the shell is created.</param>
        /// <param name="captureDirectory">The <c>--capture</c> folder. Created if missing. Must not be under UserFolders.AppData.</param>
        public static int Run(string key, string captureDirectory)
        {
            if (!PageCaptureHarness.PageKeys().Contains(key))
            {
                return Fail(key, "unknown page");
            }
            if (string.IsNullOrEmpty(captureDirectory))
            {
                return Fail(key, "missing --capture folder");
            }

            string target;
            try
            {
                target = Path.GetFullPath(captureDirectory);
            }
            catch (Exception ex)
            {
                return Fail(key, "bad --capture folder: " + ex.Message);
            }
            // The same guard as PageCaptureHarness.Run: never write into the real user folders.
            if (IsUnder(target, UserFolders.AppData))
            {
                return Fail(key, "the --capture folder is inside the real user folders");
            }
            try
            {
                _ = Directory.CreateDirectory(target);
            }
            catch (Exception ex)
            {
                return Fail(key, "cannot create the --capture folder: " + ex.Message);
            }

            int exitCode = 1;
            bool ran = false;
            try
            {
                using (ShellForm shell = new ShellForm())
                {
                    // Smoke attach: settings go to a temp folder, nothing is downloaded, no single-instance guard.
                    _ = FullShellIntegration.Attach(shell, true);
                    shell.Shown += (s, e) =>
                    {
                        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 1500 };
                        t.Tick += (s2, e2) =>
                        {
                            t.Stop();
                            ran = true;
                            exitCode = CaptureOne(shell, key, target);
                            shell.Close();
                        };
                        t.Start();
                    };
                    Application.Run(shell);
                }
            }
            catch (Exception ex)
            {
                return Fail(key, ex.GetType().Name + ": " + ex.Message);
            }

            if (!ran)
            {
                return Fail(key, "the shell never showed");
            }
            return exitCode;
        }

        private static int CaptureOne(ShellForm shell, string key, string target)
        {
            try
            {
                if (!shell.Pages.HasPage(key))
                {
                    return Fail(key, "no page is registered under this key");
                }

                shell.ClientSize = new Size(CaptureWidth, CaptureHeight);
                if (!shell.ShowPage(key))
                {
                    return Fail(key, "ShellForm.ShowPage returned false");
                }
                if (shell.Pages.ActiveKey != key)
                {
                    return Fail(key, "the active page key is '" + shell.Pages.ActiveKey + "' after ShowPage");
                }

                shell.PerformLayout();
                Pump(shell);

                Control page = ActivePage(shell);
                if (page == null)
                {
                    return Fail(key, "the page host has no child control after ShowPage");
                }
                if (page is PlaceholderPage)
                {
                    return Fail(key, "still a placeholder page (its real page is not attached)");
                }

                List<string> emptyControls = new List<string>();
                CheckSizes(page, Describe(page), emptyControls);
                if (emptyControls.Count > 0)
                {
                    Console.WriteLine("PAGE-CAPTURE-WARN " + key + " " + emptyControls.Count + " control(s) with no size: " + string.Join("; ", emptyControls));
                }

                TabControl tabs = FindTabControl(page);
                if (tabs != null && tabs.TabPages.Count > 0)
                {
                    return CaptureTabs(shell, key, target, tabs);
                }

                string problem = SaveShell(shell, Path.Combine(target, key + ".png"));
                if (problem != null)
                {
                    return Fail(key, problem);
                }

                Console.WriteLine("PAGE-CAPTURE-OK " + key);
                return 0;
            }
            catch (Exception ex)
            {
                return Fail(key, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static int Fail(string key, string reason)
        {
            Console.WriteLine("PAGE-CAPTURE-FAIL " + key + " " + reason);
            return 1;
        }

        // The first TabControl under the control, depth first. The walk does not enter a TabControl.
        private static TabControl FindTabControl(Control control)
        {
            foreach (Control child in control.Controls)
            {
                TabControl tabs = child as TabControl;
                if (tabs != null)
                {
                    return tabs;
                }
                TabControl inner = FindTabControl(child);
                if (inner != null)
                {
                    return inner;
                }
            }
            return null;
        }

        // One image per tab. Tab 0 is <key>.png; the others are <key>-<tab>.png. A failing tab does not stop the rest.
        private static int CaptureTabs(ShellForm shell, string key, string target, TabControl tabs)
        {
            int exitCode = 0;
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                string baseName = SafeTabName(tabs.TabPages[i].Text, i);
                string name = baseName;
                for (int n = 2; !used.Add(name); n++)
                {
                    name = baseName + "-" + n;
                }
                string fileName = (i == 0 ? key : key + "-" + name) + ".png";

                string problem;
                try
                {
                    tabs.SelectedIndex = i;
                    shell.PerformLayout();
                    Pump(shell);
                    problem = CheckTabContent(key, name, tabs, i);
                    if (problem == null)
                    {
                        problem = SaveShell(shell, Path.Combine(target, fileName), tabs.TabPages[i]);
                    }
                }
                catch (Exception ex)
                {
                    problem = ex.GetType().Name + ": " + ex.Message;
                }

                if (problem == null)
                {
                    Console.WriteLine("PAGE-CAPTURE-OK " + key + " " + name);
                }
                else
                {
                    exitCode = 1;
                    Console.WriteLine("PAGE-CAPTURE-FAIL " + key + " " + name + " " + problem);
                }
            }
            return exitCode;
        }

        // Content check for the selected tab (b102). The page must hold controls, and a control whose text says Discord
        // must appear only under the Discord tab (the settings page). Prints one PAGE-CAPTURE-CONTENT line per tab.
        // Returns null when the content is right, otherwise the reason.
        private static string CheckTabContent(string key, string tabName, TabControl tabs, int index)
        {
            TabPage page = tabs.TabPages[index];
            int count = 0;
            int discord = 0;
            CountContent(page, ref count, ref discord);
            bool isDiscordTab = string.Equals(tabs.TabPages[index].Text, "Discord", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine("PAGE-CAPTURE-CONTENT " + key + " " + tabName + " selected=" + (tabs.SelectedIndex == index ? "yes" : "no")
                + " controls=" + count + " discord=" + discord);
            if (tabs.SelectedIndex != index)
            {
                return "the tab did not become selected";
            }
            if (count == 0)
            {
                return "the tab page has no controls";
            }
            string clipped = FindClipped(page);
            if (clipped != null)
            {
                Console.WriteLine("PAGE-CAPTURE-CLIPPED " + key + " " + tabName + " " + clipped);
                return "a control is cut off by its group box: " + clipped;
            }
            if (key == ShellPageKeys.Settings && !isDiscordTab && discord > 0)
            {
                return "a Discord control appears under the " + tabName + " tab";
            }
            if (key == ShellPageKeys.Settings && isDiscordTab && discord == 0)
            {
                return "the Discord tab has no Discord control";
            }
            return null;
        }

        // The first visible control whose bottom edge lies below the display area of the GroupBox that holds it (null: none).
        // This measures the live layout, so it tells a layout fault from a painting fault.
        private static string FindClipped(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                GroupBox group = child as GroupBox;
                if (group != null && group.Visible)
                {
                    string found = FindBelow(group, group);
                    if (found != null)
                    {
                        return found;
                    }
                }
                string deeper = FindClipped(child);
                if (deeper != null)
                {
                    return deeper;
                }
            }
            return null;
        }

        private static string FindBelow(GroupBox group, Control parent)
        {
            int limit = group.DisplayRectangle.Bottom + 1;
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible)
                {
                    continue;
                }
                int bottom = group.PointToClient(child.PointToScreen(new Point(0, child.Height))).Y;
                if (bottom > limit)
                {
                    return "'" + (child.Text ?? string.Empty) + "' (" + child.GetType().Name + ") ends at " + bottom + ", group '" + group.Text + "' shows to " + limit
                        + " (group " + group.Size.Width + "x" + group.Size.Height + ", display " + group.DisplayRectangle
                        + ", child bounds " + child.Bounds + ", parent " + parent.GetType().Name + " " + parent.Bounds + ")";
                }
                string deeper = FindBelow(group, child);
                if (deeper != null)
                {
                    return deeper;
                }
            }
            return null;
        }

        private static void CountContent(Control parent, ref int count, ref int discord)
        {
            foreach (Control child in parent.Controls)
            {
                count++;
                if (child.Text != null && child.Text.IndexOf("Discord", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    discord++;
                }
                CountContent(child, ref count, ref discord);
            }
        }

        // ASCII letters, digits and dashes only. Any other character becomes a dash; an empty result falls back to the tab number.
        private static string SafeTabName(string text, int index)
        {
            char[] chars = (text ?? string.Empty).ToCharArray();
            for (int c = 0; c < chars.Length; c++)
            {
                bool keep = (chars[c] >= 'a' && chars[c] <= 'z') || (chars[c] >= 'A' && chars[c] <= 'Z') || (chars[c] >= '0' && chars[c] <= '9');
                if (!keep)
                {
                    chars[c] = '-';
                }
            }
            string result = new string(chars).Trim('-');
            return result.Length == 0 ? "tab" + (index + 1) : result;
        }

        // Draws the whole shell into a PNG. Returns null on success, otherwise the reason.
        // overlay: a tab page to paint again, by itself, over the shell image. Shell.DrawToBitmap on Mono paints the same tab
        // page (the last one) whichever tab is selected, although the selected page holds the right controls (b102 content check).
        private static string SaveShell(ShellForm shell, string file, TabPage overlay = null)
        {
            int width = shell.Width;
            int height = shell.Height;
            if (width <= 0 || height <= 0)
            {
                return "the shell has no size (" + width + "x" + height + ")";
            }

            using (Bitmap bitmap = new Bitmap(width, height))
            {
                shell.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
                if (overlay != null && overlay.Width > 0 && overlay.Height > 0)
                {
                    Point at = shell.PointToClient(overlay.PointToScreen(Point.Empty));
                    using (Bitmap pageImage = new Bitmap(overlay.Width, overlay.Height))
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        overlay.DrawToBitmap(pageImage, new Rectangle(0, 0, overlay.Width, overlay.Height));
                        Color back = overlay.BackColor.A == 0 ? SystemColors.Control : overlay.BackColor;
                        using (SolidBrush brush = new SolidBrush(back))
                        {
                            g.FillRectangle(brush, new Rectangle(at, overlay.Size));
                        }
                        g.DrawImage(pageImage, at);
                    }
                }
                bitmap.Save(file, ImageFormat.Png);
            }
            if (new FileInfo(file).Length <= 0)
            {
                return "the PNG is empty: " + file;
            }
            return null;
        }

        // The only child of the page host is the active page (PageHost keeps the others out of Controls).
        private static Control ActivePage(ShellForm shell)
        {
            PageHost host = shell.Pages;
            return host.Controls.Count == 0 ? null : host.Controls[host.Controls.Count - 1];
        }

        // Visible controls only, as PageCaptureHarness does.
        private static void CheckSizes(Control control, string path, IList<string> problems)
        {
            if (control.Width <= 0 || control.Height <= 0)
            {
                problems.Add(path + " (" + control.Width + "x" + control.Height + ")");
            }

            foreach (Control child in control.Controls)
            {
                if (child.Visible)
                {
                    CheckSizes(child, path + "/" + Describe(child), problems);
                }
            }
        }

        private static string Describe(Control control)
        {
            string type = control.GetType().Name;
            return string.IsNullOrEmpty(control.Name) ? type : control.Name + ":" + type;
        }

        private static void Pump(Form form)
        {
            // Let the layout and the first paint of the page run (OnPageShown may load data on the UI thread).
            for (int i = 0; i < 3; i++)
            {
                Application.DoEvents();
            }
            form.PerformLayout();
        }

        private static bool IsUnder(string path, string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}
