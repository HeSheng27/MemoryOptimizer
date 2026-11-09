using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    /// <summary>自绘日志面板，支持按宽度换行、自动滚动、滚动条与复制。</summary>
    internal sealed class LogView : Control
    {
        private sealed class Entry
        {
            public DateTime Time;
            public string Message;
            public LogLevel Level;
        }

        private sealed class Line
        {
            public string TimeText;
            public string Text;
            public Color Color;
            public bool First;
        }

        private const int MaxEntries = 500;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Line> _lines = new List<Line>();
        private readonly ContextMenuStrip _menu;
        private bool _dirty = true;
        private int _scrollBack;
        private int _lineHeight = 20;
        private float _scale;

        public LogView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = Theme.LogBg;
            Font = Theme.BodySmall;
            TabStop = true;

            _menu = new ContextMenuStrip();
            _menu.Items.Add("复制全部日志", null, delegate { CopyAll(); });
            _menu.Items.Add("清空日志", null, delegate { Clear(); });
            ContextMenuStrip = _menu;
        }

        public void Append(string message, LogLevel level)
        {
            if (message == null) return;

            Entry entry = new Entry();
            entry.Time = DateTime.Now;
            entry.Message = message;
            entry.Level = level;
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);

            _dirty = true;
            Invalidate();
        }

        public void Clear()
        {
            _entries.Clear();
            _lines.Clear();
            _scrollBack = 0;
            _dirty = true;
            Invalidate();
        }

        public void CopyAll()
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                foreach (Entry entry in _entries)
                    builder.AppendLine(entry.Time.ToString("HH:mm:ss") + "  " + entry.Message);
                if (builder.Length > 0)
                    Clipboard.SetText(builder.ToString());
            }
            catch { }
        }

        private float UiScale
        {
            get { return _scale <= 0f ? 1f : _scale; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = Theme.DpiScale(this);
            _dirty = true;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int step = Math.Max(1, Math.Abs(e.Delta) / 120) * 3;
            _scrollBack += e.Delta > 0 ? step : -step;
            if (_scrollBack < 0) _scrollBack = 0;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _dirty = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(BackColor);

            float scale = UiScale;
            int padX = (int)(11 * scale);
            int padY = (int)(7 * scale);
            _lineHeight = Math.Max(16, Font.Height + (int)(3 * scale));

            if (_dirty)
                Rebuild(graphics, Width - padX * 2 - (int)(8 * scale));

            if (_lines.Count == 0)
            {
                TextRenderer.DrawText(graphics, "暂无日志", Font, ClientRectangle, Theme.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            int visible = Math.Max(1, (Height - padY * 2) / _lineHeight);
            int maxStart = Math.Max(0, _lines.Count - visible);
            if (_scrollBack > maxStart) _scrollBack = maxStart;
            int start = maxStart - _scrollBack;

            string timeSample = "00:00:00  ";
            int timeWidth = Theme.Measure(graphics, timeSample, Font).Width;

            int y = padY;
            for (int i = start; i < _lines.Count && y + _lineHeight <= Height - padY + _lineHeight / 2; i++)
            {
                Line line = _lines[i];
                if (line.First && line.TimeText != null)
                    Theme.Draw(graphics, line.TimeText, Font, new Point(padX, y), Theme.TextMuted);
                Theme.Draw(graphics, line.Text, Font, new Point(padX + timeWidth, y), line.Color);
                y += _lineHeight;
            }

            if (_lines.Count > visible)
            {
                int trackTop = padY;
                int trackHeight = Height - padY * 2;
                int thumbHeight = Math.Max((int)(22 * scale), (int)(trackHeight * (float)visible / _lines.Count));
                float position = maxStart == 0 ? 0f : (float)(maxStart - _scrollBack) / maxStart;
                int thumbTop = trackTop + (int)((trackHeight - thumbHeight) * position);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(70, 100, 116, 139)))
                    graphics.FillRectangle(brush, Width - (int)(5 * scale), thumbTop, Math.Max(2, (int)(2 * scale)), thumbHeight);
            }
        }

        private void Rebuild(Graphics graphics, int width)
        {
            _lines.Clear();
            if (width < 60) width = 60;

            int timeWidth = Theme.Measure(graphics, "00:00:00  ", Font).Width;
            int messageWidth = Math.Max(40, width - timeWidth);

            foreach (Entry entry in _entries)
            {
                Color color;
                switch (entry.Level)
                {
                    case LogLevel.Success: color = Theme.Success; break;
                    case LogLevel.Warn: color = Theme.Warn; break;
                    case LogLevel.Error: color = Theme.Danger; break;
                    default: color = Theme.Text; break;
                }

                string time = entry.Time.ToString("HH:mm:ss") + "  ";
                string message = entry.Message;
                bool first = true;

                while (true)
                {
                    int take = Fit(graphics, message, messageWidth);
                    if (take <= 0 && message.Length > 0) take = 1;

                    Line line = new Line();
                    line.TimeText = first ? time : null;
                    line.Text = message.Substring(0, take);
                    line.Color = color;
                    line.First = first;
                    _lines.Add(line);

                    if (take >= message.Length) break;
                    message = message.Substring(take);
                    first = false;
                }
            }

            _dirty = false;
        }

        private int Fit(Graphics graphics, string text, int maxWidth)
        {
            if (text.Length == 0) return 0;
            if (Theme.Measure(graphics, text, Font).Width <= maxWidth) return text.Length;

            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (Theme.Measure(graphics, text.Substring(0, middle), Font).Width <= maxWidth)
                    low = middle;
                else
                    high = middle - 1;
            }
            return low;
        }
    }
}
