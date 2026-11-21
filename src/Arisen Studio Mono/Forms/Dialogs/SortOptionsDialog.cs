using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Lets the user pick a sort option and a sort direction.
    /// Stock WinForms port of the old SortOptionsDialog form.
    /// Dialog result: OK = apply the sort, Cancel = keep the current sort.
    /// </summary>
    internal class SortOptionsDialog : Form
    {
        private readonly Label _labelSortBy;
        private readonly Label _labelSortOrder;
        private readonly FlowLayoutPanel _optionsPanel;
        private readonly FlowLayoutPanel _orderPanel;
        private readonly RadioButton _radioAscending;
        private readonly RadioButton _radioDescending;
        private readonly Button _buttonOk;
        private readonly Button _buttonCancel;
        private readonly TableLayoutPanel _root;
        private readonly List<RadioButton> _optionRadios = new List<RadioButton>();

        // The radios write these while the dialog is open. SortOption and SortOrder are only set when the dialog closes with OK.
        private string _pendingOption;
        private System.Windows.Forms.SortOrder _pendingOrder = System.Windows.Forms.SortOrder.Ascending;

        public SortOptionsDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            _labelSortBy = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _labelSortOrder = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            // Sort option radios are added in OnLoad, from SortOptions.
            _optionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, pad)
            };

            _radioAscending = new RadioButton
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, pad / 4)
            };
            _radioDescending = new RadioButton
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            _radioAscending.CheckedChanged += OrderCheckedChanged;
            _radioDescending.CheckedChanged += OrderCheckedChanged;

            _orderPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, pad)
            };
            _orderPanel.Controls.Add(_radioAscending);
            _orderPanel.Controls.Add(_radioDescending);

            _buttonOk = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0),
                DialogResult = DialogResult.OK,
                TabIndex = 0
            };
            _buttonCancel = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0, 0, pad, 0),
                DialogResult = DialogResult.Cancel,
                TabIndex = 1
            };

            // Right-to-left flow: OK sits at the far right, Cancel to its left.
            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, pad / 2, 0, 0)
            };
            buttonRow.Controls.Add(_buttonOk);
            buttonRow.Controls.Add(_buttonCancel);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++)
            {
                _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.Controls.Add(_labelSortBy, 0, 0);
            _root.Controls.Add(_optionsPanel, 0, 1);
            _root.Controls.Add(_labelSortOrder, 0, 2);
            _root.Controls.Add(_orderPanel, 0, 3);
            _root.Controls.Add(buttonRow, 0, 4);
            Controls.Add(_root);

            _optionsPanel.TabIndex = 0;
            _orderPanel.TabIndex = 1;
            buttonRow.TabIndex = 2;

            ApplyLanguage();

            AcceptButton = _buttonOk;
            CancelButton = _buttonCancel;
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// The sort options to offer, shown as radio buttons in the order given. Null or empty entries are skipped.
        /// </summary>
        public List<string> SortOptions { get; set; } = new List<string>();

        /// <summary>
        /// The selected sort option. Read it after the dialog returns OK. An unknown or empty value selects the first option.
        /// </summary>
        public string SortOption { get; set; }

        /// <summary>
        /// The selected sort direction. Read it after the dialog returns OK. Only Descending selects the descending radio;
        /// every other value is shown and returned as Ascending.
        /// </summary>
        public System.Windows.Forms.SortOrder SortOrder { get; set; } = System.Windows.Forms.SortOrder.Ascending;

        protected override void OnLoad(EventArgs e)
        {
            _pendingOption = SortOption;
            _pendingOrder = SortOrder;
            ApplyLanguage();
            BuildOptionRadios();
            SelectSortOption();
            SelectSortOrderRadio();
            SizeToContent();
            base.OnLoad(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // Only OK commits the choice. Cancel, Escape and the window close button leave SortOption and SortOrder as they were.
            if (DialogResult == DialogResult.OK)
            {
                SortOption = _pendingOption;
                SortOrder = _pendingOrder;
            }
        }

        private void ApplyLanguage()
        {
            Text = Lookup("LABEL_SORT_OPTIONS", "Sort Options");

            _labelSortBy.Text = Lookup("LABEL_SORT_BY", "Sort By") + ":";
            _labelSortOrder.Text = Lookup("LABEL_SORT_ORDER", "Sort Order") + ":";

            _radioAscending.Text = Lookup("LABEL_ASCENDING", "Ascending");
            _radioDescending.Text = Lookup("LABEL_DESCENDING", "Descending");

            _buttonOk.Text = Lookup("LABEL_OK", "OK");
            _buttonCancel.Text = Lookup("LABEL_CANCEL", "Cancel");
        }

        /// <summary>
        /// Replaces the option radios with one radio per entry in SortOptions. Safe to call more than once.
        /// </summary>
        private void BuildOptionRadios()
        {
            int pad = Font.Height / 2;

            foreach (RadioButton old in _optionRadios)
            {
                _optionsPanel.Controls.Remove(old);
                old.Dispose();
            }
            _optionRadios.Clear();

            List<string> options = SortOptions ?? new List<string>();
            foreach (string option in options)
            {
                if (string.IsNullOrEmpty(option))
                {
                    continue;
                }

                RadioButton radio = new RadioButton
                {
                    AutoSize = true,
                    UseMnemonic = false,
                    Text = option,
                    Tag = option,
                    Margin = new Padding(0, 0, 0, pad / 4)
                };
                radio.CheckedChanged += OptionCheckedChanged;
                _optionRadios.Add(radio);
                _optionsPanel.Controls.Add(radio);
            }
        }

        /// <summary>
        /// Checks the radio that matches SortOption (ordinal match), or the first radio when nothing matches.
        /// Leaves SortOption untouched when there are no options.
        /// </summary>
        private void SelectSortOption()
        {
            if (_optionRadios.Count == 0)
            {
                return;
            }

            RadioButton chosen = _optionRadios[0];
            if (!string.IsNullOrEmpty(SortOption))
            {
                foreach (RadioButton radio in _optionRadios)
                {
                    if (string.Equals(radio.Tag as string, SortOption, StringComparison.Ordinal))
                    {
                        chosen = radio;
                        break;
                    }
                }
            }

            chosen.Checked = true;
        }

        private void SelectSortOrderRadio()
        {
            if (SortOrder == System.Windows.Forms.SortOrder.Descending)
            {
                _radioDescending.Checked = true;
            }
            else
            {
                _radioAscending.Checked = true;
                _pendingOrder = System.Windows.Forms.SortOrder.Ascending;
            }
        }

        /// <summary>
        /// Sizes the form to its content, clamped to 90% of the working area of the screen it is on.
        /// Called before base.OnLoad so the final size is set before the form is positioned.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Size preferred = _root.PreferredSize;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(Math.Max(h * 20, preferred.Width), (int)(work.Width * 0.9));
            int height = Math.Min(Math.Max(preferred.Height, h * 14), (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private void OptionCheckedChanged(object sender, EventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            if (radio != null && radio.Checked)
            {
                _pendingOption = radio.Tag as string;
            }
        }

        private void OrderCheckedChanged(object sender, EventArgs e)
        {
            _pendingOrder = _radioDescending.Checked
                ? System.Windows.Forms.SortOrder.Descending
                : System.Windows.Forms.SortOrder.Ascending;
        }

        /// <summary>
        /// Returns the localised text for a key, or the English fallback when there is no language source or no value.
        /// </summary>
        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
