using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    /// <summary>语言选择对话框：顶部“跟随系统”，其余按英文名首字母排序。</summary>
    internal sealed class LanguageForm : Form
    {
        private readonly ListBox _list;
        private readonly List<string> _codes = new List<string>();
        private readonly FlatButton _ok;
        private readonly FlatButton _cancel;

        public string SelectedCode
        {
            get
            {
                int index = _list.SelectedIndex;
                if (index < 0 || index >= _codes.Count) return Loc.AutoCode;
                return _codes[index];
            }
        }

        public LanguageForm(string currentSetting)
        {
            Text = Loc.T("lang.title");
            Font = Theme.Body;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            ClientSize = new Size(420, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);

            Label title = new Label();
            title.Text = Loc.T("lang.title");
            title.Font = Theme.Title;
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label hint = new Label();
            hint.Text = Loc.T("lang.hint");
            hint.Font = Theme.Caps;
            hint.ForeColor = Theme.TextDim;
            hint.AutoSize = true;
            hint.MaximumSize = new Size(380, 0);
            hint.Location = new Point(20, 46);
            Controls.Add(hint);

            _list = new ListBox();
            _list.Location = new Point(20, 76);
            _list.Size = new Size(380, 376);
            _list.Font = Theme.Body;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.IntegralHeight = false;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = Math.Max(22, Theme.Body.Height + 6);
            _list.DrawItem += OnDrawItem;
            _list.DoubleClick += delegate { Accept(); };
            Controls.Add(_list);

            _codes.Add(Loc.AutoCode);
            _list.Items.Add(Loc.T("lang.follow"));

            string target = string.Equals(currentSetting, Loc.AutoCode, StringComparison.OrdinalIgnoreCase)
                ? Loc.AutoCode
                : Loc.Normalize(currentSetting);

            int selected = 0;
            foreach (Loc.LanguageInfo language in Loc.Languages)
            {
                _codes.Add(language.Code);
                _list.Items.Add(language.DisplayName);
                if (string.Equals(language.Code, target, StringComparison.OrdinalIgnoreCase))
                    selected = _codes.Count - 1;
            }

            if (_list.Items.Count > 0)
                _list.SelectedIndex = selected;

            _cancel = new FlatButton();
            _cancel.Primary = false;
            _cancel.Font = Theme.Body;
            _cancel.Size = new Size(88, 32);
            _cancel.Location = new Point(208, 468);
            _cancel.Text = Loc.T("ex.cancel");
            _cancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(_cancel);

            _ok = new FlatButton();
            _ok.Primary = true;
            _ok.Font = Theme.Body;
            _ok.Size = new Size(92, 32);
            _ok.Location = new Point(308, 468);
            _ok.Text = Loc.T("lang.ok");
            _ok.Click += delegate { Accept(); };
            Controls.Add(_ok);
        }

        private void Accept()
        {
            if (_list.SelectedIndex < 0) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush background = new SolidBrush(selected ? Theme.Accent : Theme.Card))
                e.Graphics.FillRectangle(background, e.Bounds);

            string text = Convert.ToString(_list.Items[e.Index]);
            Color color = selected ? Color.White : Theme.Text;
            TextRenderer.DrawText(e.Graphics, text, _list.Font,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
                e.DrawFocusRectangle();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                Accept();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
