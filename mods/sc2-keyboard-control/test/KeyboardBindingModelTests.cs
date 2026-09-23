using System;
using System.Collections.Generic;
using System.Linq;
using SCDEKeyboardControl;

internal static class KeyboardBindingModelTests
{
    private static int _assertions;

    private static void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition)
        {
            Console.Error.WriteLine("ASSERTION_FAILED: " + message);
            throw new InvalidOperationException(message);
        }
    }

    public static int Main(string[] args)
    {
        Random cameraRandom = new Random(1234);
        bool firstCluster = false, secondCluster = false;
        int[] group = { 17, 42 };
        for (int click = 0; click < 64; click++)
        {
            RandomUnitCameraTarget target = new RandomUnitCameraTarget();
            Assert(target.Count == 0, "Empty groups must not produce a camera target.");
            target.Consider(group[0], 210, 220, cameraRandom);
            Assert(target.UnitId == 17 && target.X == 210 && target.Y == 220,
                "A single valid member must always be the camera target.");
            target.Consider(group[1], 610, 620, cameraRandom);
            Assert(target.Count == 2 &&
                (target.UnitId == 17 && target.X == 210 && target.Y == 220 ||
                 target.UnitId == 42 && target.X == 610 && target.Y == 620),
                "Split groups must center on an actual member, never the empty midpoint.");
            firstCluster |= target.UnitId == 17;
            secondCluster |= target.UnitId == 42;
        }
        Assert(firstCluster && secondCluster, "Repeated camera requests can choose either group member.");
        Assert(group.SequenceEqual(new[] { 17, 42 }), "Camera sampling must not modify group membership.");
        double cameraX, cameraY;
        Assert(GroupCameraPlan.TryGetPosition(2, 1, 1, 300, 410, 2404, 3282, 400,
            out cameraX, out cameraY) && cameraX == 300.5 && cameraY == 410.25,
            "Camera must read native fine coordinates for a living local unit.");
        foreach (int state in new[] { 0, 1, 3 })
            Assert(!GroupCameraPlan.TryGetPosition(state, 1, 1, 300, 410, 2404, 3282, 400,
                out cameraX, out cameraY), "Non-live records must not pull the camera away.");
        Assert(!GroupCameraPlan.TryGetPosition(2, 2, 1, 300, 410, 2404, 3282, 400,
            out cameraX, out cameraY), "Reused enemy records must not affect the group camera.");
        Assert(!GroupCameraPlan.TryGetPosition(2, 1, 1, 199, 410, 2404, 3282, 400,
            out cameraX, out cameraY), "Reject coordinates outside the active map offset.");
        Assert(!GroupCameraPlan.TryGetPosition(2, 1, 1, 300, 600, 2404, 3282, 400,
            out cameraX, out cameraY), "Active map upper bound is exclusive.");
        Assert(GroupCameraPlan.TryGetPosition(2, 1, 1, 300, 410, 0, 32767, 400,
            out cameraX, out cameraY) && cameraX == 300.5 && cameraY == 410.5,
            "Invalid fine positions fall back to validated native cells, not remote targets.");
        Assert(GroupCameraPlan.TryGetPosition(2, 1, 1, 50, 750, 404, 6004, 800,
            out cameraX, out cameraY) && cameraX == 50.5 && cameraY == 750.5,
            "Camera validation must support full-size maps.");
        BindingModel model = new BindingModel();
        model.ResetDefaults();
        var launcher = SettingsLauncherPlan.BesideMenu(1360f, 540f, 600f, 1920f, 1080f);
        Assert(launcher.Left == 1368f && launcher.Top + launcher.Height / 2f == 540f,
            "Settings launcher must be eight pixels beside the actual menu, vertically centered.");
        var shiftedLauncher = SettingsLauncherPlan.BesideMenu(1460f, 600f, 600f, 1920f, 1080f);
        Assert(shiftedLauncher.Left == launcher.Left + 100f && shiftedLauncher.Top == launcher.Top + 60f,
            "Launcher must follow menu translation, not a hard-coded screen corner.");
        foreach (float screenWidth in new[] { 800f, 1024f, 1280f, 1920f, 2560f, 3840f })
        {
            float screenHeight = screenWidth * 9f / 16f;
            float menuHeight = Math.Min(600f, screenHeight);
            var entry = SettingsLauncherPlan.BesideMenu(screenWidth / 2f + 400f,
                screenHeight / 2f, menuHeight, screenWidth, screenHeight);
            Assert(entry.Left >= 0 && entry.Top >= 0 && entry.Left + entry.Width <= screenWidth &&
                entry.Top + entry.Height <= screenHeight, "Launcher must remain on screen: " + screenWidth);
        }
        var scaledLauncher = SettingsLauncherPlan.BesideMenu(2720f, 1080f, 1200f, 3840f, 2160f);
        Assert(scaledLauncher.Left == 2736f && scaledLauncher.Height == 104f,
            "Launcher must follow menu scaling.");
        Assert(MinimapControlPlan.MoveButton(true) == 0 && MinimapControlPlan.CameraButton(true) == 1,
            "Classic must use left move and right camera.");
        Assert(MinimapControlPlan.MoveButton(false) == 1 && MinimapControlPlan.CameraButton(false) == 0,
            "Modern must use right move and left camera.");
        Assert(MinimapControlPlan.LeftMouseState(true, true) == 1 &&
               MinimapControlPlan.LeftMouseState(true, false) == 3,
            "Classic native movement must send press 1 and release 3; state 2 is held, not release.");
        Assert(MinimapControlPlan.LeftMouseState(false, true) == 0 &&
               MinimapControlPlan.LeftMouseState(false, false) == 0,
            "Modern must not inject a left selection click.");

        Assert(model.Bindings.Count() == 87,
            "All requested actions except the intentionally unbound defaults must be present.");
        Assert(model.Bindings.Select(pair => pair.Value).Distinct().Count() == 79,
            "Only the eight contextual Q-I production/build pairs may share default chords.");

        KeyChord chord;
        Assert(model.TryGet(KeyboardAction.ToggleGoods, out chord) && chord.KeyCode == 109 &&
            !chord.Ctrl && !chord.Shift && !chord.Alt, "Resource stocks must default to M.");
        Assert(model.TryGet(KeyboardAction.CameraLeft, out chord), "CameraLeft is missing.");
        Assert(chord.KeyCode == 276 && !chord.Ctrl && !chord.Shift && !chord.Alt,
            "CameraLeft must default to LeftArrow, without WASD aliases.");
        Assert(model.TryGet(KeyboardAction.CameraUp, out chord) && chord.KeyCode == 273,
            "CameraUp must default to UpArrow.");
        Assert(model.TryGet(KeyboardAction.CameraRight, out chord) && chord.KeyCode == 275,
            "CameraRight must default to RightArrow.");
        Assert(model.TryGet(KeyboardAction.CameraDown, out chord) && chord.KeyCode == 274,
            "CameraDown must default to DownArrow.");
        Assert(model.TryGet(KeyboardAction.ReplaceGroup1, out chord) && chord.KeyCode == 49 && chord.Ctrl,
            "ReplaceGroup1 must default to Ctrl+1.");
        Assert(model.TryGet(KeyboardAction.SelectGroup10, out chord) && chord.KeyCode == 48 && !chord.Ctrl,
            "SelectGroup10 must default to 0.");
        foreach (int slot in new[] { 3, 4, 5 })
        {
            Assert(!model.TryGet((KeyboardAction)((int)KeyboardAction.ReplaceGroup1 + slot), out chord) &&
                !model.TryGet((KeyboardAction)((int)KeyboardAction.SelectGroup1 + slot), out chord) &&
                !model.TryGet((KeyboardAction)((int)KeyboardAction.AddGroup1 + slot), out chord) &&
                !model.TryGet((KeyboardAction)((int)KeyboardAction.ExclusiveGroup1 + slot), out chord),
                "Control groups 4-6 must default to unbound for every operation.");
        }
        Assert(model.TryGet(KeyboardAction.ExclusiveGroup7, out chord) && chord.Alt && chord.KeyCode == 55,
            "ExclusiveGroup7 must default to Alt+7.");
        Assert(model.TryGet(KeyboardAction.SaveCamera4, out chord) && chord.Ctrl && chord.KeyCode == 285,
            "SaveCamera4 must default to Ctrl+F4.");
        foreach (int slot in new[] { 4, 5, 6, 7 })
        {
            Assert(!model.TryGet((KeyboardAction)((int)KeyboardAction.SaveCamera1 + slot), out chord) &&
                !model.TryGet((KeyboardAction)((int)KeyboardAction.RecallCamera1 + slot), out chord),
                "Camera bookmarks F5-F8 must default to unbound.");
        }
        Assert(model.TryGet(KeyboardAction.RecallCamera1, out chord) && chord.KeyCode == 282,
            "RecallCamera1 must default to F1.");
        Assert(model.TryGet(KeyboardAction.ProduceSlot1, out chord) && chord.KeyCode == 113,
            "ProduceSlot1 must default to Q.");
        Assert(model.TryGet(KeyboardAction.ProduceSlot8, out chord) && chord.KeyCode == 105,
            "ProduceSlot8 must default to I.");
        Assert(model.TryGet(KeyboardAction.SelectMercenaryPost, out chord) && chord.KeyCode == 52,
            "Mercenary-post selection must default to 4.");
        Assert(model.TryGet(KeyboardAction.SelectBedouinStockade, out chord) && chord.KeyCode == 53,
            "Bedouin-stockade selection must default to 5.");
        Assert(model.TryGet(KeyboardAction.SelectBarracks, out chord) && chord.KeyCode == 54,
            "Barracks selection must default to 6.");
        Assert(model.TryGet(KeyboardAction.SelectEngineersGuild, out chord) && chord.KeyCode == 286,
            "Engineers-guild selection must default to F5.");
        Assert(model.TryGet(KeyboardAction.SelectTunnelersGuild, out chord) && chord.KeyCode == 287,
            "Tunnelers-guild selection must default to F6.");
        Assert(model.TryGet(KeyboardAction.SelectCathedral, out chord) && chord.KeyCode == 288,
            "Cathedral selection must default to F7.");
        Assert(model.TryGet(KeyboardAction.BuildPageCastle, out chord) && chord.KeyCode == 122 &&
            model.TryGet(KeyboardAction.BuildPageFood, out chord) && chord.KeyCode == 110,
            "Build pages must span Z through N.");
        Assert(model.TryGet(KeyboardAction.BuildSlot1, out chord) && chord.KeyCode == 113 &&
            model.TryGet(KeyboardAction.BuildSlot10, out chord) && chord.KeyCode == 112,
            "Build slots must span Q through P.");
        Assert(model.TryGet(KeyboardAction.PauseGame, out chord) && chord.KeyCode == 47,
            "Pause must default to Slash (/).");
        Assert(model.TryGet(KeyboardAction.CenterOnKeep, out chord) && chord.KeyCode == 103,
            "Center-on-keep must default to G.");
        Assert(model.TryGet(KeyboardAction.FlattenLandscape, out chord) && chord.KeyCode == 96,
            "Flatten-landscape must default to BackQuote.");
        Assert(model.TryGet(KeyboardAction.OpenChat, out chord) && chord.KeyCode == 13,
            "Chat must default to Enter.");
        Assert(model.TryGet(KeyboardAction.MultiplayerPing, out chord) && chord.KeyCode == 326,
            "Multiplayer position marker must default to Mouse 4.");
        Assert(model.TryGet(KeyboardAction.SelectAllMilitary, out chord) && chord.KeyCode == 32,
            "Nearby military selection must keep its Space default.");
        Assert(model.TryGet(KeyboardAction.SelectAllMilitaryMap, out chord) && chord.KeyCode == 97 && chord.Ctrl,
            "Map-wide military selection must default to Ctrl+A.");
        Assert(model.TryGet(KeyboardAction.StopUnits, out chord) && chord.KeyCode == 115 && !chord.Ctrl,
            "Stop must default to S independently of movement Mods.");
        Assert(model.TryGet(KeyboardAction.PatrolUnits, out chord) && chord.KeyCode == 102 && !chord.Ctrl,
            "Patrol must default to F.");
        foreach (KeyboardAction action in new[] { KeyboardAction.StopUnits, KeyboardAction.PatrolUnits })
        {
            Assert(BindingMigrationPlan.ShouldApplyNewDefault(4, action, ""), "Repair previous blank troop commands once.");
            Assert(!BindingMigrationPlan.ShouldApplyNewDefault(5, action, ""), "Later intentional unbinding must persist.");
            Assert(!BindingMigrationPlan.ShouldApplyNewDefault(4, action, "120,0,0,0"), "Keep custom troop bindings.");
        }
        Assert(BindingConflictPolicy.ContextFor(KeyboardAction.StopUnits) == HotkeyContext.TroopCommand &&
               BindingConflictPolicy.ContextFor(KeyboardAction.PatrolUnits) == HotkeyContext.TroopCommand,
            "Stop and patrol are troop commands, not global shortcuts.");
        foreach (KeyboardAction action in new[] { KeyboardAction.CenterOnKeep, KeyboardAction.SelectAllMilitaryMap })
        {
            int oldKey = action == KeyboardAction.CenterOnKeep ? 102 : 103;
            string oldDefault = new KeyChord(oldKey, false, false, false).Serialize();
            Assert(BindingMigrationPlan.ShouldApplyNewDefault(3, action, oldDefault), "Old defaults migrate once.");
            Assert(!BindingMigrationPlan.ShouldApplyNewDefault(4, action, oldDefault), "Schema 4 preserves intentional old keys.");
            Assert(!BindingMigrationPlan.ShouldApplyNewDefault(3, action, "") &&
                   !BindingMigrationPlan.ShouldApplyNewDefault(3, action, "120,1,0,0"), "Keep unbound and custom keys.");
        }
        Assert(model.TryGet(KeyboardAction.SelectLord, out chord) && chord.KeyCode == 108,
            "SelectLord must default to L.");
        Assert(model.TryGet(KeyboardAction.ToggleFrameRate, out chord) && chord.KeyCode == 93,
            "ToggleFrameRate must default to RightBracket (]).");
        Assert(model.TryGet(KeyboardAction.IncreaseGameSpeed, out chord) &&
            chord.KeyCode == 61 && chord.Shift,
            "IncreaseGameSpeed must default to main-keyboard Plus (Shift+Equals). ");
        Assert(model.TryGet(KeyboardAction.IncreaseGameSpeedKeypad, out chord) && chord.KeyCode == 270,
            "IncreaseGameSpeedKeypad must default to KeypadPlus.");
        Assert(model.TryGet(KeyboardAction.DecreaseGameSpeed, out chord) && chord.KeyCode == 45,
            "DecreaseGameSpeed must default to main-keyboard Minus.");
        Assert(model.TryGet(KeyboardAction.DecreaseGameSpeedKeypad, out chord) && chord.KeyCode == 269,
            "DecreaseGameSpeedKeypad must default to KeypadMinus.");
        Assert(model.TryGet(KeyboardAction.AttackMove, out chord) && chord.KeyCode == 97 &&
            !chord.Ctrl && !chord.Shift && !chord.Alt,
            "Optional hybrid-movement attack move must default to A.");

        KeyChord replacement = new KeyChord(109, true, false, false);
        KeyboardAction? displaced = model.Set(KeyboardAction.SelectGroup1, replacement);
        Assert(!displaced.HasValue, "Fresh Ctrl+M binding should not displace an action.");
        displaced = model.Set(KeyboardAction.SelectGroup2, replacement);
        Assert(displaced == KeyboardAction.SelectGroup1, "A duplicate chord must displace the old action.");
        Assert(!model.TryGet(KeyboardAction.SelectGroup1, out chord), "Displaced action stayed bound.");

        BindingModel contextual = new BindingModel();
        KeyChord contextualQ = new KeyChord(113, false, false, false);
        Assert(!contextual.Set(KeyboardAction.ProduceSlot1, contextualQ).HasValue &&
            !contextual.Set(KeyboardAction.BuildSlot1, contextualQ).HasValue,
            "Mutually exclusive production and build contexts must share a chord.");
        Assert(contextual.TryGet(KeyboardAction.ProduceSlot1, out chord) &&
            contextual.TryGet(KeyboardAction.BuildSlot1, out chord),
            "A contextual duplicate unexpectedly removed one action.");
        Assert(contextual.Set(KeyboardAction.CameraLeft, contextualQ).HasValue,
            "A global action must displace contextual actions using the same chord.");
        Assert(!contextual.TryGet(KeyboardAction.ProduceSlot1, out chord) &&
            !contextual.TryGet(KeyboardAction.BuildSlot1, out chord),
            "A global collision did not clear every conflicting contextual action.");

        BindingModel copied = new BindingModel();
        copied.CopyFrom(model);
        Assert(copied.TryGet(KeyboardAction.SelectGroup2, out chord) && chord.Equals(replacement),
            "Draft binding copy did not preserve the replacement chord.");
        model.Remove(KeyboardAction.SelectGroup2);
        Assert(copied.TryGet(KeyboardAction.SelectGroup2, out chord),
            "Draft binding copy must not share mutable state with the source.");

        BindingModel active = new BindingModel();
        active.ResetDefaults();
        BindingModel draft = new BindingModel();
        draft.CopyFrom(active);
        KeyChord draftCamera = new KeyChord(113, false, false, false);
        draft.Set(KeyboardAction.CameraLeft, draftCamera);
        Assert(active.TryGet(KeyboardAction.CameraLeft, out chord) && chord.KeyCode == 276,
            "A draft edit changed the active bindings before save.");
        active.CopyFrom(draft);
        Assert(active.TryGet(KeyboardAction.CameraLeft, out chord) && chord.Equals(draftCamera),
            "Saving a draft did not atomically replace the active bindings.");

        string serialized = replacement.Serialize();
        KeyChord parsed;
        Assert(KeyChord.TryParse(serialized, out parsed) && parsed.Equals(replacement),
            "Chord serialization did not round-trip.");
        Assert(!KeyChord.TryParse("invalid", out parsed), "Invalid chord text was accepted.");
        Assert(new KeyChord(49, true, true, true).ToNativeEncoding() == 0x70031,
            "Native modifier encoding is incorrect.");

        NativeSelectionPhaseSpec selectionPhase = NativeSelectionPlan.Phase(0);
        Assert(selectionPhase.MouseState == 1 && selectionPhase.CurrentAction == 8 &&
            !selectionPhase.UseSelectedUnits && !selectionPhase.SelectionOn &&
            !selectionPhase.SelectionEstablished && !selectionPhase.TroopSelectionBoxOn &&
            selectionPhase.UseOnScreenUnits,
            "Native selection press phase does not match the game's input transaction.");
        selectionPhase = NativeSelectionPlan.Phase(1);
        Assert(selectionPhase.MouseState == 2 && selectionPhase.CurrentAction == 8 &&
            selectionPhase.UseSelectedUnits && selectionPhase.SelectionOn &&
            selectionPhase.SelectionEstablished && !selectionPhase.TroopSelectionBoxOn &&
            !selectionPhase.UseOnScreenUnits,
            "Native selection drag phase does not match the game's input transaction.");
        selectionPhase = NativeSelectionPlan.Phase(2);
        Assert(selectionPhase.MouseState == 3 && selectionPhase.CurrentAction == 9 &&
            selectionPhase.UseSelectedUnits && !selectionPhase.SelectionOn &&
            !selectionPhase.SelectionEstablished && selectionPhase.TroopSelectionBoxOn &&
            !selectionPhase.UseOnScreenUnits,
            "Native selection release phase does not match the game's input transaction.");
        bool rejectedInvalidSelectionPhase = false;
        try
        {
            NativeSelectionPlan.Phase(NativeSelectionPlan.PhaseCount);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedInvalidSelectionPhase = true;
        }
        Assert(rejectedInvalidSelectionPhase, "An invalid native selection phase was accepted.");
        Assert(NativeSelectionVerification.SelectedUnitsAreCandidateSubset(
            new[] { 11, 12, 13, 14 }, new[] { 11, 14 }),
            "Native owner filtering must be allowed to return a subset of military candidates.");
        Assert(!NativeSelectionVerification.SelectedUnitsAreCandidateSubset(
            new[] { 11, 12, 13 }, new[] { 11, 99 }),
            "A native selection containing a non-candidate unit was accepted.");
        Assert(MilitarySelectionPlan.IsMapWideCandidate(11, true, 1, 1, 22),
            "An active player-owned military unit was rejected from map-wide selection.");
        Assert(MilitarySelectionPlan.IsNativeUnitCountValid(230, 10000) &&
               !MilitarySelectionPlan.IsNativeUnitCountValid(0, 10000) &&
               !MilitarySelectionPlan.IsNativeUnitCountValid(10001, 10000),
            "Native unit-table count guard accepted an unavailable or out-of-range table.");
        Assert(!MilitarySelectionPlan.IsMapWideCandidate(11, true, 1, 1, 55),
            "The lord must be excluded from map-wide military selection.");
        Assert(!MilitarySelectionPlan.IsMapWideCandidate(11, true, 1, 1, 1) &&
               !MilitarySelectionPlan.IsMapWideCandidate(11, true, 2, 1, 22) &&
               !MilitarySelectionPlan.IsMapWideCandidate(11, false, 1, 1, 22) &&
               !MilitarySelectionPlan.IsMapWideCandidate(0, true, 1, 1, 22),
            "Workers, enemies, inactive records, and unit zero must not enter map-wide selection.");
        Assert(!LordSelectionPlan.IsDoubleClick(-1f, 1f) &&
            LordSelectionPlan.IsDoubleClick(1f, 1f + LordSelectionPlan.DoubleClickSeconds) &&
            !LordSelectionPlan.IsDoubleClick(1f, 1.36f),
            "Lord single/double-press timing boundaries are incorrect.");
        Assert(ControlGroupPlan.Merge(
                new[] { 12, 11, 12, -1 }, new[] { 14, 13, 11, 0 })
                .SequenceEqual(new[] { 11, 12, 13, 14 }),
            "Adding units to a control group must preserve every old member, add every new member, and leave no invalid or duplicate ID.");
        Assert(ControlGroupPlan.Merge(null, new[] { 9, 8 })
                .SequenceEqual(new[] { 8, 9 }),
            "Adding units to an empty control group must create a deterministic group.");

        NativeCommandSpec command = NativeCommandPlan.Group(0, NativeGroupMode.Replace);
        Assert(command.Function == 20 && command.Value1 == -1 && command.Value2 == 0 && command.Value3 == 0,
            "Native group 1 replace command is incorrect.");
        command = NativeCommandPlan.Group(4, NativeGroupMode.Delete);
        Assert(command.Function == 24 && command.Value1 == 20 && command.Value2 == 0,
            "Native group delete mode must be passed as value1.");
        command = NativeCommandPlan.SelectGroup(9);
        Assert(command.Function == 29 && command.Value1 == -1 && command.Value2 == 0,
            "Native group 10 select command is incorrect.");

        Assert(BarracksRallyPlan.MappersForMode(1).SequenceEqual(
            new[] { 332, 334, 335, 333, 336, 337, 338 }),
            "Barracks assembly-point mapping is incorrect.");
        Assert(BarracksRallyPlan.MappersForMode(44).SequenceEqual(
            new[] { 360, 362, 364, 363, 361, 366, 365 }),
            "Mercenary-post assembly-point mapping is incorrect.");
        Assert(BarracksRallyPlan.MappersForMode(23).SequenceEqual(new[] { 367, 368 }),
            "Engineers-guild assembly-point mapping is incorrect.");
        Assert(BarracksRallyPlan.MappersForMode(24).SequenceEqual(new[] { 369 }),
            "Tunnelers-guild assembly-point mapping is incorrect.");
        Assert(BarracksRallyPlan.MappersForMode(96).SequenceEqual(new[] { 370 }),
            "Cathedral assembly-point mapping is incorrect.");
        Assert(BarracksRallyPlan.MappersForMode(63).SequenceEqual(
            new[] { 395, 397, 391, 396, 398, 393, 392, 394 }),
            "Bedouin-stockade assembly-point mapping is incorrect.");
        Assert(BarracksRallyPlan.MappersForMode(0) == null,
            "Unsupported buildings must not receive barracks assembly points.");
        Assert(ReferenceEquals(BarracksRallyPlan.MappersForMode(1),
            BarracksRallyPlan.MappersForMode(1)),
            "Barracks rally mappings must be cached instead of allocated on every lookup.");

        Assert(BarracksProductionPlan.ChimpTypesForMode(1).SequenceEqual(
            new[] { 22, 24, 26, 23, 25, 27, 28 }),
            "Barracks production order is incorrect.");
        Assert(BarracksProductionPlan.ChimpTypesForMode(44).SequenceEqual(
            new[] { 70, 71, 72, 73, 74, 75, 76 }),
            "Mercenary-post production order is incorrect.");
        Assert(BarracksProductionPlan.ChimpTypesForMode(63).SequenceEqual(
            new[] { 78, 79, 80, 81, 82, 83, 84, 85 }),
            "Bedouin-stockade production order is incorrect.");
        Assert(BarracksProductionPlan.ChimpTypesForMode(23).SequenceEqual(new[] { 29, 30 }),
            "Engineers-guild production order is incorrect.");
        Assert(BarracksProductionPlan.ChimpTypesForMode(24).SequenceEqual(new[] { 5 }),
            "Tunnelers-guild production order is incorrect.");
        Assert(BarracksProductionPlan.ChimpTypesForMode(96).SequenceEqual(new[] { 37 }),
            "Cathedral production order is incorrect.");
        Assert(BarracksProductionPlan.ButtonFieldsForMode(63).Length == 8,
            "The largest production panel must expose eight shortcut hints.");
        Assert(ReferenceEquals(BarracksProductionPlan.ChimpTypesForMode(63),
                BarracksProductionPlan.ChimpTypesForMode(63)) &&
            ReferenceEquals(BarracksProductionPlan.ButtonFieldsForMode(63),
                BarracksProductionPlan.ButtonFieldsForMode(63)),
            "Production mappings must be cached instead of allocated during each HUD draw.");
        Assert(BarracksProductionPlan.ChimpTypesForMode(0) == null &&
            BarracksProductionPlan.ButtonFieldsForMode(0) == null,
            "Unsupported buildings must not expose production shortcuts.");

        Assert(BarracksSelectionPlan.FunctionForAction(KeyboardAction.SelectMercenaryPost) == 97 &&
            BarracksSelectionPlan.FunctionForAction(KeyboardAction.SelectBedouinStockade) == 99 &&
            BarracksSelectionPlan.FunctionForAction(KeyboardAction.SelectBarracks) == 9,
            "First three barracks-selection actions do not map to their native functions.");
        Assert(BarracksSelectionPlan.FunctionForAction(KeyboardAction.SelectEngineersGuild) == 118 &&
            BarracksSelectionPlan.FunctionForAction(KeyboardAction.SelectTunnelersGuild) == 117 &&
            BarracksSelectionPlan.FunctionForAction(KeyboardAction.SelectCathedral) == 201,
            "Last three barracks-selection actions do not map to their native functions.");
        Assert(BarracksSelectionPlan.BuildingModeForAction(KeyboardAction.SelectMercenaryPost) == 44 &&
            BarracksSelectionPlan.MapperForAction(KeyboardAction.SelectMercenaryPost) == 86 &&
            BarracksSelectionPlan.BuildingModeForAction(KeyboardAction.SelectCathedral) == 96 &&
            BarracksSelectionPlan.MapperForAction(KeyboardAction.SelectCathedral) == 97,
            "Barracks select-or-build fallback mappings are incorrect.");
        Assert(BarracksSelectionPlan.NativeSelectorForAction(KeyboardAction.SelectMercenaryPost) == 8 &&
            BarracksSelectionPlan.NativeSelectorForAction(KeyboardAction.SelectBedouinStockade) == 108 &&
            BarracksSelectionPlan.NativeSelectorForAction(KeyboardAction.SelectBarracks) == 9 &&
            BarracksSelectionPlan.NativeSelectorForAction(KeyboardAction.SelectEngineersGuild) == 24 &&
            BarracksSelectionPlan.NativeSelectorForAction(KeyboardAction.SelectTunnelersGuild) == 25 &&
            BarracksSelectionPlan.NativeSelectorForAction(KeyboardAction.SelectCathedral) == 38,
            "Barracks hotkeys must reproduce the game's native 1047 selector values.");
        Assert(BarracksSelectionPlan.ActionForFunction(99) == KeyboardAction.SelectBedouinStockade,
            "Native barracks function reverse mapping is incorrect.");
        Assert(BarracksSelectionFallbackPlan.NativeSelectionWaitFrames == 120 &&
            BarracksSelectionFallbackPlan.IsExpectedSelection(16, 44, 44) &&
            !BarracksSelectionFallbackPlan.IsExpectedSelection(16, 1, 44) &&
            !BarracksSelectionFallbackPlan.IsExpectedSelection(0, 44, 44),
            "Barracks selection fallback must wait for the expected native building panel.");
        Assert(!BarracksSelectionFallbackPlan.CanIssueNativeSelection(true, 0) &&
            !BarracksSelectionFallbackPlan.CanIssueNativeSelection(false, 1) &&
            BarracksSelectionFallbackPlan.CanIssueNativeSelection(false, 0),
            "Barracks selection must wait for the synchronized selection transaction and troop selection to clear.");
        Assert(BarracksSelectionFallbackPlan.ShouldEnterPlacement(true, false) &&
            !BarracksSelectionFallbackPlan.ShouldEnterPlacement(true, true) &&
            !BarracksSelectionFallbackPlan.ShouldEnterPlacement(false, false),
            "Barracks placement must require positive proof that the owned building is absent.");

        StockpilePoint[] oneArea =
        {
            new StockpilePoint(8, 1, 662, 451),
            new StockpilePoint(9, 1, 665, 451),
            new StockpilePoint(10, 1, 662, 454),
            new StockpilePoint(11, 1, 665, 454)
        };
        Assert(GiftedStockpileRemovalPlan.CountAreas(oneArea) == 1 &&
            GiftedStockpileRemovalPlan.ShouldAttempt(true, 1),
            "Four touching native stockpile components must count as one removable area.");
        StockpilePoint[] twoAreas =
        {
            oneArea[0], oneArea[1], oneArea[2], oneArea[3],
            new StockpilePoint(12, 1, 700, 700)
        };
        Assert(GiftedStockpileRemovalPlan.CountAreas(twoAreas) == 2 &&
            !GiftedStockpileRemovalPlan.ShouldAttempt(true, 2) &&
            !GiftedStockpileRemovalPlan.ShouldAttempt(false, 1) &&
            !GiftedStockpileRemovalPlan.ShouldAttempt(true, 0),
            "Saves, missing stockpiles, and owners with multiple independent areas must be preserved.");
        Assert(GiftedStockpileRemovalPlan.CountAreas(new[]
            {
                new StockpilePoint(1, 1, 100, 100),
                new StockpilePoint(2, 2, 100, 100)
            }) == 2,
            "Touching stockpiles owned by different players must remain separate areas.");
        StockpilePoint[] mixedOwners = oneArea.Concat(new[]
        {
            new StockpilePoint(20, 2, 200, 200),
            new StockpilePoint(21, 3, 300, 300),
            new StockpilePoint(22, 3, 400, 400)
        }).ToArray();
        Assert(GiftedStockpileRemovalPlan.OwnersWithOneArea(mixedOwners, true)
                   .SequenceEqual(new[] { 1, 2 }) &&
               GiftedStockpileRemovalPlan.OwnersWithOneArea(mixedOwners, false).Length == 0 &&
               GiftedStockpileRemovalPlan.OwnersWithOneArea(
                   new StockpilePoint[0], true).Length == 0,
            "Direct native cleanup must select only owners with exactly one stockpile area, in deterministic order, and remain disabled for saves.");

        int configuredRadius;
        Assert(EnemyExclusionRadiusPatchPlan.TryParseRadius(
                   "[building_placement]\nenemy_exclusion_radius = 83 # test", out configuredRadius) &&
               configuredRadius == 83 &&
               !EnemyExclusionRadiusPatchPlan.TryParseRadius(
                   "enemy_exclusion_radius = 0", out configuredRadius) &&
               !EnemyExclusionRadiusPatchPlan.TryParseRadius(
                   "enemy_exclusion_radius = 128", out configuredRadius),
            "The TOML radius must parse inside the one-byte native instruction range only.");
        NativePatchSpec[] exclusionPatches =
            EnemyExclusionRadiusPatchPlan.CreateSpecs(83);
        Assert(exclusionPatches.Length == 8 &&
            exclusionPatches.All(patch => patch.Original.Length == patch.Patched.Length),
            "The enemy building/unit exclusion patch must lock all eight same-length native sites.");
        Assert(exclusionPatches.Any(patch => patch.Rva == 0x77DBB &&
                   patch.Original[4] == 5 && patch.Patched[4] == 83) &&
            exclusionPatches.Any(patch => patch.Rva == 0xEE8E8 &&
                   patch.Patched.SequenceEqual(new byte[] { 0x83, 0xFE, 0x53 })),
            "The final unit-distance guard and existing enemy-building guard must both use the TOML radius.");

        BuildingPlacementSettings placementSettings;
        Assert(BuildingPlacementPolicyPlan.TryParse(
                   "[building_placement_policy]\n" +
                   "active_option = 2\n" +
                   "option_1_enemy_exclusion_radius = 71\n" +
                   "option_2_opening_lord_radius = 141\n" +
                   "option_2_duration_minutes = 6", out placementSettings) &&
               placementSettings.Policy == BuildingPlacementPolicy.OpeningLordRadius &&
               placementSettings.EnemyExclusionRadius == 71 &&
               placementSettings.OpeningLordRadius == 141 &&
               placementSettings.OpeningDurationMinutes == 6,
            "The two-option building-placement TOML did not parse deterministically.");
        Assert(!BuildingPlacementPolicyPlan.TryParse(
                   "active_option = 3\n" +
                   "option_1_enemy_exclusion_radius = 70\n" +
                   "option_2_opening_lord_radius = 140\n" +
                   "option_2_duration_minutes = 5", out placementSettings),
            "Building placement must reject an option other than 1 or 2.");
        Assert(!BuildingPlacementPolicyPlan.TryParse(
                   "active_option = 2\n" +
                   "option_1_enemy_exclusion_radius = 128\n" +
                   "option_2_opening_lord_radius = 140\n" +
                   "option_2_duration_minutes = 5", out placementSettings),
            "Option 1 must keep the locked native one-byte radius range.");
        Assert(BuildingPlacementPolicyPlan.Default.Policy ==
                   BuildingPlacementPolicy.OpeningLordRadius &&
               BuildingPlacementPolicyPlan.Default.EnemyExclusionRadius == 70 &&
               BuildingPlacementPolicyPlan.Default.OpeningLordRadius == 140 &&
               BuildingPlacementPolicyPlan.Default.OpeningDurationMinutes == 5,
            "Option 2 with a 140-tile, five-minute opening must be the default.");
        Assert(BuildingPlacementPolicyPlan.ShouldApplyEnemyPatch(
                   BuildingPlacementPolicy.EnemyExclusion) &&
               !BuildingPlacementPolicyPlan.ShouldApplyEnemyPatch(
                   BuildingPlacementPolicy.OpeningLordRadius),
            "Only option 1 may replace the game's native enemy-distance rules.");
        Assert(BuildingPlacementPolicyPlan.IsOpeningWindow(11999, 5) &&
               !BuildingPlacementPolicyPlan.IsOpeningWindow(12000, 5) &&
               !BuildingPlacementPolicyPlan.IsOpeningWindow(-1, 5),
            "The five-minute opening window must use 40 simulation ticks per second.");
        Assert(BuildingPlacementPolicyPlan.IsWithinLordRadius(
                   400.5f, 400.5f, 540.5f, 400.5f, 140) &&
               !BuildingPlacementPolicyPlan.IsWithinLordRadius(
                   400.5f, 400.5f, 541.5f, 400.5f, 140),
            "The opening placement boundary must be measured in logic tiles from the frozen lord position.");
        Assert(BuildingPlacementPolicyPlan.ShouldRestrictCurrentAction(5) &&
               !BuildingPlacementPolicyPlan.ShouldRestrictCurrentAction(0) &&
               !BuildingPlacementPolicyPlan.ShouldRestrictCurrentAction(6) &&
               !BuildingPlacementPolicyPlan.ShouldRestrictCurrentAction(7),
            "The opening radius may restrict only the game's actual building-placement action.");
        Assert(BuildingPlacementPolicyPlan.ShouldHoldNativeFeedback(10f, 10f) &&
               BuildingPlacementPolicyPlan.ShouldHoldNativeFeedback(9.9f, 10f) &&
               !BuildingPlacementPolicyPlan.ShouldHoldNativeFeedback(10.01f, 10f) &&
               !BuildingPlacementPolicyPlan.ShouldHoldNativeFeedback(0f, -1f),
            "Opening placement feedback must survive same-frame native UI refreshes and expire afterward.");
        Assert(BuildingPlacementPolicyPlan.NativeTooFarPanelSection == 77 &&
               BuildingPlacementPolicyPlan.NativeTooFarText == 15,
            "A blocked opening placement must reuse the game's localized too-far-from-castle feedback.");

        int logicX;
        int logicY;
        string migrationRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scde-keyboard-config-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(migrationRoot);
        try
        {
            string oldConfig = System.IO.Path.Combine(migrationRoot, "old.cfg");
            string persistentConfig = System.IO.Path.Combine(migrationRoot, "persistent.cfg");
            System.IO.File.WriteAllText(oldConfig, "player-custom-bindings");
            PersistentConfigurationPlan.CopyIfMissing(oldConfig, persistentConfig);
            Assert(System.IO.File.ReadAllText(persistentConfig) == "player-custom-bindings",
                "Moving config outside the game clone must preserve the player's existing bindings.");
            System.IO.File.WriteAllText(oldConfig, "stale-clone-defaults");
            PersistentConfigurationPlan.CopyIfMissing(oldConfig, persistentConfig);
            Assert(System.IO.File.ReadAllText(persistentConfig) == "player-custom-bindings",
                "A rebuilt game clone must never overwrite established persistent bindings.");
        }
        finally { System.IO.Directory.Delete(migrationRoot, true); }
        Assert(Math.Abs(RadarProjectionPlan.NormalizeNoesisY(0f, 101) - 1f) < 0.0001f &&
               Math.Abs(RadarProjectionPlan.NormalizeNoesisY(50f, 101) - 0.5f) < 0.0001f &&
               Math.Abs(RadarProjectionPlan.NormalizeNoesisY(100f, 101)) < 0.0001f,
            "Noesis top-left radar Y must be converted to the bottom-left projected Y axis exactly once.");
        Assert(RadarProjectionPlan.TryInverseProjectedPoint(
                   0f, 0f, 100f, 0f, 0f, 100f,
                   25f, 75f, 25f, 75f,
                   0.5f, 0.5f, 0, 100, 0, 100,
                   out logicX, out logicY) && logicX == 50 && logicY == 50,
            "The radar projection center did not map to the center logic tile.");
        Assert(RadarProjectionPlan.TryInverseProjectedPoint(
                   0f, 0f, 100f, 0f, 0f, 100f,
                   25f, 75f, 25f, 75f,
                   0f, 1f, 0, 100, 0, 100,
                   out logicX, out logicY) && logicX == 25 && logicY == 75,
            "The radar projection edges did not preserve the cropped isometric basis.");
        Assert(!RadarProjectionPlan.TryInverseProjectedPoint(
                   0f, 0f, 100f, 0f, 200f, 0f,
                   25f, 75f, 25f, 75f,
                   0.5f, 0.5f, 0, 100, 0, 100,
                   out logicX, out logicY),
            "A degenerate radar projection must fail instead of producing a bogus order.");
        for (int rotation = 0; rotation < 4; rotation++)
        {
            float angle = (float)(rotation * Math.PI / 2.0);
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            float ux = 100f * c - 50f * s, uy = 100f * s + 50f * c;
            float vx = -100f * c - 50f * s, vy = -100f * s + 50f * c;
            float minX = Math.Min(0f, Math.Min(ux, Math.Min(vx, ux + vx)));
            float maxX = Math.Max(0f, Math.Max(ux, Math.Max(vx, ux + vx)));
            float minY = Math.Min(0f, Math.Min(uy, Math.Min(vy, uy + vy)));
            float maxY = Math.Max(0f, Math.Max(uy, Math.Max(vy, uy + vy)));
            float worldX = ux * 0.7f + vx * 0.4f;
            float worldY = uy * 0.7f + vy * 0.4f;
            float noesisY = (1f - (worldY - minY) / (maxY - minY)) * 100f;
            Assert(RadarProjectionPlan.TryInverseProjectedPoint(
                0f, 0f, ux, uy, vx, vy, minX, maxX, minY, maxY,
                (worldX - minX) / (maxX - minX), RadarProjectionPlan.NormalizeNoesisY(noesisY, 101),
                250, 350, 250, 350, out logicX, out logicY) && logicX == 320 && logicY == 290,
                "Radar UI-to-logic inversion failed for rotation " + rotation);
        }
        Assert(BuildMenuPlan.FirstButtonIndex(0) == 1 &&
            BuildMenuPlan.FirstButtonIndex(1) == 0 &&
            BuildMenuPlan.FirstButtonIndex(7) == 1 &&
            BuildMenuPlan.FirstButtonIndex(11) == 1 &&
            BuildMenuPlan.FirstButtonIndex(13) == 1 &&
            BuildMenuPlan.FirstButtonIndex(14) == 1,
            "Build subpages must skip their leading return button.");
        Assert(BindingMigrationPlan.LegacyDefaultForAction(KeyboardAction.SelectGroup4) ==
            new KeyChord(52, false, false, false).Serialize() &&
            BindingMigrationPlan.LegacyDefaultForAction(KeyboardAction.SaveCamera8) ==
            new KeyChord(289, true, false, false).Serialize() &&
            BindingMigrationPlan.LegacyDefaultForAction(KeyboardAction.SelectCathedral) ==
            new KeyChord(108, false, false, false).Serialize(),
            "The 0.1.16 defaults selected for migration are incorrect.");
        Assert(BindingMigrationPlan.LegacyDefaultForAction(KeyboardAction.SelectGroup3) == null &&
            BindingMigrationPlan.LegacyDefaultForAction(KeyboardAction.SaveCamera4) == null &&
            BindingMigrationPlan.LegacyDefaultForAction(KeyboardAction.BuildSlot1) == null,
            "Migration must not rewrite unaffected or newly added actions.");
        Assert(BindingMigrationPlan.ShouldApplyNewDefault(
                   2, KeyboardAction.PauseGame, "") &&
               !BindingMigrationPlan.ShouldApplyNewDefault(
                   3, KeyboardAction.PauseGame, "") &&
               !BindingMigrationPlan.ShouldApplyNewDefault(
                   2, KeyboardAction.PauseGame, "47:0:0:0") &&
               !BindingMigrationPlan.ShouldApplyNewDefault(
                   2, KeyboardAction.SelectAllMilitaryMap, ""),
            "The new Slash pause default must migrate only the previous unbound default once.");

        if (args.Length > 0)
        {
            BuildingPlacementSettings packed;
            Assert(BuildingPlacementPolicyPlan.TryParse(System.IO.File.ReadAllText(args[0]), out packed),
                "Build must reject invalid source rules before packaging.");
            Console.WriteLine("PACKED_RULES_VALID option=" + (int)packed.Policy + " enemy=" + packed.EnemyExclusionRadius +
                " lord=" + packed.OpeningLordRadius + " minutes=" + packed.OpeningDurationMinutes);
        }
        Console.WriteLine("KEYBOARD_BINDING_MODEL_OK assertions=" + _assertions);
        return 0;
    }
}
