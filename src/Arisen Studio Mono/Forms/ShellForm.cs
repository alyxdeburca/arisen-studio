using System;
using System.Drawing;
using System.Windows.Forms;
using ArisenStudio.Forms.Dialogs;

namespace ArisenStudio.Shell
{
    internal sealed class ShellForm : Form
    {
        public ShellForm()
        {
            Text = "Arisen Studio (Mono milestone 1)";
            ClientSize = new Size(640, 400);
            Label body = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "Arisen Studio - cross-platform shell" };
            StatusStrip status = new StatusStrip();
            _ = status.Items.Add(new ToolStripStatusLabel("Platforms: " + string.Join(", ", Enum.GetNames(typeof(Platform)))));
            MenuStrip menu = new MenuStrip();
            ToolStripMenuItem file = new ToolStripMenuItem("File");
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += (s, e) => Close();
            _ = file.DropDownItems.Add(exit);
            _ = menu.Items.Add(file);
            ToolStripMenuItem help = new ToolStripMenuItem("Help");
            ToolStripMenuItem reportIssue = new ToolStripMenuItem("Report issue...");
            reportIssue.Click += (s, e) =>
            {
                using (ReportIssueDialog dialog = new ReportIssueDialog()) { dialog.ShowDialog(this); }
            };
            _ = help.DropDownItems.Add(reportIssue);
            _ = menu.Items.Add(help);
            // Fill control first, then the strips, then the menu (the order the spike verified).
            Controls.Add(body);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;
        }
    }
}
