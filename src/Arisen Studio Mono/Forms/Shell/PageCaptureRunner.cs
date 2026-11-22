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

                int width = shell.Width;
                int height = shell.Height;
                if (width <= 0 || height <= 0)
                {
                    return Fail(key, "the shell has no size (" + width + "x" + height + ")");
                }

                string file = Path.Combine(target, key + ".png");
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    shell.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
                    bitmap.Save(file, ImageFormat.Png);
                }
                if (new FileInfo(file).Length <= 0)
                {
                    return Fail(key, "the PNG is empty: " + file);
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
