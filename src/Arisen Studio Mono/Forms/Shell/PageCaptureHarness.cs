using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using ArisenStudio.Io;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// What <see cref="PageCaptureHarness.Run" /> found for one page key.
    /// </summary>
    internal sealed class PageCaptureResult
    {
        private readonly List<string> _emptyControls = new List<string>();

        public PageCaptureResult(string key)
        {
            Key = key;
        }

        /// <summary>The ShellPageKeys value.</summary>
        public string Key { get; private set; }

        /// <summary>True when the page was shown and the PNG was written.</summary>
        public bool Captured { get; internal set; }

        /// <summary>Full path of the PNG, or null when nothing was written.</summary>
        public string FilePath { get; internal set; }

        /// <summary>True when the page registered under the key is still a PlaceholderPage (its real page was not attached).</summary>
        public bool IsPlaceholder { get; internal set; }

        /// <summary>Why the page could not be captured (not registered, ShowPage false, an exception). Null when captured.</summary>
        public string Error { get; internal set; }

        /// <summary>The key the shell showed after ShowPage. Differs from Key when the navigation redirected it (PS4 Game Mods shows Homebrew). Null when not reached.</summary>
        public string ResolvedKey { get; internal set; }

        /// <summary>Visible descendant controls of the page with a width or height of zero or less, as "path (WxH)".</summary>
        public IList<string> EmptyControls
        {
            get { return _emptyControls; }
        }

        /// <summary>Captured, not a placeholder, and no empty control.</summary>
        public bool Passed
        {
            get { return Captured && !IsPlaceholder && _emptyControls.Count == 0; }
        }
    }

    /// <summary>
    /// Shows a wired <see cref="ShellForm" /> on each registered page in turn at 1280x800, checks the page for visible
    /// descendant controls with a non-positive width or height, and saves a DrawToBitmap capture as
    /// <c>&lt;targetDirectory&gt;/&lt;pageKey&gt;.png</c>. Page keys are the ShellPageKeys constants (PageHost has no key list,
    /// so each constant is tried with PageHost.HasPage). UI thread only. Nothing here was compiled or run.
    /// <para>
    /// Safety: the harness itself writes only to the target directory, which must not be inside UserFolders.AppData (the
    /// real user folders); it opens no modal dialog and no other form besides the shell (shown non-modally, hidden again
    /// afterwards if the harness showed it). It cannot stop a page from doing its own work in OnPageShown, so the caller
    /// should attach the shell with ShellServices.LoadSettingsOnAttach = false and a ShellServices.SettingsPath inside a
    /// temp folder, so that nothing reads or writes the real settings.
    /// </para>
    /// </summary>
    internal sealed class PageCaptureHarness
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 800;

        /// <summary>
        /// Captures every page. Returns one result per ShellPageKeys constant, in declaration order. A page that cannot be
        /// captured gets a result with Captured false and the reason in Error; one failing page does not stop the others.
        /// </summary>
        /// <param name="shell">A shell the ShellController is already attached to (or a bare one; placeholders are then flagged).</param>
        /// <param name="targetDirectory">Where the PNG files go. Created if missing. Must not be under UserFolders.AppData.</param>
        /// <param name="capturePageOnly">False (default) captures the whole shell window; true captures only the page control.</param>
        public static IList<PageCaptureResult> Run(ShellForm shell, string targetDirectory, bool capturePageOnly = false)
        {
            if (shell == null)
            {
                throw new ArgumentNullException("shell");
            }
            if (string.IsNullOrEmpty(targetDirectory))
            {
                throw new ArgumentException("A target directory is required.", "targetDirectory");
            }

            string target = Path.GetFullPath(targetDirectory);
            if (IsUnder(target, UserFolders.AppData))
            {
                throw new ArgumentException("The target directory is inside the real user folders: " + UserFolders.AppData, "targetDirectory");
            }
            _ = Directory.CreateDirectory(target);

            List<PageCaptureResult> results = new List<PageCaptureResult>();
            bool showedShell = false;
            try
            {
                shell.ClientSize = new Size(CaptureWidth, CaptureHeight);
                if (!shell.Visible)
                {
                    shell.Show();
                    showedShell = true;
                }
                Pump(shell);

                foreach (string key in PageKeys())
                {
                    results.Add(CaptureOne(shell, key, target, capturePageOnly));
                }
            }
            finally
            {
                if (showedShell && !shell.IsDisposed)
                {
                    shell.Hide();
                }
            }
            return results;
        }

        /// <summary>One line per page, then a total line. For a console or a result file.</summary>
        public static string Summarize(IList<PageCaptureResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException("results");
            }

            StringBuilder text = new StringBuilder();
            int passed = 0;
            foreach (PageCaptureResult r in results)
            {
                string state = r.Passed ? "PASS" : (r.Captured ? "WARN" : "FAIL");
                if (r.Passed)
                {
                    passed++;
                }
                text.Append(state).Append(' ').Append(r.Key);
                if (r.ResolvedKey != null && r.ResolvedKey != r.Key)
                {
                    text.Append(" resolved=").Append(r.ResolvedKey);
                }
                if (r.Error != null)
                {
                    text.Append(": ").Append(r.Error);
                }
                if (r.IsPlaceholder)
                {
                    text.Append(": still a placeholder page");
                }
                if (r.EmptyControls.Count > 0)
                {
                    text.Append(": ").Append(r.EmptyControls.Count).Append(" control(s) with no size: ").Append(string.Join("; ", r.EmptyControls));
                }
                text.AppendLine();
            }
            text.Append(passed).Append(" of ").Append(results.Count).AppendLine(" pages passed.");
            return text.ToString();
        }

        /// <summary>The page keys the harness walks: every public string constant of ShellPageKeys, in declaration order.</summary>
        public static IList<string> PageKeys()
        {
            List<string> keys = new List<string>();
            foreach (FieldInfo field in typeof(ShellPageKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    keys.Add((string)field.GetRawConstantValue());
                }
            }
            return keys;
        }

        private static PageCaptureResult CaptureOne(ShellForm shell, string key, string target, bool pageOnly)
        {
            PageCaptureResult result = new PageCaptureResult(key);
            try
            {
                if (!shell.Pages.HasPage(key))
                {
                    result.Error = "no page is registered under this key";
                    return result;
                }
                if (!shell.ShowPage(key))
                {
                    result.Error = "ShellForm.ShowPage returned false";
                    return result;
                }
                string resolved;
                string keyProblem = CheckActiveKey(shell, key, out resolved);
                if (keyProblem != null)
                {
                    result.Error = keyProblem;
                    return result;
                }
                result.ResolvedKey = resolved;

                shell.ClientSize = new Size(CaptureWidth, CaptureHeight);
                shell.PerformLayout();
                Pump(shell);

                Control page = ActivePage(shell);
                if (page == null)
                {
                    result.Error = "the page host has no child control after ShowPage";
                    return result;
                }
                result.IsPlaceholder = page is PlaceholderPage;

                CheckSizes(page, Describe(page), result.EmptyControls);

                Control source = pageOnly ? page : shell;
                int width = source.Width;
                int height = source.Height;
                if (width <= 0 || height <= 0)
                {
                    result.Error = "the " + (pageOnly ? "page" : "shell") + " has no size (" + width + "x" + height + ")";
                    return result;
                }

                string file = Path.Combine(target, SafeFileName(key) + ".png");
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    source.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
                    bitmap.Save(file, ImageFormat.Png);
                }
                result.FilePath = file;
                result.Captured = true;
            }
            catch (Exception ex)
            {
                result.Captured = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Accepts the page the navigation showed after ShowPage(key): the requested key, or the key that
        /// <see cref="ShellForm.ResolvePageKey" /> maps it to (PS4 Game Mods shows Homebrew). Any other active key fails.
        /// Returns null when accepted (resolvedKey is then the active key), otherwise the reason.
        /// </summary>
        internal static string CheckActiveKey(ShellForm shell, string key, out string resolvedKey)
        {
            string active = shell.Pages.ActiveKey;
            resolvedKey = active;
            if (active == key)
            {
                return null;
            }

            string expected = shell.ResolvePageKey == null ? key : shell.ResolvePageKey(key);
            if (expected != null && active == expected && shell.Pages.HasPage(expected))
            {
                return null;
            }
            return "the active page key is '" + active + "' after ShowPage (expected '" + key + "'"
                + (expected != null && expected != key ? " or its redirect '" + expected + "'" : string.Empty) + ")";
        }

        // The only child of the page host is the active page (PageHost keeps the others out of Controls).
        private static Control ActivePage(ShellForm shell)
        {
            PageHost host = shell.Pages;
            return host.Controls.Count == 0 ? null : host.Controls[host.Controls.Count - 1];
        }

        // Visible controls only: a hidden tab page or panel may legitimately have no size. Hidden controls are not entered.
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

        private static string SafeFileName(string key)
        {
            StringBuilder name = new StringBuilder();
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in key)
            {
                name.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return name.ToString();
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
