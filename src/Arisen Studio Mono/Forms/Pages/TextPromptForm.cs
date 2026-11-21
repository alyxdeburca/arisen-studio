using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Pages
{
    /// <summary>
    /// A one-line text input box (the old DialogExtensions.ShowTextInputDialog): used by the file manager for
    /// rename and new folder. Show returns the entered text, or null when the user cancelled.
    /// </summary>
    internal sealed class TextPromptForm : Form
    {
        private readonly TextBox textBox = new TextBox();

        private TextPromptForm(string title, string prompt, string initialText)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(380, 110);

            Label label = new Label();
            label.Text = prompt;
            label.AutoSize = false;
            label.SetBounds(12, 12, 356, 20);

            textBox.SetBounds(12, 36, 356, 24);
            textBox.Text = initialText ?? string.Empty;

            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(212, 72, 75, 26);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(293, 72, 75, 26);

            Controls.Add(label);
            Controls.Add(textBox);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            textBox.Focus();
            textBox.SelectAll();
        }

        public static string Show(IWin32Window owner, string title, string prompt, string initialText)
        {
            using (TextPromptForm form = new TextPromptForm(title, prompt, initialText))
            {
                DialogResult result = owner != null ? form.ShowDialog(owner) : form.ShowDialog();
                return result == DialogResult.OK ? form.textBox.Text : null;
            }
        }
    }
}
