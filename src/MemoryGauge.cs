using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    /// <summary>内存概览：大号占用率 + 进度条 + 四项统计。</summary>
    internal sealed class MemoryGauge : Control
    {
        private MemorySnapshot _snapshot;
        private double _shown;
        private double _target;
        private float _scale;
        private readonly Timer _animation;

        public MemoryGauge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Card;
            _animation = new Timer();
            _animation.Interval = 16;
            _animation.Tick += OnAnimationTick;
        }

        public void UpdateSnapshot(MemorySnapshot snapshot, bool animate)
        {
            if (snapshot == null) return;
            _snapshot = snapshot;
            _target = snapshot.UsedPercent;

            if (!animate)
            {
                _shown = _target;
                Invalidate();
                return;
            }

            if (!_animation.Enabled)
                _animation.Start();
            Invalidate();
        }

        public void RefreshData()
        {
            Invalidate();
        }

        private float UiScale
        {
            get { return _scale <= 0f ? 1f : _scale; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = Theme.DpiScale(this);
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            double delta = _target - _shown;
            if (Math.Abs(delta) < 0.05)
            {
                _shown = _target;
                _animation.Stop();
            }
            else
            {
                _shown += delta * 0.2;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(BackColor);

            MemorySnapshot snapshot = _snapshot;
            if (snapshot == null || Width < 40 || Height < 40) return;

            float scale = UiScale;
            int width = Width;
            Color usageColor = Theme.UsageColor(_shown);

            // ── 顶部：百分比 / 说明 / 可用内存 ──────────────────────────────
            string percentText = string.Format(CultureInfo.InvariantCulture, "{0:0.0}", _shown);
            Size percentSize = Theme.Measure(graphics, percentText, Theme.BigNumber);
            Size unitSize = Theme.Measure(graphics, "%", Theme.BigUnit);
            Theme.Draw(graphics, percentText, Theme.BigNumber, new Point(0, 0), usageColor);
            Theme.Draw(graphics, "%", Theme.BigUnit,
                new Point(percentSize.Width + (int)(2 * scale), percentSize.Height - unitSize.Height - (int)(5 * scale)),
                usageColor);

            string caption = Loc.T("gauge.caption");
            Size captionSize = Theme.Measure(graphics, caption, Theme.Caps);
            int captionX = percentSize.Width + unitSize.Width + (int)(14 * scale);
            Theme.Draw(graphics, caption, Theme.Caps,
                new Point(captionX, percentSize.Height - captionSize.Height - (int)(7 * scale)), Theme.TextDim);

            string availableText = Loc.F("gauge.available", Fmt.Bytes(snapshot.AvailPhys));
            string totalText = Loc.F("gauge.total", Fmt.Bytes(snapshot.TotalPhys));
            Size availableSize = Theme.Measure(graphics, availableText, Theme.ValueSmall);
            Size totalSize = Theme.Measure(graphics, totalText, Theme.Caps);
            int rightEdge = width;
            Theme.Draw(graphics, availableText, Theme.ValueSmall,
                new Point(rightEdge - availableSize.Width, percentSize.Height - totalSize.Height - availableSize.Height - (int)(5 * scale)),
                Theme.Text);
            Theme.Draw(graphics, totalText, Theme.Caps,
                new Point(rightEdge - totalSize.Width, percentSize.Height - totalSize.Height - (int)(3 * scale)),
                Theme.TextMuted);

            // ── 进度条 ────────────────────────────────────────────────────
            int barTop = percentSize.Height + (int)(9 * scale);
            int barHeight = Math.Max(8, (int)(11 * scale));
            RectangleF track = new RectangleF(0, barTop, width, barHeight);
            using (GraphicsPath trackPath = Theme.RoundRect(track, barHeight / 2f))
            using (SolidBrush trackBrush = new SolidBrush(Theme.Track))
            {
                graphics.FillPath(trackBrush, trackPath);
            }

            double percent = Math.Max(0, Math.Min(100, _shown));
            float fillWidth = (float)(width * percent / 100.0);
            if (percent > 0 && fillWidth < barHeight) fillWidth = barHeight;
            if (fillWidth > 0.5f)
            {
                RectangleF fill = new RectangleF(0, barTop, fillWidth, barHeight);
                using (GraphicsPath fillPath = Theme.RoundRect(fill, barHeight / 2f))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                           new RectangleF(0, barTop, Math.Max(fillWidth, 2f), barHeight),
                           Theme.AccentLight, usageColor, LinearGradientMode.Horizontal))
                {
                    graphics.FillPath(fillBrush, fillPath);
                }
            }

            // ── 底部：四项统计 ─────────────────────────────────────────────
            int statsTop = barTop + barHeight + (int)(13 * scale);
            int statsHeight = Height - statsTop;
            if (statsHeight < 24) return;

            string[] captions = { Loc.T("gauge.used"), Loc.T("gauge.ws"), Loc.T("gauge.cache"), Loc.T("gauge.processes") };
            string[] values =
            {
                Fmt.Bytes(snapshot.UsedPhys),
                snapshot.WorkingSetSum >= 0 ? Fmt.Bytes(snapshot.WorkingSetSum) : Loc.T("gauge.measuring"),
                Fmt.Bytes(snapshot.SystemCache),
                snapshot.ProcessCount.ToString(CultureInfo.InvariantCulture)
            };

            float columnWidth = width / 4f;
            for (int i = 0; i < 4; i++)
            {
                float columnLeft = i * columnWidth;
                if (i > 0)
                {
                    using (Pen pen = new Pen(Theme.Separator))
                    {
                        graphics.DrawLine(pen, columnLeft, statsTop + (int)(3 * scale), columnLeft,
                            statsTop + statsHeight - (int)(4 * scale));
                    }
                }

                int textX = (int)columnLeft + (i == 0 ? 0 : (int)(12 * scale));
                Theme.Draw(graphics, captions[i], Theme.Caps, new Point(textX, statsTop), Theme.TextMuted);
                Size measuredCaption = Theme.Measure(graphics, captions[i], Theme.Caps);
                Theme.Draw(graphics, values[i], Theme.Value,
                    new Point(textX, statsTop + measuredCaption.Height + (int)(2 * scale)), Theme.Text);
            }
        }
    }
}
