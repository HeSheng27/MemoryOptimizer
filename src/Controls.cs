using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    /// <summary>白色圆角卡片，自带标题与右上角说明文字。</summary>
    internal class CardPanel : Panel
    {
        private readonly List<Control> _headerControls = new List<Control>();
        private bool _layoutingHeader;
        private string _title = string.Empty;
        private string _headerNote = string.Empty;
        private Color _headerNoteColor = Theme.TextMuted;

        public string Title
        {
            get { return _title; }
            set { if (_title == value) return; _title = value ?? string.Empty; Invalidate(); }
        }

        public string HeaderNote
        {
            get { return _headerNote; }
            set { if (_headerNote == value) return; _headerNote = value ?? string.Empty; Invalidate(); }
        }

        public Color HeaderNoteColor
        {
            get { return _headerNoteColor; }
            set { if (_headerNoteColor == value) return; _headerNoteColor = value; Invalidate(); }
        }

        public int CornerRadius = 12;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Card;
            Padding = new Padding(18, 46, 18, 14);
            Margin = new Padding(0, 0, 0, 10);
        }

        public void AddHeaderControl(Control control)
        {
            _headerControls.Add(control);
            Controls.Add(control);
            LayoutHeaderControls();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width > 0 && Height > 0)
            {
                using (GraphicsPath path = Theme.RoundRect(new RectangleF(0, 0, Width, Height), CornerRadius))
                {
                    Region previous = Region;
                    Region = new Region(path);
                    if (previous != null) previous.Dispose();
                }
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutHeaderControls();
        }

        private void LayoutHeaderControls()
        {
            if (_layoutingHeader) return;
            _layoutingHeader = true;
            try
            {
                int right = Width - Padding.Right;
                for (int i = _headerControls.Count - 1; i >= 0; i--)
                {
                    Control control = _headerControls[i];
                    if (!control.Visible) continue;
                    Point target = new Point(right - control.Width, 12);
                    if (control.Location != target) control.Location = target;
                    right -= control.Width + 8;
                }
            }
            finally
            {
                _layoutingHeader = false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(BackColor);

            using (GraphicsPath path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), CornerRadius))
            {
                using (Pen pen = new Pen(Theme.CardBorder))
                    graphics.DrawPath(pen, path);
            }

            if (_title.Length > 0)
                Theme.Draw(graphics, _title, Theme.Title, new Point(Padding.Left, 15), Theme.Text);

            if (_headerNote.Length > 0)
            {
                int right = Width - Padding.Right;
                foreach (Control control in _headerControls)
                {
                    if (control.Visible) right -= control.Width + 8;
                }
                Size size = Theme.Measure(graphics, _headerNote, Theme.Caps);
                Theme.Draw(graphics, _headerNote, Theme.Caps,
                    new Point(right - size.Width, 20), _headerNoteColor);
            }
        }
    }

    /// <summary>扁平圆角按钮，支持主按钮 / 次级按钮两种样式。</summary>
    internal sealed class FlatButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private bool _primary = true;
        private Font _fitFont;
        private string _fitText;
        private int _fitWidth = -1;

        private const TextFormatFlags MeasureFlags =
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        public bool Primary
        {
            get { return _primary; }
            set { _primary = value; Invalidate(); }
        }

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable |
                     ControlStyles.StandardClick, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = Theme.Button;
            Size = new Size(150, 38);
            BackColor = Theme.Card;
        }

        public void SetCompact()
        {
            Font = Theme.Caps;
            Size = new Size(64, 26);
        }

        /// <summary>文字过长时自动缩小字号，避免小语种按钮被裁切。</summary>
        private void EnsureFit()
        {
            if (_fitWidth == Width && string.Equals(_fitText, Text, StringComparison.Ordinal))
                return;

            _fitWidth = Width;
            _fitText = Text;
            if (_fitFont != null)
            {
                _fitFont.Dispose();
                _fitFont = null;
            }

            if (string.IsNullOrEmpty(Text) || Width <= 10) return;

            int available = Width - 14;
            Size baseSize = TextRenderer.MeasureText(Text, Font, new Size(4096, 4096), MeasureFlags);
            if (baseSize.Width <= available) return;

            for (float size = Font.Size - 0.5f; size >= 7f; size -= 0.5f)
            {
                Font candidate = new Font(Font.FontFamily, size, Font.Style, GraphicsUnit.Point);
                Size candidateSize = TextRenderer.MeasureText(Text, candidate, new Size(4096, 4096), MeasureFlags);
                if (candidateSize.Width <= available)
                {
                    _fitFont = candidate;
                    break;
                }
                candidate.Dispose();
            }
        }

        private Font DrawFont
        {
            get
            {
                EnsureFit();
                return _fitFont ?? Font;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _fitFont != null)
            {
                _fitFont.Dispose();
                _fitFont = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Focus();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(BackColor);

            RectangleF bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Theme.RoundRect(bounds, 8f))
            {
                Color fill;
                Color textColor;
                if (!Enabled)
                {
                    fill = _primary ? Color.FromArgb(224, 229, 237) : Theme.Card;
                    textColor = Color.FromArgb(154, 163, 178);
                }
                else if (_primary)
                {
                    fill = _pressed ? Theme.AccentDown : (_hover ? Theme.AccentHover : Theme.Accent);
                    textColor = Color.White;
                }
                else
                {
                    fill = _pressed ? Color.FromArgb(238, 242, 248) : (_hover ? Color.FromArgb(246, 248, 252) : Theme.Card);
                    textColor = Theme.Text;
                }

                using (SolidBrush brush = new SolidBrush(fill))
                    graphics.FillPath(brush, path);

                Color borderColor = !Enabled
                    ? Color.FromArgb(232, 235, 240)
                    : (_primary ? fill : (_hover ? Color.FromArgb(196, 205, 218) : Theme.CardBorder));
                using (Pen pen = new Pen(borderColor))
                    graphics.DrawPath(pen, path);

                if (Focused && ShowFocusCues && Enabled)
                {
                    using (Pen focusPen = new Pen(Color.FromArgb(120, _primary ? Color.White : Theme.Accent)))
                    {
                        focusPen.DashStyle = DashStyle.Dot;
                        RectangleF inner = RectangleF.Inflate(bounds, -3f, -3f);
                        using (GraphicsPath innerPath = Theme.RoundRect(inner, 6f))
                            graphics.DrawPath(focusPen, innerPath);
                    }
                }

                TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(graphics, Text, DrawFont, ClientRectangle, textColor, flags);
            }
        }
    }

    /// <summary>胶囊形状的开关控件。</summary>
    internal sealed class SwitchToggle : Control
    {
        private bool _checked;
        private bool _hover;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public SwitchToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(46, 26);
            BackColor = Theme.Card;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled) return;
            Checked = !Checked;
            Focus();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                if (Enabled) Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(BackColor);

            int trackHeight = Math.Min(Height, 26);
            float pad = 3f;
            RectangleF track = new RectangleF(0.5f, (Height - trackHeight) / 2f + 0.5f, Width - 1f, trackHeight - 1f);

            Color trackColor;
            if (!Enabled) trackColor = Color.FromArgb(226, 230, 236);
            else if (_checked) trackColor = _hover ? Theme.AccentHover : Theme.Accent;
            else trackColor = _hover ? Color.FromArgb(186, 195, 208) : Color.FromArgb(205, 212, 222);

            using (GraphicsPath path = Theme.RoundRect(track, track.Height / 2f))
            {
                using (SolidBrush brush = new SolidBrush(trackColor))
                    graphics.FillPath(brush, path);
            }

            float knob = track.Height - pad * 2f;
            float knobX = _checked ? track.Right - knob - pad : track.Left + pad;
            float knobY = track.Top + pad;

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                graphics.FillEllipse(shadow, knobX, knobY + 1f, knob, knob);
            using (SolidBrush brush = new SolidBrush(Enabled ? Color.White : Color.FromArgb(245, 246, 248)))
                graphics.FillEllipse(brush, knobX, knobY, knob, knob);
        }
    }
}
