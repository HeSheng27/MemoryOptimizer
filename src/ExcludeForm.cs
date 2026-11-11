using System;
using System.Drawing;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    /// <summary>排除名单编辑对话框。</summary>
    internal sealed class ExcludeForm : Form
    {
        private readonly TextBox _textBox;
        private readonly CheckBox _protect;

        public string Names
        {
            get { return _textBox.Text; }
        }

        public bool ProtectForeground
        {
            get { return _protect.Checked; }
        }

        public ExcludeForm(string names, bool protectForeground)
        {
            Text = Loc.T("ex.title");
            Font = Theme.Body;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            ClientSize = new Size(470, 466);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);

            Label title = new Label();
            title.Text = Loc.T("ex.title");
            title.Font = Theme.Title;
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label hint = new Label();
            hint.Text = Loc.T("ex.hint");
            hint.Font = Theme.Caps;
            hint.ForeColor = Theme.TextDim;
            hint.AutoSize = true;
            hint.MaximumSize = new Size(428, 0);
            hint.Location = new Point(20, 46);
            Controls.Add(hint);

            Panel host = new Panel();
            host.Location = new Point(20, 120);
            host.Size = new Size(430, 168);
            host.BackColor = Theme.CardBorder;
            host.Padding = new Padding(1);
            Controls.Add(host);

            _textBox = new TextBox();
            _textBox.Dock = DockStyle.Fill;
            _textBox.Multiline = true;
            _textBox.AcceptsReturn = true;
            _textBox.WordWrap = false;
            _textBox.ScrollBars = ScrollBars.Vertical;
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Font = Theme.Body;
            _textBox.BackColor = Color.White;
            _textBox.ForeColor = Theme.Text;
            _textBox.AccessibleName = Loc.T("ex.accessibility");
            if (!string.IsNullOrEmpty(names))
                _textBox.Text = names.Replace("|", Environment.NewLine);
            host.Controls.Add(_textBox);

            _protect = new CheckBox();
            _protect.Text = Loc.T("ex.protect");
            _protect.Checked = protectForeground;
            _protect.Font = Theme.Body;
            _protect.ForeColor = Theme.Text;
            _protect.AutoSize = false;
            _protect.Size = new Size(430, 26);
            _protect.Location = new Point(20, 296);
            _protect.Cursor = Cursors.Hand;
            Controls.Add(_protect);

            Label footer = new Label();
            footer.Text = Loc.T("ex.footer");
            footer.Font = Theme.Caps;
            footer.ForeColor = Theme.TextMuted;
            footer.AutoSize = true;
            footer.MaximumSize = new Size(430, 0);
            footer.Location = new Point(20, 328);
            Controls.Add(footer);

            FlatButton cancel = new FlatButton();
            cancel.Primary = false;
            cancel.Font = Theme.Body;
            cancel.Size = new Size(88, 32);
            cancel.Location = new Point(258, 418);
            cancel.Text = Loc.T("ex.cancel");
            cancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancel);

            FlatButton ok = new FlatButton();
            ok.Primary = true;
            ok.Font = Theme.Body;
            ok.Size = new Size(92, 32);
            ok.Location = new Point(358, 418);
            ok.Text = Loc.T("ex.save");
            ok.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(ok);

            Shown += delegate
            {
                _textBox.Focus();
                _textBox.SelectionStart = _textBox.TextLength;
                _textBox.SelectionLength = 0;
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
