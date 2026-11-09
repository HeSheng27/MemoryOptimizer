using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace IconBuilder
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            string icoPath = Path.Combine(root, "app.ico");
            string previewDir = Path.Combine(root, "tools");

            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            byte[][] images = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap bitmap = DrawChip(sizes[i]))
                {
                    images[i] = BuildDib(bitmap);
                    if (sizes[i] == 32 || sizes[i] == 256)
                        bitmap.Save(Path.Combine(previewDir, "icon-preview-" + sizes[i] + ".png"), ImageFormat.Png);
                }
            }

            using (FileStream stream = File.Create(icoPath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)sizes.Length);

                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)images[i].Length);
                    writer.Write((uint)offset);
                    offset += images[i].Length;
                }

                foreach (byte[] image in images)
                    writer.Write(image);
            }

            Console.WriteLine("icon written: " + icoPath + " (" + new FileInfo(icoPath).Length + " bytes)");
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Bitmap DrawChip(int size)
        {
            Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);

                float s = size;
                float pad = 0.05f * s;
                RectangleF background = new RectangleF(pad, pad, s - 2 * pad, s - 2 * pad);
                using (GraphicsPath path = RoundedRect(background, 0.235f * s))
                using (LinearGradientBrush brush = new LinearGradientBrush(background,
                           Color.FromArgb(255, 82, 152, 255), Color.FromArgb(255, 24, 82, 196), 62f))
                {
                    graphics.FillPath(brush, path);
                }

                float bodyMin = 0.295f * s;
                float bodySize = 0.41f * s;
                float pinThickness = 0.05f * s;
                float pinLength = 0.085f * s;

                using (SolidBrush white = new SolidBrush(Color.White))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float center = bodyMin + bodySize * (i + 0.5f) / 4f;
                        FillPins(graphics, white, s, bodyMin, bodySize, center, pinThickness, pinLength);
                    }

                    using (GraphicsPath body = RoundedRect(new RectangleF(bodyMin, bodyMin, bodySize, bodySize), 0.075f * s))
                        graphics.FillPath(white, body);
                }

                RectangleF core = new RectangleF(0.42f * s, 0.42f * s, 0.16f * s, 0.16f * s);
                using (GraphicsPath corePath = RoundedRect(core, 0.035f * s))
                using (LinearGradientBrush coreBrush = new LinearGradientBrush(core,
                           Color.FromArgb(255, 76, 143, 246), Color.FromArgb(255, 28, 88, 198), 62f))
                {
                    graphics.FillPath(coreBrush, corePath);
                }
            }
            return bitmap;
        }

        private static void FillPins(Graphics graphics, Brush brush, float s, float bodyMin, float bodySize,
            float center, float thickness, float length)
        {
            float half = thickness / 2f;
            RectangleF[] pins =
            {
                new RectangleF(bodyMin - length, center - half, length + 0.01f * s, thickness),
                new RectangleF(bodyMin + bodySize - 0.01f * s, center - half, length + 0.01f * s, thickness),
                new RectangleF(center - half, bodyMin - length, thickness, length + 0.01f * s),
                new RectangleF(center - half, bodyMin + bodySize - 0.01f * s, thickness, length + 0.01f * s)
            };

            foreach (RectangleF pin in pins)
            {
                using (GraphicsPath path = RoundedRect(pin, 0.02f * s))
                    graphics.FillPath(brush, path);
            }
        }

        private static byte[] BuildDib(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[data.Stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            int stride = data.Stride;
            bitmap.UnlockBits(data);

            int maskStride = ((width + 31) / 32) * 4;
            int maskSize = maskStride * height;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(40);
                writer.Write(width);
                writer.Write(height * 2);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(0);
                writer.Write(width * height * 4 + maskSize);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);

                for (int y = height - 1; y >= 0; y--)
                    writer.Write(pixels, y * stride, width * 4);

                writer.Write(new byte[maskSize]);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
