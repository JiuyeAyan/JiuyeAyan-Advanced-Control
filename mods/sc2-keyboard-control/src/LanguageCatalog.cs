using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SCDEKeyboardControl
{
    // Same on-disk contract as Manager 0.2.8. Loaded once, never polled from Update/OnGUI.
    internal sealed class LanguageCatalog
    {
        private readonly Dictionary<string, string> _english = LanguageDefaults.English();
        private readonly Dictionary<string, string> _strings = LanguageDefaults.English();
        internal readonly List<string> Errors = new List<string>();
        internal string Language { get; private set; }

        internal LanguageCatalog() { Language = "en"; }

        internal void Load(string folder, string locale)
        {
            // Optional translation failures must never prevent the gameplay plugin from starting.
            try { LoadFiles(folder, locale); }
            catch (Exception error) { Errors.Add(error.Message); }
        }

        private void LoadFiles(string folder, string locale)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            files["en"] = null;
            files["zh-CN"] = null;
            try
            {
                CheckNoLinks(folder);
                Directory.CreateDirectory(folder);
                foreach (string lang in new[] { "en", "zh-CN" })
                {
                    string target = Path.Combine(folder, lang + ".json");
                    if (!File.Exists(target))
                    {
                        using (var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                        using (var writer = new StreamWriter(stream)) writer.Write(Embedded(lang));
                    }
                }
                foreach (string file in Directory.GetFiles(folder, "*.json"))
                {
                    string lang = Path.GetFileNameWithoutExtension(file).Replace('_', '-');
                    if (Regex.IsMatch(lang, "^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$")) files[lang] = file;
                }
            }
            catch (Exception error) { Errors.Add(error.Message); }

            Language = Choose(locale, files.Keys);
            ReadAndMerge(files["en"], "en");
            if (Language != "en")
            {
                string file = files[Language];
                if (file == null && Language == "zh-CN") Merge(ReadPack(Embedded("zh-CN"), "zh-CN"));
                else ReadAndMerge(file, Language);
            }
        }

        internal static string Choose(string locale, IEnumerable<string> available)
        {
            string[] languages = available.ToArray();
            string candidate = (locale ?? "").Replace('_', '-');
            while (candidate.Length > 0)
            {
                string found = languages.FirstOrDefault(l => String.Equals(l, candidate, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
                int dash = candidate.LastIndexOf('-');
                candidate = dash < 0 ? "" : candidate.Substring(0, dash);
            }
            return (locale ?? "").StartsWith("zh", StringComparison.OrdinalIgnoreCase) &&
                languages.Contains("zh-CN") ? "zh-CN" : "en";
        }

        private void ReadAndMerge(string file, string language)
        {
            if (file == null) return;
            try
            {
                CheckNoLinks(file);
                if (new FileInfo(file).Length > 512 * 1024) throw new InvalidDataException("Oversized language file");
                Merge(ReadPack(File.ReadAllText(file), language));
            }
            catch (Exception error) { Errors.Add(Path.GetFileName(file) + ": " + error.Message); }
        }

        private static Dictionary<string, object> ReadPack(string json, string language)
        {
            var pack = LanguageJson.Read(json);
            object metadata, sections, text;
            var strings = pack.TryGetValue("strings", out sections) ? sections as Dictionary<string, object> : null;
            var ui = strings != null && strings.TryGetValue("ui", out text) ? text as Dictionary<string, object> : null;
            if (!pack.TryGetValue("language", out metadata) ||
                !String.Equals(metadata as string, language, StringComparison.OrdinalIgnoreCase) || ui == null)
                throw new InvalidDataException("Invalid language metadata or strings.ui");
            return ui;
        }

        private void Merge(Dictionary<string, object> text)
        {
            foreach (var entry in text)
            {
                string english;
                if (!_english.TryGetValue(entry.Key, out english)) continue;
                string value = entry.Value as string;
                if (value == null || value.Length > 16000) continue;
                if (Tokens(value) != Tokens(english)) continue;
                _strings[entry.Key] = value;
            }
        }

        private static string Tokens(string value)
        {
            return String.Join("|", Regex.Matches(value ?? "", "\\{[A-Za-z0-9][A-Za-z0-9]*\\}")
                .Cast<Match>().Select(m => m.Value).Distinct().OrderBy(s => s).ToArray());
        }

        private static void CheckNoLinks(string path)
        {
            for (string part = Path.GetFullPath(path); !String.IsNullOrEmpty(part); part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) &&
                    (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked language paths are not supported");
        }

        private static string Embedded(string language)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "SCDEKeyboardControl.Locales." + language + ".json"))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }

        internal string Get(string key)
        {
            string value;
            return _strings.TryGetValue(key, out value) ? value : key;
        }
    }
}
