using ArisenStudio.Extensions;
using ArisenStudio.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools
{
    /// <summary>
    /// Small helpers shared by the stock WinForms PS3 and Xbox tool forms (the ones ported from the old DevExpress tool forms).
    /// Internal on purpose: the forms are the public surface. Nothing here talks to a console.
    /// Lives in the PS3\New folder but is used by both platforms (namespace ArisenStudio.Forms.Tools).
    /// </summary>
    internal static class ToolFormSupport
    {
        /// <summary>
        /// Localised text for a key, or the English fallback when there is no language source or no value for the key.
        /// </summary>
        public static string Lookup(ResourceManager language, string key, string fallback)
        {
            if (language == null)
            {
                return fallback;
            }

            try
            {
                string value = language.GetString(key);
                return string.IsNullOrEmpty(value) ? fallback : value;
            }
            catch (MissingManifestResourceException)
            {
                return fallback;
            }
        }

        /// <summary>
        /// Send an error to the host logger (AppHost.Logger). A no-op until the host assigns one.
        /// </summary>
        public static void LogError(Exception exception, string message)
        {
            Action<Exception, string> logger = AppHost.Logger;

            if (logger != null)
            {
                logger(exception, message);
            }
        }

        /// <summary>
        /// Stock look shared by the tool forms: message box font, no icon, not in the taskbar, centred on the owner.
        /// </summary>
        public static void ConfigureForm(Form form, bool sizable)
        {
            form.Font = SystemFonts.MessageBoxFont;
            form.FormBorderStyle = sizable ? FormBorderStyle.Sizable : FormBorderStyle.FixedDialog;
            form.MaximizeBox = sizable;
            form.MinimizeBox = false;
            form.ShowIcon = false;
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.CenterParent;

            if (!sizable)
            {
                form.SizeGripStyle = SizeGripStyle.Hide;
            }
        }

        public static int Pad(Control control)
        {
            return control.Font.Height / 2;
        }

        public static Label NewLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Text = text ?? string.Empty,
                Margin = new Padding(0, 3, 6, 3)
            };
        }

        /// <summary>
        /// A button that grows with its text but is at least <paramref name="widthUnits" /> font heights wide.
        /// </summary>
        public static Button NewButton(Control owner, int widthUnits)
        {
            return new Button
            {
                AutoSize = true,
                MinimumSize = new Size(owner.Font.Height * widthUnits, 0),
                Margin = new Padding(0, 0, Pad(owner), 0),
                UseMnemonic = false
            };
        }

        /// <summary>
        /// A row of controls placed left to right (wraps when the form is narrow).
        /// </summary>
        public static FlowLayoutPanel FlowRow(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = Padding.Empty
            };

            foreach (Control control in controls)
            {
                row.Controls.Add(control);
            }

            return row;
        }

        /// <summary>
        /// A table with one auto-sized column per entry of <paramref name="columnStyles" /> and no rows yet. Use AddRow / AddFullRow to fill it.
        /// </summary>
        public static TableLayoutPanel NewTable(params ColumnStyle[] columnStyles)
        {
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = columnStyles.Length,
                Margin = Padding.Empty
            };

            foreach (ColumnStyle style in columnStyles)
            {
                table.ColumnStyles.Add(style);
            }

            return table;
        }

        /// <summary>
        /// Add an auto-height row with a control in each of the first two columns (either may be null).
        /// </summary>
        public static int AddRow(TableLayoutPanel table, Control first, Control second)
        {
            int row = table.RowStyles.Count;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowCount = row + 1;

            if (first != null)
            {
                table.Controls.Add(first, 0, row);
            }

            if (second != null)
            {
                table.Controls.Add(second, 1, row);
            }

            return row;
        }

        /// <summary>
        /// Add a row whose single control spans every column. A percent height fills the leftover space (use it for lists).
        /// </summary>
        public static int AddFullRow(TableLayoutPanel table, Control control, bool fill)
        {
            int row = table.RowStyles.Count;
            table.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100F) : new RowStyle(SizeType.AutoSize));
            table.RowCount = row + 1;
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, Math.Max(1, table.ColumnCount));
            return row;
        }

        /// <summary>
        /// Add a status bar at the bottom of the form and return its label. Add the form's main (docked Fill) control BEFORE calling this,
        /// so the bar takes the bottom edge first.
        /// </summary>
        public static ToolStripStatusLabel AddStatusBar(Form form)
        {
            StatusStrip strip = new StatusStrip();
            ToolStripStatusLabel label = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            strip.Items.Add(label);
            form.Controls.Add(strip);
            return label;
        }

        /// <summary>
        /// Set a status label from any thread.
        /// </summary>
        public static void SetStatusText(Form form, ToolStripStatusLabel label, string text)
        {
            if (form.IsDisposed || label == null)
            {
                return;
            }

            if (form.InvokeRequired)
            {
                form.BeginInvoke((MethodInvoker)delegate { label.Text = text; });
            }
            else
            {
                label.Text = text;
            }
        }

        /// <summary>
        /// Opt-in non-blocking mode for headless or smoke runs. When true, <see cref="ShowError" /> writes a
        /// "TOOL-NOTICE" line to the console instead of showing a modal MessageBox, and
        /// <see cref="ShowErrorAndClose" /> leaves the form open. Default false: behaviour is unchanged.
        /// </summary>
        public static bool NonBlockingErrors { get; set; }

        public static void ShowError(IWin32Window owner, string title, string message)
        {
            if (NonBlockingErrors)
            {
                Console.WriteLine("TOOL-NOTICE: " + title + ": " + message);
                return;
            }

            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Show an error and close the form (the load-failure path). In non-blocking mode the form is not closed.
        /// </summary>
        public static void ShowErrorAndClose(Form form, string title, string message)
        {
            ShowError(form, title, message);

            if (!NonBlockingErrors)
            {
                form.Close();
            }
        }

        public static void ShowInfo(IWin32Window owner, string title, string message)
        {
            MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static bool Confirm(IWin32Window owner, string title, string message)
        {
            return MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        /// <summary>
        /// File size text: "1.5 MB" style when <paramref name="formatted" /> is true, otherwise "1572864 bytes".
        /// </summary>
        public static string FormatBytes(long bytes, bool formatted, string bytesWord)
        {
            if (!formatted)
            {
                return bytes.ToString(CultureInfo.CurrentCulture) + " " + bytesWord;
            }

            string[] units = { "bytes", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0
                ? bytes.ToString(CultureInfo.CurrentCulture) + " bytes"
                : value.ToString("0.##", CultureInfo.CurrentCulture) + " " + units[unit];
        }

        /// <summary>
        /// Date text: "5 minutes ago" style when <paramref name="relative" /> is true, otherwise MM/dd/yyyy.
        /// (A plain stand-in for the Humanizer package call the old form used.)
        /// </summary>
        public static string FormatDate(DateTime value, bool relative)
        {
            if (!relative)
            {
                return value.ToString("MM/dd/yyyy", CultureInfo.CurrentCulture);
            }

            TimeSpan span = DateTime.Now - value;

            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero;
            }

            if (span.TotalMinutes < 1)
            {
                return "just now";
            }

            if (span.TotalHours < 1)
            {
                return Plural((int)span.TotalMinutes, "minute") + " ago";
            }

            if (span.TotalDays < 1)
            {
                return Plural((int)span.TotalHours, "hour") + " ago";
            }

            if (span.TotalDays < 30)
            {
                return Plural((int)span.TotalDays, "day") + " ago";
            }

            if (span.TotalDays < 365)
            {
                return Plural((int)(span.TotalDays / 30), "month") + " ago";
            }

            return Plural((int)(span.TotalDays / 365), "year") + " ago";
        }

        private static string Plural(int count, string unit)
        {
            return count == 1 ? "1 " + unit : count.ToString(CultureInfo.CurrentCulture) + " " + unit + "s";
        }

        /// <summary>
        /// Remove characters that are not allowed in a file name, and trim spaces and dots from both ends.
        /// </summary>
        public static string RemoveInvalidFileNameChars(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);

            foreach (char c in name)
            {
                if (Array.IndexOf(invalid, c) < 0)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Trim(' ', '.');
        }

        /// <summary>
        /// Hex text ("0A1B...") to bytes. Null when the text is empty, has an odd length or has a character that is not a hex digit.
        /// </summary>
        public static byte[] TryHexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
            {
                return null;
            }

            byte[] result = new byte[hex.Length / 2];

            for (int i = 0; i < result.Length; i++)
            {
                char high = hex[i * 2];
                char low = hex[i * 2 + 1];

                if (!Uri.IsHexDigit(high) || !Uri.IsHexDigit(low))
                {
                    return null;
                }

                result[i] = (byte)((Uri.FromHex(high) << 4) | Uri.FromHex(low));
            }

            return result;
        }

        /// <summary>
        /// Show a file in the system file manager (Explorer on Windows, the folder in the default file manager elsewhere).
        /// Does nothing when the file is gone.
        /// </summary>
        public static void RevealFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
                }
                else
                {
                    Process.Start(new ProcessStartInfo(Path.GetDirectoryName(filePath)) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "Unable to open the folder of " + filePath);
            }
        }

        /// <summary>
        /// Open an http or https link in the default browser. Other schemes are refused. Returns false when it was not opened.
        /// </summary>
        public static bool OpenUrl(string url)
        {
            Uri uri;

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "Unable to open " + url);
                return false;
            }
        }

        /// <summary>
        /// Small modal box with one text field. Returns the text, or null when cancelled.
        /// (Stands in for the old DialogExtensions.ShowTextInputDialog.)
        /// </summary>
        public static string PromptText(IWin32Window owner, string title, string label, string initial)
        {
            using (Form dialog = new Form())
            {
                ConfigureForm(dialog, false);
                dialog.Text = title;

                int h = dialog.Font.Height;
                int pad = h / 2;

                Label prompt = NewLabel(label);
                TextBox box = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Text = initial ?? string.Empty,
                    Margin = new Padding(0, 0, 0, pad)
                };
                Button ok = NewButton(dialog, 6);
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                Button cancel = NewButton(dialog, 6);
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Margin = Padding.Empty;

                TableLayoutPanel root = NewTable(new ColumnStyle(SizeType.Percent, 100F));
                root.Padding = new Padding(pad);
                root.AutoSize = false;
                AddFullRow(root, prompt, false);
                AddFullRow(root, box, false);
                AddFullRow(root, FlowRow(ok, cancel), false);

                dialog.Controls.Add(root);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                dialog.ClientSize = new Size(h * 28, h * 9);

                return dialog.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
            }
        }

        /// <summary>
        /// Small modal list. Shows <see cref="ListItem.Name" /> and returns the chosen item, or null when cancelled.
        /// (Stands in for the old DialogExtensions.ShowListViewDialog.)
        /// </summary>
        public static ListItem PickFromList(IWin32Window owner, string title, IList<ListItem> items)
        {
            using (Form dialog = new Form())
            {
                ConfigureForm(dialog, true);
                dialog.Text = title;
                dialog.MinimizeBox = false;

                int h = dialog.Font.Height;
                int pad = h / 2;

                ListBox list = new ListBox
                {
                    Dock = DockStyle.Fill,
                    DisplayMember = "Name",
                    IntegralHeight = false,
                    Margin = new Padding(0, 0, 0, pad)
                };

                foreach (ListItem item in items)
                {
                    list.Items.Add(item);
                }

                if (list.Items.Count > 0)
                {
                    list.SelectedIndex = 0;
                }

                Button ok = NewButton(dialog, 6);
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                Button cancel = NewButton(dialog, 6);
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Margin = Padding.Empty;

                list.DoubleClick += delegate
                {
                    if (list.SelectedItem != null)
                    {
                        dialog.DialogResult = DialogResult.OK;
                    }
                };

                TableLayoutPanel root = NewTable(new ColumnStyle(SizeType.Percent, 100F));
                root.Padding = new Padding(pad);
                root.AutoSize = false;
                AddFullRow(root, list, true);
                AddFullRow(root, FlowRow(ok, cancel), false);

                dialog.Controls.Add(root);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                dialog.ClientSize = new Size(h * 30, h * 16);

                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    return list.SelectedItem as ListItem;
                }

                return null;
            }
        }
    }

    /// <summary>
    /// A row of mutually exclusive radio buttons (stands in for the DevExpress RadioGroup). SelectedIndex is -1 when none is checked.
    /// </summary>
    internal sealed class RadioGroupPanel : FlowLayoutPanel
    {
        private readonly List<RadioButton> _buttons = new List<RadioButton>();

        public RadioGroupPanel(string[] captions, int selectedIndex)
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FlowDirection = FlowDirection.LeftToRight;
            WrapContents = true;
            Margin = Padding.Empty;

            for (int i = 0; i < captions.Length; i++)
            {
                RadioButton button = new RadioButton
                {
                    AutoSize = true,
                    UseMnemonic = false,
                    Text = captions[i],
                    Margin = new Padding(0, 0, 12, 0)
                };
                _buttons.Add(button);
                Controls.Add(button);
            }

            SelectedIndex = selectedIndex;
        }

        public int SelectedIndex
        {
            get
            {
                for (int i = 0; i < _buttons.Count; i++)
                {
                    if (_buttons[i].Checked)
                    {
                        return i;
                    }
                }

                return -1;
            }
            set
            {
                for (int i = 0; i < _buttons.Count; i++)
                {
                    _buttons[i].Checked = i == value;
                }
            }
        }
    }
}
