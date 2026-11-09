using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    /// <summary>统一的配色、字体与绘制工具。</summary>
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(243, 245, 248);
        public static readonly Color Card = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(228, 231, 237);
        public static readonly Color Text = Color.FromArgb(30, 41, 59);
        public static readonly Color TextDim = Color.FromArgb(100, 116, 139);
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        public static readonly Color Accent = Color.FromArgb(45, 121, 245);
        public static readonly Color AccentLight = Color.FromArgb(110, 172, 255);
        public static readonly Color AccentHover = Color.FromArgb(37, 106, 222);
        public static readonly Color AccentDown = Color.FromArgb(29, 90, 196);
        public static readonly Color Track = Color.FromArgb(237, 241, 247);
        public static readonly Color Separator = Color.FromArgb(238, 241, 246);
        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Warn = Color.FromArgb(217, 119, 6);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color LogBg = Color.FromArgb(250, 251, 253);

        public const string FontName = "Microsoft YaHei UI";

        public static readonly Font Title = new Font(FontName, 11f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font Body = new Font(FontName, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font BodySmall = new Font(FontName, 9f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font Caps = new Font(FontName, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font Button = new Font(FontName, 10f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font BigNumber = new Font(FontName, 26f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font BigUnit = new Font(FontName, 13f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font Value = new Font(FontName, 11.5f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font ValueSmall = new Font(FontName, 10.5f, FontStyle.Bold, GraphicsUnit.Point);

        public const TextFormatFlags TxtFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

        public static GraphicsPath RoundRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0.5f || rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            float diameter = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color UsageColor(double percent)
        {
            if (percent >= 90) return Danger;
            if (percent >= 75) return Warn;
            return Accent;
        }

        public static Size Measure(Graphics graphics, string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return Size.Empty;
            return TextRenderer.MeasureText(graphics, text, font, new Size(4096, 4096), TxtFlags);
        }

        public static void Draw(Graphics graphics, string text, Font font, Point point, Color color)
        {
            TextRenderer.DrawText(graphics, text, font, point, color, TxtFlags);
        }

        public static float DpiScale(Control control)
        {
            try
            {
                using (Graphics graphics = control.CreateGraphics())
                {
                    float scale = graphics.DpiX / 96f;
                    return scale <= 0.1f ? 1f : scale;
                }
            }
            catch
            {
                return 1f;
            }
        }
    }
}
