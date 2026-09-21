using System;
using System.Collections.Generic;
using System.Globalization;

namespace SCDEKeyboardControl
{
    internal enum KeyboardAction
    {
        CameraLeft,
        CameraRight,
        CameraUp,
        CameraDown,

        ReplaceGroup1,
        ReplaceGroup2,
        ReplaceGroup3,
        ReplaceGroup4,
        ReplaceGroup5,
        ReplaceGroup6,
        ReplaceGroup7,
        ReplaceGroup8,
        ReplaceGroup9,
        ReplaceGroup10,

        SelectGroup1,
        SelectGroup2,
        SelectGroup3,
        SelectGroup4,
        SelectGroup5,
        SelectGroup6,
        SelectGroup7,
        SelectGroup8,
        SelectGroup9,
        SelectGroup10,

        AddGroup1,
        AddGroup2,
        AddGroup3,
        AddGroup4,
        AddGroup5,
        AddGroup6,
        AddGroup7,
        AddGroup8,
        AddGroup9,
        AddGroup10,

        ExclusiveGroup1,
        ExclusiveGroup2,
        ExclusiveGroup3,
        ExclusiveGroup4,
        ExclusiveGroup5,
        ExclusiveGroup6,
        ExclusiveGroup7,
        ExclusiveGroup8,
        ExclusiveGroup9,
        ExclusiveGroup10,

        SaveCamera1,
        SaveCamera2,
        SaveCamera3,
        SaveCamera4,
        SaveCamera5,
        SaveCamera6,
        SaveCamera7,
        SaveCamera8,

        RecallCamera1,
        RecallCamera2,
        RecallCamera3,
        RecallCamera4,
        RecallCamera5,
        RecallCamera6,
        RecallCamera7,
        RecallCamera8,

        ProduceSlot1,
        ProduceSlot2,
        ProduceSlot3,
        ProduceSlot4,
        ProduceSlot5,
        ProduceSlot6,
        ProduceSlot7,
        ProduceSlot8,

        SelectMercenaryPost,
        SelectBedouinStockade,
        SelectBarracks,
        SelectEngineersGuild,
        SelectTunnelersGuild,
        SelectCathedral,

        BuildPageCastle,
        BuildPageIndustry,
        BuildPageFarms,
        BuildPageTown,
        BuildPageWeapons,
        BuildPageFood,

        BuildSlot1,
        BuildSlot2,
        BuildSlot3,
        BuildSlot4,
        BuildSlot5,
        BuildSlot6,
        BuildSlot7,
        BuildSlot8,
        BuildSlot9,
        BuildSlot10,

        PauseGame,
        CenterOnKeep,
        FlattenLandscape,
        OpenChat,
        MultiplayerPing,

        SelectAllMilitary,
        SelectAllMilitaryMap,
        SelectLord,
        ToggleFrameRate,
        IncreaseGameSpeed,
        IncreaseGameSpeedKeypad,
        DecreaseGameSpeed,
        DecreaseGameSpeedKeypad,
        AttackMove,
        StopUnits,
        PatrolUnits,
        ToggleGoods
    }

    internal struct KeyChord : IEquatable<KeyChord>
    {
        internal readonly int KeyCode;
        internal readonly bool Ctrl;
        internal readonly bool Shift;
        internal readonly bool Alt;

        internal KeyChord(int keyCode, bool ctrl, bool shift, bool alt)
        {
            KeyCode = keyCode;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        internal int ToNativeEncoding()
        {
            int value = KeyCode & 0xffff;
            if (Shift) value |= 0x10000;
            if (Ctrl) value |= 0x20000;
            if (Alt) value |= 0x40000;
            return value;
        }

        internal string Serialize()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}", KeyCode, Ctrl ? 1 : 0, Shift ? 1 : 0, Alt ? 1 : 0);
        }

