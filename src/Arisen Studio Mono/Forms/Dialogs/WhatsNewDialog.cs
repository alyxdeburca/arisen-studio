using System;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Shows the What's New text after an update. Subclasses the stock WinForms <see cref="DataViewDialog"/>
    /// and takes plain strings, so it does not depend on the GitHub release model.
    /// Text comes from the old app: the title is "Change Log - What's New", and the subtitle and body are
    /// produced by Runtime/Startup/WhatsNewPolicy.cs.
    /// </summary>
    public class WhatsNewDialog : DataViewDialog
    {
        public WhatsNewDialog(string title, string subtitle, string body)
        {
            Text = title ?? throw new ArgumentNullException(nameof(title));
            LabelTitle.Text = subtitle ?? string.Empty;
            Body = body ?? string.Empty;
        }
    }
}
