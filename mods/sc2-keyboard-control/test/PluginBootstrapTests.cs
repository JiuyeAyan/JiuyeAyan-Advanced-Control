using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class PluginBootstrapTests
{
    // Executes the actual compiled DLL's managed static constructor without creating a Unity component.
    // This is not a substitute for Awake, Harmony installation or a multiplayer gameplay test.
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request)
        {
            string file = new AssemblyName(request.Name).Name + ".dll";
            foreach (string folder in new[] { args[1], args[2] })
            {
                string candidate = Path.Combine(folder, file);
                if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
            }
            return null;
        };
        var assembly = Assembly.LoadFrom(args[0]);
        var plugin = assembly.GetType("SCDEKeyboardControl.SCDEKeyboardControlPlugin", true);
        RuntimeHelpers.RunClassConstructor(plugin.TypeHandle);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var catalogType = assembly.GetType("SCDEKeyboardControl.LanguageCatalog", true);
        var catalog = plugin.GetField("Text", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var get = catalogType.GetMethod("Get", hidden);
        var load = catalogType.GetMethod("Load", hidden);
        if ((string)get.Invoke(catalog, new object[] { "Save_and_Apply" }) != "Save and Apply")
            throw new Exception("Plugin static constructor did not create compiled fallback text.");
        string folderPath = Path.Combine(Path.GetTempPath(), "scde-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            load.Invoke(catalog, new object[] { folderPath, "zh-CN" });
            if ((string)get.Invoke(catalog, new object[] { "Save_and_Apply" }) != "保存并应用")
                throw new Exception("Actual plugin DLL could not load its embedded Chinese language file.");
            File.WriteAllText(Path.Combine(folderPath, "en.json"), "null");
            catalog = Activator.CreateInstance(catalogType, true);
            load.Invoke(catalog, new object[] { folderPath, "en" });
            if ((string)get.Invoke(catalog, new object[] { "Save_and_Apply" }) != "Save and Apply")
                throw new Exception("Actual plugin DLL did not recover from invalid English JSON.");
            Console.WriteLine("PLUGIN_MANAGED_BOOTSTRAP_OK actual_dll=true static_constructor=true language_load=true corrupt_file_fallback=true unity_gameplay_test=false");
            return 0;
        }
        finally { if (Directory.Exists(folderPath)) Directory.Delete(folderPath, true); }
    }
}
