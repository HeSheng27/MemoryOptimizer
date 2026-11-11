using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    /// <summary>
    /// 多语言支持。字符串存放在 lang/&lt;code&gt;.ini（编译时嵌入，也可放在 exe 旁边的 lang 目录覆盖），
    /// 语言清单在 lang/languages.ini（code|EnglishName|NativeName）。新增语言只需加一个 ini 文件。
    /// </summary>
    internal static class Loc
    {
        public sealed class LanguageInfo
        {
            public string Code;
            public string EnglishName;
            public string NativeName;

            public LanguageInfo(string code, string englishName, string nativeName)
            {
                Code = code;
                EnglishName = englishName;
                NativeName = nativeName;
            }

            public string DisplayName
            {
                get
                {
                    if (string.Equals(NativeName, EnglishName, StringComparison.OrdinalIgnoreCase))
                        return NativeName;
                    return NativeName + "  ·  " + EnglishName;
                }
            }
        }

        public const string AutoCode = "auto";

        private static readonly List<LanguageInfo> _languages = new List<LanguageInfo>();
        private static readonly Dictionary<string, Dictionary<string, string>> _tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static string _current = "en-US";

        static Loc()
        {
            try
            {
                LoadManifest();
                LoadTables();
            }
            catch { }

            if (_languages.Count == 0)
            {
                _languages.Add(new LanguageInfo("en-US", "English", "English"));
                _languages.Add(new LanguageInfo("zh-CN", "Chinese (Simplified)", "简体中文"));
                _languages.Add(new LanguageInfo("zh-TW", "Chinese (Traditional)", "繁體中文"));
            }

            _languages.Sort(delegate(LanguageInfo a, LanguageInfo b)
            {
                return string.Compare(a.EnglishName, b.EnglishName, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static IList<LanguageInfo> Languages
        {
            get { return _languages; }
        }

        public static string Current
        {
            get { return _current; }
        }

        /// <summary>按设置值初始化；auto 表示跟随系统。</summary>
        public static void Initialize(string setting)
        {
            if (string.IsNullOrEmpty(setting) || string.Equals(setting, AutoCode, StringComparison.OrdinalIgnoreCase))
            {
                _current = Detect();
                return;
            }
            _current = Normalize(setting);
        }

        public static void Set(string code)
        {
            _current = Normalize(code);
        }

        public static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return Detect();
            if (string.Equals(code, AutoCode, StringComparison.OrdinalIgnoreCase)) return Detect();

            foreach (LanguageInfo language in _languages)
            {
                if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                    return language.Code;
            }
            return Detect();
        }

        public static string Detect()
        {
            try
            {
                CultureInfo culture = CultureInfo.CurrentUICulture;
                string name = culture.Name ?? string.Empty;

                foreach (LanguageInfo language in _languages)
                {
                    if (string.Equals(language.Code, name, StringComparison.OrdinalIgnoreCase))
                        return language.Code;
                }

                if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    bool traditional = name.IndexOf("TW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       name.IndexOf("HK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       name.IndexOf("MO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       name.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0;
                    return Find("zh-TW") ?? Find("zh-CN") ?? "en-US";
                }

                if (name.Length >= 2)
                {
                    string prefix = name.Substring(0, 2);
                    string matched = FindByPrefix(prefix);
                    if (matched != null) return matched;
                }
            }
            catch { }

            return "en-US";
        }

        private static string Find(string code)
        {
            foreach (LanguageInfo language in _languages)
            {
                if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                    return language.Code;
            }
            return null;
        }

        private static string FindByPrefix(string prefix)
        {
            foreach (LanguageInfo language in _languages)
            {
                if (language.Code.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
                    return language.Code;
            }
            return null;
        }

        public static string T(string key)
        {
            Dictionary<string, string> table;
            string value;

            if (_tables.TryGetValue(_current, out table) && table.TryGetValue(key, out value) &&
                !string.IsNullOrEmpty(value))
                return value;

            if (_tables.TryGetValue("en-US", out table) && table.TryGetValue(key, out value) &&
                !string.IsNullOrEmpty(value))
                return value;

            return key;
        }

        public static string F(string key, params object[] args)
        {
            try
            {
                return string.Format(T(key), args);
            }
            catch
            {
                return T(key);
            }
        }

        public static string NativeName(string code)
        {
            foreach (LanguageInfo language in _languages)
            {
                if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                    return language.NativeName;
            }
            return code;
        }

        private static void LoadManifest()
        {
            string text = ReadText("lang/languages.ini");
            if (string.IsNullOrEmpty(text)) return;

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r').Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] parts = line.Split('|');
                if (parts.Length < 3) continue;

                string code = parts[0].Trim();
                string english = parts[1].Trim();
                string native = parts[2].Trim();
                if (code.Length == 0 || english.Length == 0) continue;

                if (Find(code) == null)
                    _languages.Add(new LanguageInfo(code, english, native.Length == 0 ? english : native));
            }
        }

        private static void LoadTables()
        {
            foreach (LanguageInfo language in _languages)
            {
                string text = ReadText("lang/" + language.Code + ".ini");
                if (string.IsNullOrEmpty(text)) continue;
                _tables[language.Code] = ParseTable(text);
            }
        }

        private static Dictionary<string, string> ParseTable(string text)
        {
            Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int split = line.IndexOf('=');
                if (split <= 0) continue;

                string key = line.Substring(0, split).Trim();
                string value = line.Substring(split + 1);
                if (key.Length == 0) continue;
                table[key] = value;
            }

            return table;
        }

        /// <summary>优先读取 exe 旁边 lang 目录下的文件（方便用户改翻译），找不到再读嵌入资源。</summary>
        private static string ReadText(string relativePath)
        {
            try
            {
                string baseDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.IsNullOrEmpty(baseDirectory))
                {
                    string path = Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(path))
                        return File.ReadAllText(path, Encoding.UTF8);
                }
            }
            catch { }

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = "MemoryOptimizer." + relativePath.Replace('/', '.').Replace('\\', '.');
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        return reader.ReadToEnd();
                }
            }
            catch { }

            return null;
        }
    }
}