        internal static bool TryParse(string value, out KeyChord chord)
        {
            chord = default(KeyChord);
            string[] parts = (value ?? "").Split(',');
            int keyCode;
            int ctrl;
            int shift;
            int alt;
            if (parts.Length != 4 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out keyCode) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ctrl) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out shift) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out alt) ||
                keyCode <= 0 || (ctrl != 0 && ctrl != 1) ||
                (shift != 0 && shift != 1) || (alt != 0 && alt != 1))
            {
                return false;
            }

            chord = new KeyChord(keyCode, ctrl == 1, shift == 1, alt == 1);
            return true;
        }

        public bool Equals(KeyChord other)
        {
            return KeyCode == other.KeyCode && Ctrl == other.Ctrl &&
                   Shift == other.Shift && Alt == other.Alt;
        }

        public override bool Equals(object obj)
        {
            return obj is KeyChord && Equals((KeyChord)obj);
        }

        public override int GetHashCode()
        {
            int hash = KeyCode;
            hash = (hash * 397) ^ (Ctrl ? 1 : 0);
            hash = (hash * 397) ^ (Shift ? 1 : 0);
            hash = (hash * 397) ^ (Alt ? 1 : 0);
            return hash;
        }
    }

    internal sealed class BindingModel
    {
        private readonly Dictionary<KeyboardAction, KeyChord> _bindings =
            new Dictionary<KeyboardAction, KeyChord>();

        internal IEnumerable<KeyValuePair<KeyboardAction, KeyChord>> Bindings
        {
            get { return _bindings; }
        }

        internal void ResetDefaults()
        {
            _bindings.Clear();
            Set(KeyboardAction.CameraLeft, new KeyChord(276, false, false, false));
            Set(KeyboardAction.CameraRight, new KeyChord(275, false, false, false));
            Set(KeyboardAction.CameraUp, new KeyChord(273, false, false, false));
            Set(KeyboardAction.CameraDown, new KeyChord(274, false, false, false));

            for (int slot = 0; slot < 10; slot++)
            {
                if (slot >= 3 && slot <= 5) continue;
                int digit = slot == 9 ? 48 : 49 + slot;
                Set((KeyboardAction)((int)KeyboardAction.ReplaceGroup1 + slot),
                    new KeyChord(digit, true, false, false));
                Set((KeyboardAction)((int)KeyboardAction.SelectGroup1 + slot),
                    new KeyChord(digit, false, false, false));
                Set((KeyboardAction)((int)KeyboardAction.AddGroup1 + slot),
                    new KeyChord(digit, false, true, false));
                Set((KeyboardAction)((int)KeyboardAction.ExclusiveGroup1 + slot),
                    new KeyChord(digit, false, false, true));
            }

            for (int slot = 0; slot < 4; slot++)
            {
                int functionKey = 282 + slot;
                Set((KeyboardAction)((int)KeyboardAction.SaveCamera1 + slot),
                    new KeyChord(functionKey, true, false, false));
                Set((KeyboardAction)((int)KeyboardAction.RecallCamera1 + slot),
                    new KeyChord(functionKey, false, false, false));
            }

            int[] productionKeys = { 113, 119, 101, 114, 116, 121, 117, 105 };
            for (int slot = 0; slot < productionKeys.Length; slot++)
                Set((KeyboardAction)((int)KeyboardAction.ProduceSlot1 + slot),
                    new KeyChord(productionKeys[slot], false, false, false));

            int[] barracksKeys = { 52, 53, 54, 286, 287, 288 };
            for (int slot = 0; slot < barracksKeys.Length; slot++)
                Set((KeyboardAction)((int)KeyboardAction.SelectMercenaryPost + slot),
                    new KeyChord(barracksKeys[slot], false, false, false));

            int[] buildPageKeys = { 122, 120, 99, 118, 98, 110 };
            for (int slot = 0; slot < buildPageKeys.Length; slot++)
                Set((KeyboardAction)((int)KeyboardAction.BuildPageCastle + slot),
                    new KeyChord(buildPageKeys[slot], false, false, false));

            int[] buildSlotKeys = { 113, 119, 101, 114, 116, 121, 117, 105, 111, 112 };
            for (int slot = 0; slot < buildSlotKeys.Length; slot++)
                Set((KeyboardAction)((int)KeyboardAction.BuildSlot1 + slot),
                    new KeyChord(buildSlotKeys[slot], false, false, false));

            Set(KeyboardAction.PauseGame, new KeyChord(47, false, false, false));
            Set(KeyboardAction.CenterOnKeep, new KeyChord(103, false, false, false));
            Set(KeyboardAction.FlattenLandscape, new KeyChord(96, false, false, false));
            Set(KeyboardAction.OpenChat, new KeyChord(13, false, false, false));
            Set(KeyboardAction.MultiplayerPing, new KeyChord(326, false, false, false));

            Set(KeyboardAction.SelectAllMilitary, new KeyChord(32, false, false, false));
            Set(KeyboardAction.SelectAllMilitaryMap, new KeyChord(97, true, false, false));
            Set(KeyboardAction.SelectLord, new KeyChord(108, false, false, false));
            Set(KeyboardAction.ToggleFrameRate, new KeyChord(93, false, false, false));
            Set(KeyboardAction.IncreaseGameSpeed, new KeyChord(61, false, true, false));
            Set(KeyboardAction.IncreaseGameSpeedKeypad, new KeyChord(270, false, false, false));
            Set(KeyboardAction.DecreaseGameSpeed, new KeyChord(45, false, false, false));
            Set(KeyboardAction.DecreaseGameSpeedKeypad, new KeyChord(269, false, false, false));
            Set(KeyboardAction.AttackMove, new KeyChord(97, false, false, false));
            Set(KeyboardAction.StopUnits, new KeyChord(115, false, false, false));
            Set(KeyboardAction.PatrolUnits, new KeyChord(102, false, false, false));
            Set(KeyboardAction.ToggleGoods, new KeyChord(109, false, false, false));
        }

        internal bool TryGet(KeyboardAction action, out KeyChord chord)
        {
            return _bindings.TryGetValue(action, out chord);
        }

        internal KeyboardAction? Set(KeyboardAction action, KeyChord chord)
        {
            KeyboardAction? displaced = null;
            while (true)
            {
                KeyboardAction? conflict = null;
                foreach (KeyValuePair<KeyboardAction, KeyChord> pair in _bindings)
                {
                    if (pair.Key != action && pair.Value.Equals(chord) &&
                        !BindingConflictPolicy.CanShareChord(pair.Key, action))
                    {
                        conflict = pair.Key;
                        break;
                    }
                }
                if (!conflict.HasValue) break;
                if (!displaced.HasValue) displaced = conflict;
                _bindings.Remove(conflict.Value);
            }
            _bindings[action] = chord;
            return displaced;
        }

        internal void Remove(KeyboardAction action)
        {
            _bindings.Remove(action);
        }

        internal void CopyFrom(BindingModel source)
        {
            if (source == null) throw new ArgumentNullException("source");
            _bindings.Clear();
            foreach (KeyValuePair<KeyboardAction, KeyChord> pair in source._bindings)
                _bindings[pair.Key] = pair.Value;
        }
    }

    [Flags]
    internal enum HotkeyContext
    {
        None = 0,
        BarracksProduction = 1,
        BuildMenu = 2,
        TroopCommand = 4,
        AllGameplay = BarracksProduction | BuildMenu | TroopCommand
    }

    internal static class BindingConflictPolicy
    {
        internal static bool CanShareChord(KeyboardAction first, KeyboardAction second)
        {
            return (ContextFor(first) & ContextFor(second)) == HotkeyContext.None;
        }

        internal static HotkeyContext ContextFor(KeyboardAction action)
        {
            if (action >= KeyboardAction.ProduceSlot1 && action <= KeyboardAction.ProduceSlot8)
                return HotkeyContext.BarracksProduction;
            if (action >= KeyboardAction.BuildPageCastle && action <= KeyboardAction.BuildSlot10)
                return HotkeyContext.BuildMenu;
            if (action == KeyboardAction.AttackMove ||
                action == KeyboardAction.StopUnits || action == KeyboardAction.PatrolUnits)
                return HotkeyContext.TroopCommand;
            return HotkeyContext.AllGameplay;
        }
    }

    internal static class BuildMenuPlan
    {
        internal const int MaxSlots = 10;

        internal static int FirstButtonIndex(int buildScreenId)
        {
            return buildScreenId == 0 ||
                   buildScreenId >= 7 && buildScreenId <= 11 ||
                   buildScreenId == 13 || buildScreenId == 14
                ? 1
                : 0;
        }
    }

    internal static class BindingMigrationPlan
    {
        internal static bool ShouldApplyNewDefault(
            int schemaVersion, KeyboardAction action, string currentValue)
        {
            if (schemaVersion < 5 &&
                (action == KeyboardAction.StopUnits || action == KeyboardAction.PatrolUnits) &&
                string.IsNullOrWhiteSpace(currentValue)) return true;
            if (schemaVersion < 4)
            {
                if (action == KeyboardAction.CenterOnKeep)
                    return currentValue == new KeyChord(102, false, false, false).Serialize();
                if (action == KeyboardAction.SelectAllMilitaryMap)
                    return currentValue == new KeyChord(103, false, false, false).Serialize();
            }
            return schemaVersion < 3 && action == KeyboardAction.PauseGame &&
                   string.IsNullOrWhiteSpace(currentValue);
        }

        internal static string LegacyDefaultForAction(KeyboardAction action)
        {
            int slot;
            if (TrySlot(action, KeyboardAction.ReplaceGroup1, 10, out slot) &&
                slot >= 3 && slot <= 5)
                return new KeyChord(49 + slot, true, false, false).Serialize();
            if (TrySlot(action, KeyboardAction.SelectGroup1, 10, out slot) &&
                slot >= 3 && slot <= 5)
                return new KeyChord(49 + slot, false, false, false).Serialize();
            if (TrySlot(action, KeyboardAction.AddGroup1, 10, out slot) &&
                slot >= 3 && slot <= 5)
                return new KeyChord(49 + slot, false, true, false).Serialize();
            if (TrySlot(action, KeyboardAction.ExclusiveGroup1, 10, out slot) &&
                slot >= 3 && slot <= 5)
                return new KeyChord(49 + slot, false, false, true).Serialize();
            if (TrySlot(action, KeyboardAction.SaveCamera1, 8, out slot) && slot >= 4)
                return new KeyChord(282 + slot, true, false, false).Serialize();
            if (TrySlot(action, KeyboardAction.RecallCamera1, 8, out slot) && slot >= 4)
                return new KeyChord(282 + slot, false, false, false).Serialize();
            if (TrySlot(action, KeyboardAction.SelectMercenaryPost, 6, out slot))
            {
                int[] oldKeys = { 102, 103, 104, 106, 107, 108 };
                return new KeyChord(oldKeys[slot], false, false, false).Serialize();
            }
            return null;
        }

        private static bool TrySlot(
            KeyboardAction action, KeyboardAction first, int count, out int slot)
        {
            slot = (int)action - (int)first;
            return slot >= 0 && slot < count;
        }
    }

    internal struct SettingsLauncherBounds
    {
        internal float Left, Top, Width, Height;
    }

    internal static class SettingsLauncherPlan
    {
        internal static SettingsLauncherBounds BesideMenu(float menuRight, float menuCenterY,
            float menuHeight, float screenWidth, float screenHeight)
        {
            float scale = Math.Max(0.75f, Math.Min(2f, menuHeight / 600f));
            float padding = 8f * scale;
            float left = menuRight + padding;
            float width = Math.Min(240f * scale, Math.Max(140f * scale, screenWidth - padding - left));
            float height = 52f * scale;
            // Keep the launcher reachable if a narrow screen leaves too little right margin.
            left = Math.Max(padding, Math.Min(left, screenWidth - padding - width));
            float top = Math.Max(padding, Math.Min(menuCenterY - height * 0.5f, screenHeight - padding - height));
            return new SettingsLauncherBounds { Left = left, Top = top, Width = width, Height = height };
        }
    }

    internal static class MinimapControlPlan
    {
        internal static int MoveButton(bool classic) { return classic ? 0 : 1; }
        internal static int CameraButton(bool classic) { return classic ? 1 : 0; }
        internal static int LeftMouseState(bool classic, bool press)
        {
            // EditorDirector.Update: 1 = down, 2 = held, 3 = up.
            return classic ? (press ? 1 : 3) : 0;
        }
    }

    internal struct NativeSelectionPhaseSpec
    {
        internal readonly int MouseState;
        internal readonly int CurrentAction;
        internal readonly bool UseSelectedUnits;
        internal readonly bool SelectionOn;
        internal readonly bool SelectionEstablished;
        internal readonly bool TroopSelectionBoxOn;
        internal readonly bool UseOnScreenUnits;

        internal NativeSelectionPhaseSpec(
            int mouseState, int currentAction, bool useSelectedUnits,
            bool selectionOn, bool selectionEstablished, bool troopSelectionBoxOn,
            bool useOnScreenUnits)
        {
            MouseState = mouseState;
            CurrentAction = currentAction;
            UseSelectedUnits = useSelectedUnits;
            SelectionOn = selectionOn;
            SelectionEstablished = selectionEstablished;
            TroopSelectionBoxOn = troopSelectionBoxOn;
            UseOnScreenUnits = useOnScreenUnits;
        }
    }

    internal static class NativeSelectionPlan
    {
        internal const int PhaseCount = 3;

        internal static NativeSelectionPhaseSpec Phase(int phase)
        {
            switch (phase)
            {
                case 0:
                    return new NativeSelectionPhaseSpec(1, 8, false, false, false, false, true);
                case 1:
                    return new NativeSelectionPhaseSpec(2, 8, true, true, true, false, false);
                case 2:
                    return new NativeSelectionPhaseSpec(3, 9, true, false, false, true, false);
                default:
                    throw new ArgumentOutOfRangeException("phase");
            }
        }
    }

    internal static class NativeSelectionVerification
    {
        internal static bool SelectedUnitsAreCandidateSubset(
            IEnumerable<int> candidates, IEnumerable<int> selected)
        {
            HashSet<int> candidateSet = new HashSet<int>(candidates ?? new int[0]);
            foreach (int objectId in selected ?? new int[0])
            {
                if (objectId > 0 && !candidateSet.Contains(objectId)) return false;
            }
            return true;
        }
    }

    internal static class MilitarySelectionPlan
    {
        internal static bool IsNativeUnitCountValid(int count, int maximum)
        {
            return maximum > 0 && count >= 1 && count <= maximum;
        }

        internal static bool IsMilitaryType(int unitType)
        {
            switch (unitType)
            {
                case 5:
                case 22:
                case 23:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                case 30:
                case 37:
                case 39:
                case 40:
                case 41:
                case 58:
                case 59:
                case 60:
                case 61:
                case 67:
                case 70:
                case 71:
                case 72:
                case 73:
                case 74:
                case 75:
                case 76:
                case 77:
                case 78:
                case 79:
                case 80:
                case 81:
                case 82:
                case 83:
                case 84:
                case 85:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsMapWideCandidate(
            int unitId, bool active, int owner, int activePlayer, int unitType)
        {
            return unitId > 0 && active && activePlayer > 0 && owner == activePlayer &&
                   IsMilitaryType(unitType);
        }
    }

    internal static class LordSelectionPlan
    {
        internal const float DoubleClickSeconds = 0.35f;

        internal static bool IsDoubleClick(float previousPressTime, float currentPressTime)
        {
            float elapsed = currentPressTime - previousPressTime;
            return previousPressTime >= 0f && elapsed >= 0f &&
                   elapsed <= DoubleClickSeconds + 0.0001f;
        }
    }

    internal static class ControlGroupPlan
    {
        internal static int[] Merge(
            IEnumerable<int> existing, IEnumerable<int> additions)
        {
            HashSet<int> merged = new HashSet<int>();
            foreach (int unitId in existing ?? new int[0])
                if (unitId > 0) merged.Add(unitId);
            foreach (int unitId in additions ?? new int[0])
                if (unitId > 0) merged.Add(unitId);
            int[] result = new int[merged.Count];
            merged.CopyTo(result);
            Array.Sort(result);
            return result;
        }
    }

    internal static class BarracksSelectionFallbackPlan
    {
        internal const int NativeSelectionWaitFrames = 120;

        internal static bool IsExpectedSelection(
            int appMode, int appSubMode, int expectedBuildingMode)
        {
            return appMode == 16 && appSubMode == expectedBuildingMode;
        }

        internal static bool CanIssueNativeSelection(
            bool selectionTransactionActive, int selectedTroops)
        {
            return !selectionTransactionActive && selectedTroops <= 0;
        }

        internal static bool ShouldEnterPlacement(bool existenceKnown, bool buildingExists)
        {
            return existenceKnown && !buildingExists;
        }
    }

    internal struct StockpilePoint
    {
        internal readonly int StructureId;
        internal readonly int Owner;
        internal readonly int X;
        internal readonly int Y;

        internal StockpilePoint(int structureId, int owner, int x, int y)
        {
            StructureId = structureId;
            Owner = owner;
            X = x;
            Y = y;
        }
    }

    internal static class GiftedStockpileRemovalPlan
    {
        private const int ConnectedComponentSpacing = 3;

        internal static bool ShouldAttempt(bool pendingNewMap, int ownedAreaCount)
        {
            return pendingNewMap && ownedAreaCount == 1;
        }

        internal static int CountAreas(IList<StockpilePoint> points)
        {
            if (points == null || points.Count == 0) return 0;
            bool[] visited = new bool[points.Count];
            int areas = 0;
            for (int start = 0; start < points.Count; start++)
            {
                if (visited[start]) continue;
                areas++;
                Queue<int> pending = new Queue<int>();
                pending.Enqueue(start);
                visited[start] = true;
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    for (int candidate = 0; candidate < points.Count; candidate++)
                    {
                        if (visited[candidate] ||
                            !AreConnected(points[current], points[candidate])) continue;
                        visited[candidate] = true;
                        pending.Enqueue(candidate);
                    }
                }
            }
            return areas;
        }

        internal static int[] OwnersWithOneArea(
            IList<StockpilePoint> points, bool pendingNewMap)
        {
            if (!pendingNewMap || points == null || points.Count == 0)
                return new int[0];
            Dictionary<int, List<StockpilePoint>> byOwner =
                new Dictionary<int, List<StockpilePoint>>();
            foreach (StockpilePoint point in points)
            {
                List<StockpilePoint> owned;
                if (!byOwner.TryGetValue(point.Owner, out owned))
                {
                    owned = new List<StockpilePoint>();
                    byOwner.Add(point.Owner, owned);
                }
                owned.Add(point);
            }
            List<int> owners = new List<int>();
            foreach (KeyValuePair<int, List<StockpilePoint>> entry in byOwner)
            {
                if (ShouldAttempt(true, CountAreas(entry.Value)))
                    owners.Add(entry.Key);
            }
            owners.Sort();
            return owners.ToArray();
        }

        private static bool AreConnected(StockpilePoint left, StockpilePoint right)
        {
            return left.Owner == right.Owner &&
                   Math.Abs(left.X - right.X) <= ConnectedComponentSpacing &&
                   Math.Abs(left.Y - right.Y) <= ConnectedComponentSpacing;
        }
    }

    internal sealed class NativePatchSpec
    {
        internal readonly int Rva;
        internal readonly byte[] Original;
        internal readonly byte[] Patched;

        internal NativePatchSpec(int rva, byte[] original, byte[] patched)
        {
            Rva = rva;
            Original = original;
            Patched = patched;
        }
    }

    internal static class EnemyExclusionRadiusPatchPlan
    {
        internal const int DefaultRadius = 70;
        internal const int MinimumRadius = 1;
        internal const int MaximumRadius = 127;

        internal static bool TryParseRadius(string toml, out int radius)
        {
            radius = DefaultRadius;
            if (string.IsNullOrEmpty(toml)) return false;

            foreach (string rawLine in toml.Split(new[] { '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("[")) continue;
                int equals = line.IndexOf('=');
                if (equals <= 0 ||
                    !line.Substring(0, equals).Trim().Equals(
                        "enemy_exclusion_radius", StringComparison.OrdinalIgnoreCase)) continue;

                string value = line.Substring(equals + 1);
                int comment = value.IndexOf('#');
                if (comment >= 0) value = value.Substring(0, comment);
                int parsed;
                if (!int.TryParse(value.Trim(), out parsed) ||
                    parsed < MinimumRadius || parsed > MaximumRadius) return false;
                radius = parsed;
                return true;
            }
            return false;
        }

        internal static NativePatchSpec[] CreateSpecs(int radius)
        {
            if (radius < MinimumRadius || radius > MaximumRadius)
                throw new ArgumentOutOfRangeException("radius");
            byte value = (byte)radius;
            return new[]
            {
                new NativePatchSpec(0xEE8E8,
                    new byte[] { 0x41, 0x3B, 0xF4 },
                    new byte[] { 0x83, 0xFE, value }),
                new NativePatchSpec(0x77DBB,
                    new byte[] { 0xC7, 0x44, 0x24, 0x20, 0x05, 0x00, 0x00, 0x00 },
                    new byte[] { 0xC7, 0x44, 0x24, 0x20, value, 0x00, 0x00, 0x00 }),
                new NativePatchSpec(0x78110,
                    new byte[] { 0xC7, 0x44, 0x24, 0x20, 0x03, 0x00, 0x00, 0x00 },
                    new byte[] { 0xC7, 0x44, 0x24, 0x20, value, 0x00, 0x00, 0x00 }),
                new NativePatchSpec(0x78352,
                    new byte[] { 0xB8, 0x1E, 0x00, 0x00, 0x00 },
                    new byte[] { 0xB8, value, 0x00, 0x00, 0x00 }),
                new NativePatchSpec(0x78357,
                    new byte[] { 0xB9, 0x0F, 0x00, 0x00, 0x00 },
                    new byte[] { 0xB9, value, 0x00, 0x00, 0x00 }),
                new NativePatchSpec(0x78560,
                    new byte[] { 0xC7, 0x44, 0x24, 0x20, 0x03, 0x00, 0x00, 0x00 },
                    new byte[] { 0xC7, 0x44, 0x24, 0x20, value, 0x00, 0x00, 0x00 }),
                new NativePatchSpec(0x78599,
                    new byte[] { 0xB8, 0x1E, 0x00, 0x00, 0x00 },
                    new byte[] { 0xB8, value, 0x00, 0x00, 0x00 }),
                new NativePatchSpec(0x7859E,
                    new byte[] { 0xB9, 0x0F, 0x00, 0x00, 0x00 },
                    new byte[] { 0xB9, value, 0x00, 0x00, 0x00 })
            };
        }
    }

    internal enum BuildingPlacementPolicy
    {
        EnemyExclusion = 1,
        OpeningLordRadius = 2
    }

    internal struct BuildingPlacementSettings
    {
        internal readonly BuildingPlacementPolicy Policy;
        internal readonly int EnemyExclusionRadius;
        internal readonly int OpeningLordRadius;
        internal readonly int OpeningDurationMinutes;

        internal BuildingPlacementSettings(
            BuildingPlacementPolicy policy,
            int enemyExclusionRadius,
            int openingLordRadius,
            int openingDurationMinutes)
        {
            Policy = policy;
            EnemyExclusionRadius = enemyExclusionRadius;
            OpeningLordRadius = openingLordRadius;
            OpeningDurationMinutes = openingDurationMinutes;
        }
    }

    internal static class BuildingPlacementPolicyPlan
    {
        internal const int SimulationTicksPerSecond = 40;
        internal const int MinimumOpeningRadius = 1;
        internal const int MaximumOpeningRadius = 800;
        internal const int MinimumDurationMinutes = 1;
        internal const int MaximumDurationMinutes = 120;
        internal const int NativeTooFarPanelSection = 77;
        internal const int NativeTooFarText = 15;

        internal static BuildingPlacementSettings Default
        {
            get
            {
                return new BuildingPlacementSettings(
                    BuildingPlacementPolicy.OpeningLordRadius,
                    EnemyExclusionRadiusPatchPlan.DefaultRadius, 140, 5);
            }
        }

        internal static bool TryParse(
            string toml, out BuildingPlacementSettings settings)
        {
            settings = Default;
            int activeOption;
            int enemyRadius;
            int openingRadius;
            int durationMinutes;
            if (!TryReadInt(toml, "active_option", out activeOption) ||
                (activeOption != (int)BuildingPlacementPolicy.EnemyExclusion &&
                 activeOption != (int)BuildingPlacementPolicy.OpeningLordRadius) ||
                !TryReadInt(toml, "option_1_enemy_exclusion_radius", out enemyRadius) ||
                enemyRadius < EnemyExclusionRadiusPatchPlan.MinimumRadius ||
                enemyRadius > EnemyExclusionRadiusPatchPlan.MaximumRadius ||
                !TryReadInt(toml, "option_2_opening_lord_radius", out openingRadius) ||
                openingRadius < MinimumOpeningRadius ||
                openingRadius > MaximumOpeningRadius ||
                !TryReadInt(toml, "option_2_duration_minutes", out durationMinutes) ||
                durationMinutes < MinimumDurationMinutes ||
                durationMinutes > MaximumDurationMinutes)
            {
                return false;
            }

            settings = new BuildingPlacementSettings(
                (BuildingPlacementPolicy)activeOption,
                enemyRadius, openingRadius, durationMinutes);
            return true;
        }

        internal static bool ShouldApplyEnemyPatch(BuildingPlacementPolicy policy)
        {
            return policy == BuildingPlacementPolicy.EnemyExclusion;
        }

        internal static bool ShouldRestrictCurrentAction(int currentAction)
        {
            return currentAction == 5;
        }

        internal static bool ShouldHoldNativeFeedback(
            float currentTime, float feedbackUntil)
        {
            return feedbackUntil >= 0f && currentTime <= feedbackUntil;
        }

        internal static bool IsOpeningWindow(int gameTimeTicks, int durationMinutes)
        {
            if (gameTimeTicks < 0 || durationMinutes < MinimumDurationMinutes)
                return false;
            long durationTicks =
                (long)durationMinutes * 60L * SimulationTicksPerSecond;
            return gameTimeTicks < durationTicks;
        }

        internal static bool IsWithinLordRadius(
            float lordX, float lordY, float targetX, float targetY, int radius)
        {
            if (radius < MinimumOpeningRadius) return false;
            double deltaX = targetX - lordX;
            double deltaY = targetY - lordY;
            return deltaX * deltaX + deltaY * deltaY <= (double)radius * radius;
        }

        private static bool TryReadInt(string toml, string key, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(toml)) return false;
            foreach (string rawLine in toml.Split(
                         new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("["))
                    continue;
                int equals = line.IndexOf('=');
                if (equals <= 0 || !line.Substring(0, equals).Trim().Equals(
                        key, StringComparison.OrdinalIgnoreCase)) continue;
                string rawValue = line.Substring(equals + 1);
                int comment = rawValue.IndexOf('#');
                if (comment >= 0) rawValue = rawValue.Substring(0, comment);
                return int.TryParse(rawValue.Trim(), out value);
            }
            return false;
        }
    }

    internal static class PersistentConfigurationPlan
    {
        internal static void CopyIfMissing(string source, string destination)
        {
            if (!System.IO.File.Exists(destination) && System.IO.File.Exists(source))
                System.IO.File.Copy(source, destination, false);
        }
    }

    internal static class RadarProjectionPlan
    {
        internal static float NormalizeNoesisY(float radarPixelY, int radarHeight)
        {
            if (radarHeight <= 1) throw new ArgumentOutOfRangeException("radarHeight");
            return 1f - Clamp01(radarPixelY / (radarHeight - 1f));
        }

        internal static bool TryInverseProjectedPoint(
            float p00X, float p00Y,
            float p10X, float p10Y,
            float p01X, float p01Y,
            float worldMinX, float worldMaxX,
            float worldMinY, float worldMaxY,
            float normalizedX, float normalizedY,
            int logicStartX, int logicEndX,
            int logicStartY, int logicEndY,
            out int logicX, out int logicY)
        {
            logicX = 0;
            logicY = 0;
            float axisUX = p10X - p00X;
            float axisUY = p10Y - p00Y;
            float axisVX = p01X - p00X;
            float axisVY = p01Y - p00Y;
            float determinant = axisUX * axisVY - axisUY * axisVX;
            if (Math.Abs(determinant) < 0.0001f ||
                worldMaxX <= worldMinX || worldMaxY <= worldMinY ||
                normalizedX < 0f || normalizedX > 1f ||
                normalizedY < 0f || normalizedY > 1f ||
                logicEndX < logicStartX || logicEndY < logicStartY)
            {
                return false;
            }

            float worldX = worldMinX + (worldMaxX - worldMinX) * normalizedX;
            float worldY = worldMinY + (worldMaxY - worldMinY) * normalizedY;
            float deltaX = worldX - p00X;
            float deltaY = worldY - p00Y;
            float u = Clamp01(
                (deltaX * axisVY - deltaY * axisVX) / determinant);
            float v = Clamp01(
                (axisUX * deltaY - axisUY * deltaX) / determinant);
            logicX = RoundPositive(logicStartX + (logicEndX - logicStartX) * u);
            logicY = RoundPositive(logicStartY + (logicEndY - logicStartY) * v);
            return true;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static int RoundPositive(float value)
        {
            return (int)Math.Floor(value + 0.5f);
        }
    }

    internal enum NativeGroupMode
    {
        Replace = -1,
        Delete = 20
    }

    internal struct NativeCommandSpec
    {
        internal readonly int Function;
        internal readonly int Value1;
        internal readonly int Value2;
        internal readonly int Value3;

        internal NativeCommandSpec(int function, int value1, int value2, int value3)
        {
            Function = function;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
        }
    }

    internal static class NativeCommandPlan
    {
        internal static NativeCommandSpec Group(int slot, NativeGroupMode mode)
        {
            ValidateGroupSlot(slot);
            int suffix = (slot + 1) % 10;
            return new NativeCommandSpec(19 + suffix, (int)mode, 0, 0);
        }

        internal static NativeCommandSpec SelectGroup(int slot)
        {
            ValidateGroupSlot(slot);
            int suffix = (slot + 1) % 10;
            return new NativeCommandSpec(29 + suffix, -1, 0, 0);
        }

        private static void ValidateGroupSlot(int slot)
        {
            if (slot < 0 || slot >= 10) throw new ArgumentOutOfRangeException("slot");
        }
    }

    internal static class BarracksRallyPlan
    {
        private static readonly Dictionary<int, int[]> MappersByMode =
            new Dictionary<int, int[]>
            {
                { 1, new[] { 332, 334, 335, 333, 336, 337, 338 } },
                { 44, new[] { 360, 362, 364, 363, 361, 366, 365 } },
                { 23, new[] { 367, 368 } },
                { 24, new[] { 369 } },
                { 96, new[] { 370 } },
                { 63, new[] { 395, 397, 391, 396, 398, 393, 392, 394 } }
            };

        internal static int[] MappersForMode(int buildingMode)
        {
            int[] mappers;
            return MappersByMode.TryGetValue(buildingMode, out mappers) ? mappers : null;
        }
    }

    internal static class BarracksProductionPlan
    {
        private static readonly Dictionary<int, int[]> ChimpTypesByMode =
            new Dictionary<int, int[]>
            {
                { 1, new[] { 22, 24, 26, 23, 25, 27, 28 } },
                { 44, new[] { 70, 71, 72, 73, 74, 75, 76 } },
                { 23, new[] { 29, 30 } },
                { 24, new[] { 5 } },
                { 96, new[] { 37 } },
                { 63, new[] { 78, 79, 80, 81, 82, 83, 84, 85 } }
            };
        private static readonly Dictionary<int, string[]> ButtonFieldsByMode =
            new Dictionary<int, string[]>
            {
                { 1, new[]
                    {
                        "RefRecruitArcherButton", "RefRecruitSpearmanButton",
                        "RefRecruitMacemanButton", "RefRecruitXBowmanButton",
                        "RefRecruitPikemanButton", "RefRecruitSwordsmanButton",
                        "RefRecruitKnightButton"
                    }
                },
                { 44, new[]
                    {
                        "RefRecruitArabBowButton", "RefRecruitArabSlaveButton",
                        "RefRecruitArabSlingerButton", "RefRecruitArabAssassinButton",
                        "RefRecruitArabHorseArcherButton", "RefRecruitArabSwordsmanButton",
                        "RefRecruitArabGrenadierButton"
                    }
                },
                { 23, new[] { "RefRecruitLaddermanButton", "RefRecruitEngineerButton" } },
                { 24, new[] { "RefRecruitTunellerButton" } },
                { 96, new[] { "RefRecruitMonkButton" } },
                { 63, new[]
                    {
                        "RefRecruitBedouinCamelLancerButton", "RefRecruitBedouinHealerButton",
                        "RefRecruitBedouinEunuchButton", "RefRecruitBedouinAmbusherButton",
                        "RefRecruitBedouinSkirmisherButton", "RefRecruitBedouinHeavyCamelButton",
                        "RefRecruitBedouinSapperButton", "RefRecruitBedouinDemolisherButton"
                    }
                }
            };

        internal static int[] ChimpTypesForMode(int buildingMode)
        {
            int[] chimpTypes;
            return ChimpTypesByMode.TryGetValue(buildingMode, out chimpTypes) ? chimpTypes : null;
        }

        internal static string[] ButtonFieldsForMode(int buildingMode)
        {
            string[] buttonFields;
            return ButtonFieldsByMode.TryGetValue(buildingMode, out buttonFields)
                ? buttonFields
                : null;
        }
    }

    internal static class BarracksSelectionPlan
    {
        internal static int FunctionForAction(KeyboardAction action)
        {
            switch (action)
            {
                case KeyboardAction.SelectMercenaryPost: return 97;
                case KeyboardAction.SelectBedouinStockade: return 99;
                case KeyboardAction.SelectBarracks: return 9;
                case KeyboardAction.SelectEngineersGuild: return 118;
                case KeyboardAction.SelectTunnelersGuild: return 117;
                case KeyboardAction.SelectCathedral: return 201;
                default: return -1;
            }
        }

        internal static KeyboardAction? ActionForFunction(int function)
        {
            for (int value = (int)KeyboardAction.SelectMercenaryPost;
                 value <= (int)KeyboardAction.SelectCathedral; value++)
            {
                KeyboardAction action = (KeyboardAction)value;
                if (FunctionForAction(action) == function) return action;
            }
            return null;
        }


        internal static int BuildingModeForAction(KeyboardAction action)
        {
            switch (action)
            {
                case KeyboardAction.SelectMercenaryPost: return 44;
                case KeyboardAction.SelectBedouinStockade: return 63;
                case KeyboardAction.SelectBarracks: return 1;
                case KeyboardAction.SelectEngineersGuild: return 23;
                case KeyboardAction.SelectTunnelersGuild: return 24;
                case KeyboardAction.SelectCathedral: return 96;
                default: return -1;
            }
        }

        internal static int MapperForAction(KeyboardAction action)
        {
            switch (action)
            {
                case KeyboardAction.SelectMercenaryPost: return 86;
                case KeyboardAction.SelectBedouinStockade: return 79;
                case KeyboardAction.SelectBarracks: return 87;
                case KeyboardAction.SelectEngineersGuild: return 88;
                case KeyboardAction.SelectTunnelersGuild: return 89;
                case KeyboardAction.SelectCathedral: return 97;
                default: return -1;
            }
        }

        internal static int NativeSelectorForAction(KeyboardAction action)
        {
            switch (action)
            {
                case KeyboardAction.SelectMercenaryPost: return 8;
                case KeyboardAction.SelectBedouinStockade: return 108;
                case KeyboardAction.SelectBarracks: return 9;
                case KeyboardAction.SelectEngineersGuild: return 24;
                case KeyboardAction.SelectTunnelersGuild: return 25;
                case KeyboardAction.SelectCathedral: return 38;
                default: return -1;
            }
        }
    }
}
