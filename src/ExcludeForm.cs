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
            Text = "排除名单";
            Font = Theme.Body;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            ClientSize = new Size(470, 414);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);

            Label title = new Label();
            title.Text = "排除名单";
            title.Font = Theme.Title;
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label hint = new Label();
            hint.Text = "每行一个进程名，可不带 .exe（例如 chrome、obs64）。名单里的进程在清理时会被直接跳过。";
            hint.Font = Theme.Caps;
            hint.ForeColor = Theme.TextDim;
            hint.AutoSize = true;
            hint.MaximumSize = new Size(428, 0);
            hint.Location = new Point(20, 46);
            Controls.Add(hint);

            Panel host = new Panel();
            host.Location = new Point(20, 96);
            host.Size = new Size(430, 184);
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
            _textBox.AccessibleName = "排除的进程名";
            if (!string.IsNullOrEmpty(names))
                _textBox.Text = names.Replace("|", Environment.NewLine);
            host.Controls.Add(_textBox);

            _protect = new CheckBox();
            _protect.Text = "清理时跳过前台程序（正在玩的游戏、正在用的软件不会被清理）";
            _protect.Checked = protectForeground;
            _protect.Font = Theme.Body;
            _protect.ForeColor = Theme.Text;
            _protect.AutoSize = false;
            _protect.Size = new Size(430, 26);
            _protect.Location = new Point(20, 292);
            _protect.Cursor = Cursors.Hand;
            Controls.Add(_protect);

            Label footer = new Label();
            footer.Text = "系统关键进程（lsass、csrss、dwm、winlogon 等）始终跳过，本程序不会结束任何进程。";
            footer.Font = Theme.Caps;
            footer.ForeColor = Theme.TextMuted;
            footer.AutoSize = true;
            footer.MaximumSize = new Size(430, 0);
            footer.Location = new Point(20, 326);
            Controls.Add(footer);

            FlatButton cancel = new FlatButton();
            cancel.Primary = false;
            cancel.Font = Theme.Body;
            cancel.Size = new Size(88, 32);
            cancel.Location = new Point(258, 366);
            cancel.Text = "取消";
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
            ok.Location = new Point(358, 366);
            ok.Text = "保存";
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
