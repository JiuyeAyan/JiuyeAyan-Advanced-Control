using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

internal static class PackedRulesTests
{
    public static int Main(string[] args)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(args[0]))
        {
            var resource = assembly.MainModule.Resources.OfType<EmbeddedResource>()
                .Single(r => r.Name == "SCDEKeyboardControl.PackedSettings.toml");
            if (!resource.GetResourceData().SequenceEqual(File.ReadAllBytes(args[1])))
                throw new Exception("Embedded rules differ from the source TOML.");
            var plugin = assembly.MainModule.Types.Single(t => t.Name == "SCDEKeyboardControlPlugin");
            if (assembly.MainModule.AssemblyReferences.Any(r => r.Name == "UnityEngine.JSONSerializeModule"))
                throw new Exception("Language loading must not depend on Unity JsonUtility.");
            var initializer = plugin.Methods.Single(m => m.Name == ".cctor");
            if (initializer.Body.Instructions.Any(i => i.Operand is MethodReference &&
                (new[] { "System.IO.Path", "System.IO.File", "BepInEx.Paths", "UnityEngine.JsonUtility" }
                    .Contains(((MethodReference)i.Operand).DeclaringType.FullName) ||
                ((MethodReference)i.Operand).Name == "Load")))
                throw new Exception("Plugin static initialization must not read optional language files.");
            var languageCtor = assembly.MainModule.Types.Single(t => t.Name == "LanguageCatalog")
                .Methods.Single(m => m.Name == ".ctor");
            if (languageCtor.Body.Instructions.Any(i => i.Operand is MethodReference &&
                !new[] { "System.Object", "System.Collections.Generic.List`1<System.String>",
                    "SCDEKeyboardControl.LanguageDefaults", "SCDEKeyboardControl.LanguageCatalog" }
                    .Contains(((MethodReference)i.Operand).DeclaringType.FullName)))
                throw new Exception("Language construction must use compiled defaults only.");
            var awake = plugin.Methods.Single(m => m.Name == "Awake");
            if (!awake.Body.Instructions.Any(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).DeclaringType.Name == "LanguageCatalog" &&
                ((MethodReference)i.Operand).Name == "Load") || awake.Body.ExceptionHandlers.Count == 0)
                throw new Exception("Optional language load must occur in protected Awake, not static initialization.");
            Console.WriteLine("PLUGIN_BOOTSTRAP_CONTRACT_OK compiled_defaults=true unity_json=false protected_awake=true");
            var loader = plugin.Methods.Single(m => m.Name == "LoadBuildingPlacementSettings");
            if (loader.Body.Instructions.Any(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).DeclaringType.FullName == "System.IO.File"))
                throw new Exception("Rule loader must not read or generate external files.");
            var command = plugin.Methods.Single(m => m.Name == "ExecuteAction");
            var gate = plugin.Methods.Single(m => m.Name == "CanAcceptGameplayHotkey");
            if (gate.Body.Instructions.Any(i => i.Operand is MemberReference &&
                ((MemberReference)i.Operand).Name.Contains("Multiplayer")))
                throw new Exception("Shared gameplay gate must not disable multiplayer input.");
            // Iterator body contains both supported native default modes.
            string strings = String.Join("|", assembly.MainModule.Types.SelectMany(t => t.NestedTypes)
                .SelectMany(t => t.Methods).Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions).Where(i => i.Operand is string).Select(i => (string)i.Operand));
            if (!strings.Contains("SetDefaultFunctionsNew") || !strings.Contains("SetDefaultFunctionsSH1"))
                throw new Exception("Both native keyboard modes must reapply mod bindings.");
            if (!command.Body.Instructions.Any(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).Name == "Execute"))
                throw new Exception("Troop shortcuts must invoke the actual UI command.");
            Console.WriteLine("PACKED_RULES_OK exact_source_bytes=true external_rule_files=false ui_command=true");
            Console.WriteLine("MULTIPLAYER_INPUT_CONTRACT_OK shared_gate=true popular_and_traditional=true");
        }
        using (var game = AssemblyDefinition.ReadAssembly(args[2]))
        {
            var settings = game.MainModule.Types.Single(t => t.Name == "ConfigSettings");
            var pushSetter = settings.Methods.Single(m => m.Name == "set_Settings_PushMapScrolling");
            if (!pushSetter.Body.Instructions.Any(i => i.Operand is FieldReference &&
                ((FieldReference)i.Operand).Name == "settings_PushMapScrolling") ||
                !pushSetter.Body.Instructions.Any(i => i.Operand is FieldReference &&
                ((FieldReference)i.Operand).Name == "settingsDirty"))
                throw new Exception("Native edge-scroll property changed; review settings migration.");
            var startup = game.MainModule.Types.Single(t => t.Name == "FatControler")
                .Methods.Single(m => m.Name == "Start");
            if (!startup.Body.Instructions.Any(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).FullName == "System.Void ConfigSettings::LoadSettings()"))
                throw new Exception("Native settings startup changed; review edge-scroll default timing.");
            Console.WriteLine("NATIVE_EDGE_SCROLL_CONTRACT_OK native_property=true after_load=true");
            var radar = game.MainModule.Types.Single(t => t.Name == "FatControler")
                .Methods.Single(m => m.Name == "RadarScrollMap");
            int heldReads = radar.Body.Instructions.Count(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldfld &&
                i.Operand is FieldReference && ((FieldReference)i.Operand).Name == "mouseIsDown");
            int downReads = radar.Body.Instructions.Count(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).Name == "get_MouseIsDownStroke");
            if (heldReads != 2 || downReads != 2)
                throw new Exception("Native radar input layout changed; review the scoped camera transpiler.");
            Console.WriteLine("MINIMAP_NATIVE_LAYOUT_OK held_reads=2 down_reads=2");
            // Check the game's own input contract, not only our model's assumptions.
            // Update sends down=1, held=2, up=3 (selection/non-selection), UI cancel=0.
            var editor = game.MainModule.Types.Single(t => t.Name == "EditorDirector");
            var writes = editor.Methods.Single(m => m.Name == "Update").Body.Instructions
                .Where(i => i.Operand is MethodReference &&
                    ((MethodReference)i.Operand).Name == "updateLeftMouseStateForEngine")
                .Select(i => i.Previous.OpCode.Code).ToArray();
            if (!writes.SequenceEqual(new[] { Mono.Cecil.Cil.Code.Ldc_I4_1,
                Mono.Cecil.Cil.Code.Ldc_I4_2, Mono.Cecil.Cil.Code.Ldc_I4_3,
                Mono.Cecil.Cil.Code.Ldc_I4_3, Mono.Cecil.Cil.Code.Ldc_I4_0 }))
                throw new Exception("Native left mouse states changed; review minimap press/release.");
            // On a native tick the reader advances down -> held, pending up -> 3, up -> idle.
            var transitions = editor.Methods.Single(m => m.Name == "getMouseStateForEngine")
                .Body.Instructions.Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Stfld &&
                    i.Operand is FieldReference && ((FieldReference)i.Operand).Name == "leftMouseStateForEngine")
                .Select(i => i.Previous.OpCode.Code).ToArray();
            if (!transitions.SequenceEqual(new[] { Mono.Cecil.Cil.Code.Ldc_I4_3,
                Mono.Cecil.Cil.Code.Ldc_I4_2, Mono.Cecil.Cil.Code.Ldc_I4_0 }))
                throw new Exception("Native left mouse transitions changed; review minimap transaction.");
            Console.WriteLine("MINIMAP_NATIVE_CLICK_CONTRACT_OK down=1 held=2 up=3");
        }
        return 0;
    }
}
