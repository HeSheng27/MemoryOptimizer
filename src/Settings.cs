using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MemoryOptimizer
{
    /// <summary>用户配置。优先保存在程序目录（便携），目录不可写时回退到 %APPDATA%。</summary>
    internal sealed class Settings
    {
        public bool TrimWorkingSets = true;
        public bool PurgeStandby = true;
        public bool ClearFileCache = true;
        public string ExcludedProcesses = "";
        public bool ProtectForeground = true;

        public bool AutoEnabled = true;
        public int ThresholdPercent = 85;
        public int CooldownSeconds = 120;

        public bool StartWithWindows = false;
        public bool StartMinimized = false;
        public bool CloseToTray = true;

        /// <summary>auto 表示跟随系统，其余为 Loc 里的语言代码。</summary>
        public string Language = Loc.AutoCode;

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "MemoryOptimizer";

        private static string _directory;

        public static string Directory
        {
            get
            {
                if (_directory == null)
                    _directory = ResolveDirectory();
                return _directory;
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Directory, "settings.ini"); }
        }

        private static string ResolveDirectory()
        {
            string exeDir = null;
            try { exeDir = Path.GetDirectoryName(Application.ExecutablePath); }
            catch { }

            if (!string.IsNullOrEmpty(exeDir) && IsWritable(exeDir))
                return exeDir;

            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MemoryOptimizer");
            try { System.IO.Directory.CreateDirectory(fallback); }
            catch { }
            return fallback;
        }

        private static bool IsWritable(string directory)
        {
            try
            {
                string probe = Path.Combine(directory, ".mo-probe-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static Settings Load()
        {
            Settings settings = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                    return settings;

                foreach (string rawLine in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    int split = line.IndexOf('=');
                    if (split <= 0)
                        continue;

                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();
                    settings.Apply(key, value);
                }
            }
            catch { }

            settings.Sanitize();
            return settings;
        }

        public void Save()
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# 内存优化器设置文件（程序会自动维护，可手动编辑）");
                builder.AppendLine("TrimWorkingSets=" + (TrimWorkingSets ? "1" : "0"));
                builder.AppendLine("PurgeStandby=" + (PurgeStandby ? "1" : "0"));
                builder.AppendLine("ClearFileCache=" + (ClearFileCache ? "1" : "0"));
                builder.AppendLine("ExcludedProcesses=" + (ExcludedProcesses ?? ""));
                builder.AppendLine("ProtectForeground=" + (ProtectForeground ? "1" : "0"));
                builder.AppendLine("AutoEnabled=" + (AutoEnabled ? "1" : "0"));
                builder.AppendLine("ThresholdPercent=" + ThresholdPercent.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("CooldownSeconds=" + CooldownSeconds.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("StartWithWindows=" + (StartWithWindows ? "1" : "0"));
                builder.AppendLine("StartMinimized=" + (StartMinimized ? "1" : "0"));
                builder.AppendLine("CloseToTray=" + (CloseToTray ? "1" : "0"));
                builder.AppendLine("Language=" + (Language ?? Loc.AutoCode));

                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, builder.ToString(), new UTF8Encoding(false));
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch { }
        }

        private void Apply(string key, string value)
        {
            switch (key)
            {
                case "TrimWorkingSets": TrimWorkingSets = ParseBool(value, TrimWorkingSets); break;
                case "PurgeStandby": PurgeStandby = ParseBool(value, PurgeStandby); break;
                case "ClearFileCache": ClearFileCache = ParseBool(value, ClearFileCache); break;
                case "ExcludedProcesses": ExcludedProcesses = value; break;
                case "ProtectForeground": ProtectForeground = ParseBool(value, ProtectForeground); break;
                case "AutoEnabled": AutoEnabled = ParseBool(value, AutoEnabled); break;
                case "StartWithWindows": StartWithWindows = ParseBool(value, StartWithWindows); break;
                case "StartMinimized": StartMinimized = ParseBool(value, StartMinimized); break;
                case "CloseToTray": CloseToTray = ParseBool(value, CloseToTray); break;
                case "Language": Language = value; break;
                case "ThresholdPercent": ThresholdPercent = ParseInt(value, ThresholdPercent); break;
                case "CooldownSeconds": CooldownSeconds = ParseInt(value, CooldownSeconds); break;
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.Equals(value, "1", StringComparison.Ordinal)) return true;
            if (string.Equals(value, "0", StringComparison.Ordinal)) return false;
            bool parsed;
            if (bool.TryParse(value, out parsed)) return parsed;
            return fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return fallback;
        }

        public void Sanitize()
        {
            ThresholdPercent = ClampUtil.Int(ThresholdPercent, 1, 99);
            CooldownSeconds = ClampUtil.Int(CooldownSeconds, 5, 3600);
            if (ExcludedProcesses == null) ExcludedProcesses = string.Empty;
            if (Language == null) Language = Loc.AutoCode;
            if (!string.Equals(Language, Loc.AutoCode, StringComparison.OrdinalIgnoreCase))
                Language = Loc.Normalize(Language);
        }

        /// <summary>把配置中的排除名单拆成进程名列表（不带 .exe，忽略大小写）。</summary>
        public List<string> GetExcludedNames()
        {
            List<string> names = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string raw = ExcludedProcesses ?? string.Empty;
            string[] parts = raw.Split(new char[] { '|', ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string name = part.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4).Trim();
                if (name.Length > 0 && seen.Add(name))
                    names.Add(name);
            }
            return names;
        }

        /// <summary>把用户输入（每行一个）规整成配置里使用的 | 分隔格式。</summary>
        public static string NormalizeExclusions(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            List<string> names = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] parts = text.Split(new char[] { '|', ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string name = part.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4).Trim();
                if (name.Length > 0 && seen.Add(name))
                    names.Add(name);
            }
            return string.Join("|", names.ToArray());
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(RunValueName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetAutoStart(bool enabled, out string error)
        {
            error = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        error = "无法打开注册表启动项。";
                        return false;
                    }

                    if (enabled)
                    {
                        string command = "\"" + Application.ExecutablePath + "\" --minimized";
                        key.SetValue(RunValueName, command, RegistryValueKind.String);
                    }
                    else
                    {
                        if (key.GetValue(RunValueName) != null)
                            key.DeleteValue(RunValueName, false);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
