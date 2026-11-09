using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    internal enum LogLevel
    {
        Info,
        Success,
        Warn,
        Error
    }

    internal static class Fmt
    {
        public static string Bytes(double value)
        {
            if (double.IsNaN(value) || value < 0) value = 0;
            if (value >= 1024.0 * 1024.0 * 1024.0)
                return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GB", value / (1024.0 * 1024.0 * 1024.0));
            if (value >= 1024.0 * 1024.0)
                return string.Format(CultureInfo.InvariantCulture, "{0:0} MB", value / (1024.0 * 1024.0));
            if (value >= 1024.0)
                return string.Format(CultureInfo.InvariantCulture, "{0:0} KB", value / 1024.0);
            return string.Format(CultureInfo.InvariantCulture, "{0:0} B", value);
        }

        public static string Bytes(long value) { return Bytes((double)value); }

        public static string Bytes(ulong value) { return Bytes((double)value); }

        public static string Signed(long value)
        {
            if (value == 0) return "0 MB";
            return (value > 0 ? "+" : "-") + Bytes(value > 0 ? value : -value);
        }
    }

    internal static class AppIcon
    {
        public static Icon Load()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch { }
            return SystemIcons.Application;
        }
    }

    internal static class ClampUtil
    {
        public static int Int(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static decimal Dec(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>把日志同时写入程序目录下的 MemoryOptimizer.log，方便排查后台自动优化的行为。</summary>
    internal static class FileLog
    {
        private static readonly object Sync = new object();

        public static void Write(string message, LogLevel level)
        {
            try
            {
                string path = Path.Combine(Settings.Directory, "MemoryOptimizer.log");
                lock (Sync)
                {
                    FileInfo info = new FileInfo(path);
                    if (info.Exists && info.Length > 512 * 1024)
                    {
                        try { File.Copy(path, path + ".old", true); }
                        catch { }
                        File.WriteAllText(path, string.Empty, new UTF8Encoding(false));
                    }

                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + level + "]  " +
                                  message + Environment.NewLine;
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
