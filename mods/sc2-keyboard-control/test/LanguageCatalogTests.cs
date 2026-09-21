using System;
using System.IO;
using System.Reflection;
using SCDEKeyboardControl;

internal static class LanguageCatalogTests
{
    private static int count;
    private static void Check(bool value, string message)
    {
        count++;
        if (!value) throw new Exception(message);
    }

    private static LanguageCatalog Load(string folder, string language)
    {
        var catalog = new LanguageCatalog();
        catalog.Load(folder, language);
        return catalog;
    }

    public static int Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "scde-language-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            // This is the production reader and defaults, not a substitute .NET/Unity serializer.
            var cold = new LanguageCatalog();
            Check(cold.Get("Save_and_Apply") == "Save and Apply" && !Directory.Exists(folder), "Constructor needs no disk or Unity calls");
            cold.Load(null, "en");
            Check(cold.Errors.Count > 0 && cold.Get("Save_and_Apply") == "Save and Apply", "Unavailable folder is nonfatal");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SCDEKeyboardControl.Locales.en.json"))
            using (var reader = new StreamReader(stream))
            {
                var pack = LanguageJson.Read(reader.ReadToEnd());
                var ui = (System.Collections.Generic.Dictionary<string, object>)
                    ((System.Collections.Generic.Dictionary<string, object>)pack["strings"])["ui"];
                Check(ui.Count == LanguageDefaults.English().Count, "Compiled default key count");
                foreach (var entry in ui) Check(cold.Get(entry.Key) == (string)entry.Value, "Compiled default " + entry.Key);
            }
            var en = Load(folder, "en-US");
            Check(en.Errors.Count == 0 && en.Get("Save_and_Apply") == "Save and Apply", "English fallback");
            Check(File.Exists(Path.Combine(folder, "zh-CN.json")), "Seed templates");
            var zh = Load(folder, "zh-TW");
            Check(zh.Errors.Count == 0 && zh.Get("Save_and_Apply") == "保存并应用", "Chinese fallback");
            var chinesePack = LanguageJson.Read(File.ReadAllText(Path.Combine(folder, "zh-CN.json")));
            var chineseText = (System.Collections.Generic.Dictionary<string, object>)
                ((System.Collections.Generic.Dictionary<string, object>)chinesePack["strings"])["ui"];
            Check(chineseText.Count == LanguageDefaults.English().Count, "All Chinese keys shipped");
            foreach (var entry in chineseText) Check(zh.Get(entry.Key) == (string)entry.Value, "Loaded Chinese translation " + entry.Key);
            string japanese = Path.Combine(folder, "ja.json");
            File.WriteAllText(japanese, "{\"language\":\"ja\",\"strings\":{\"ui\":{\"Save_and_Apply\":\"保存テスト\"}}}");
            var ja = Load(folder, "ja-JP");
            Check(ja.Language == "ja" && ja.Get("Save_and_Apply") == "保存テスト", "Regional fallback");
            Check(ja.Get("Discard_and_Close") == "Discard and Close", "Partial fallback");
            File.WriteAllText(japanese, "invalid-json");
            Check(ja.Get("Save_and_Apply") == "保存テスト", "Cache without per-frame reads");
            foreach (string malformed in new[] {
                "invalid-json", "null", "{}", "{\"language\":\"ja\"}",
                "{\"language\":\"ja\",\"strings\":null}", "{\"language\":\"ja\",\"strings\":{\"ui\":null}}",
                "{\"language\":\"de\",\"strings\":{\"ui\":{}}}", "{\"language\":\"ja\",\"strings\":[]}",
                new string('a', 512 * 1024 + 1) })
            {
                File.WriteAllText(japanese, malformed);
                var invalid = Load(folder, "ja");
                Check(invalid.Errors.Count > 0 && invalid.Get("Save_and_Apply") == "Save and Apply", "Malformed pack is nonfatal");
                Check(File.ReadAllText(japanese) == malformed, "Do not overwrite invalid pack");
            }
            Check(Load(folder, "fr-FR").Language == "en", "Unknown system language");
            File.WriteAllText(japanese, "{\"language\":\"ja\",\"strings\":{\"ui\":{\"Modifier_Status\":\"bad {9}\",\"Save_and_Apply\":42}}}");
            var guarded = Load(folder, "ja");
            Check(guarded.Get("Modifier_Status") == en.Get("Modifier_Status"), "Placeholder guard");
            Check(guarded.Get("Save_and_Apply") == en.Get("Save_and_Apply"), "Non-string guard");
            File.WriteAllText(japanese, "{\"language\":\"ja\",\"strings\":{\"ui\":{\"Save_and_Apply\":\"" + new string('x', 16001) + "\"}}}");
            Check(Load(folder, "ja").Get("Save_and_Apply") == "Save and Apply", "Oversized value guard");
            File.WriteAllText(Path.Combine(folder, "en.json"), "{\"language\":\"en\",\"strings\":{\"ui\":{\"Save_and_Apply\":\"Player translation\"}}}");
            Check(Load(folder, "de").Get("Save_and_Apply") == "Player translation", "Editable English template");
            Check(Load(folder, "de").Get("Discard_and_Close") == "Discard and Close", "Missing English key uses compiled default");
            File.WriteAllText(Path.Combine(folder, "en.json"), "null");
            Check(Load(folder, "en").Get("Save_and_Apply") == "Save and Apply", "Null English pack cannot break startup");
            var escaped = LanguageJson.Read("\uFEFF{\"text\":\"\\u4e2d\\u6587\\n\\t\\r\\b\\f\\\\\\/\\\"\\ud83d\\ude00\",\"meta\":[true,false,null,1,-2.5e+3,{}]}");
            Check((string)escaped["text"] == "中文\n\t\r\b\f\\/\"\ud83d\ude00", "JSON escapes, BOM and metadata");
            foreach (string invalid in new[] { "null", "[]", "{", "{} junk", "{\"a\":01}", "{\"a\":1.}",
                "{\"a\":\"\\x\"}", "{\"a\":\"raw\nline\"}", "{\"a\":\"\\uZZZZ\"}", "{\"a\":true,}",
                "{\"a\":[1,]}", "{\"a\":tru}", "{\"a\":\"unterminated}", "{\"a\":" + new string('[', 40) + "0" + new string(']', 40) + "}" })
            {
                bool rejected = false;
                try { LanguageJson.Read(invalid); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Reject malformed or overdeep JSON");
            }
            Console.WriteLine("LANGUAGE_CATALOG_OK assertions=" + count + " production_parser=true native_dependencies=false");
            return 0;
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
