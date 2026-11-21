using System;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// A ribbon replacement command was invoked. CommandId is a RibbonCommandIds value. For a check command (Discord
    /// Presence, Install Mods to USB) Checked is the new state; for every other command it is false.
    /// </summary>
    internal sealed class RibbonCommandEventArgs : EventArgs
    {
        public RibbonCommandEventArgs(string commandId, string caption, bool isCheck, bool isChecked)
        {
            CommandId = commandId;
            Caption = caption;
            IsCheck = isCheck;
            Checked = isChecked;
        }

        public string CommandId { get; private set; }

        /// <summary>The caption the item had when it was clicked (for a status text).</summary>
        public string Caption { get; private set; }

        public bool IsCheck { get; private set; }

        public bool Checked { get; private set; }
    }
}
