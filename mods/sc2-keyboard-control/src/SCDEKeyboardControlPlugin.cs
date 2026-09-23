using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SCDEKeyboardControl
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Stronghold Crusader Definitive Edition.exe")]
    public sealed class SCDEKeyboardControlPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "scde.sc2-keyboard-control";
        public const string PluginName = "JiuyeAyan's Advanced Control";
        public const string PluginVersion = "0.2.24";

        private const string SupportedAssemblyHash =
            "bc8b6a395f01d48557db413600c8dd8d1fdfd3abdf97bfbbb68a3c56b04fd789";
        private const string SupportedNativeHash =
            "fbcb93195fc7efca9bdac5204852efdd76f9818f59a6711750d77c9cef2831e2";
        private const int NativeStructureTableRva = 0x64CCBB0;
        private const int NativeStructureCountOffset = 0x50;
        private const int NativeStructureStride = 0x32C;
        private const int NativeStructureStatusOffset = 0x12C;
        private const int NativeStructureTypeOffset = 0x12E;
        private const int NativeStructureOwnerOffset = 0x132;
        private const int NativeStructureXOffset = 0x14A;
        private const int NativeStructureYOffset = 0x14C;
        private const int NativeRemoveStockpilesForOwnerRva = 0xC3FA0;
        private const int NativeUnitManagerRva = 0x67E8400;
        private const int NativeMaximumUnitId = 10000;
        private const int NativeUnitStride = 0x490;
        private const int NativeUnitActiveOffset = 0x6E4;
        private const int NativeUnitTypeOffset = 0x6E6;
        private const int NativeUnitFineXOffset = 0x70E;
        private const int NativeUnitFineYOffset = 0x710;
        private const int NativeUnitCellXOffset = 0x71C;
        private const int NativeUnitCellYOffset = 0x71E;
        private const int NativeUnitOwnerOffset = 0xA28;
        private const int NativeLiveUnitState = 2;
        private const int StockpileStructureType = 10;
        private const int LordChimpType = 55;
        private const float PlacementFeedbackHoldSeconds = 0.35f;
        private const string HybridMovementTypeName =
            "SCDEHybridMovement.SCDEHybridMovementPlugin";
        private const string FlatMovementTypeName =
            "SCDEFlatMovement.SCDEFlatMovementPlugin";
        internal const string PackedSettingsResource = "SCDEKeyboardControl.PackedSettings.toml";

        private static readonly int[] EmptyUnitIds = new int[0];
        private static readonly KeyboardAction[] Actions =
            (KeyboardAction[])Enum.GetValues(typeof(KeyboardAction));
        private static readonly KeyboardAction[] GroupAndCameraActions = Actions.Where(action =>
            action >= KeyboardAction.ReplaceGroup1 && action <= KeyboardAction.RecallCamera8).ToArray();
        private static readonly KeyboardAction[] ProductionActions = Actions.Where(action =>
            action >= KeyboardAction.ProduceSlot1 && action <= KeyboardAction.SelectCathedral).ToArray();
        private static readonly KeyboardAction[] BuildingActions = Actions.Where(action =>
            action >= KeyboardAction.BuildPageCastle && action <= KeyboardAction.BuildSlot10).ToArray();
        private static readonly KeyboardAction[] OtherActions = Actions.Except(GroupAndCameraActions)
            .Except(ProductionActions).Except(BuildingActions).ToArray();
        private static readonly FieldInfo FunctionMapField = AccessTools.Field(typeof(KeyManager), "functionMap");
        private static readonly FieldInfo FatControllerExitingField =
            AccessTools.Field(typeof(FatControler), "exiting");
        private static readonly FieldInfo RadarMousePointField =
            AccessTools.Field(typeof(FatControler), "NGMousePoint");
        private static readonly FieldInfo RadarMouseHeldField = AccessTools.Field(typeof(FatControler), "mouseIsDown");
        private static readonly FieldInfo ChimpsField = AccessTools.Field(typeof(GameMap), "chimps");
        private static readonly FieldInfo EngineThreadLockField =
            AccessTools.Field(typeof(EngineInterface), "threadLock");
        private static readonly FieldInfo OverNoesisUiField =
            AccessTools.Field(typeof(EditorDirector), "overNoesisUI");
        private static readonly FieldInfo SelectedChimpListField =
            AccessTools.Field(typeof(EditorDirector), "selectedChimpList");
        private static readonly FieldInfo UnderCursorChimpListField =
            AccessTools.Field(typeof(EditorDirector), "underCursorChimpList");
        private static readonly FieldInfo OnScreenChimpsListField =
            AccessTools.Field(typeof(EditorDirector), "onScreenChimpsList");
        private static readonly FieldInfo GotNewSelectionInfoField =
            AccessTools.Field(typeof(EditorDirector), "gotNewSelectionInfo");
        private static readonly FieldInfo TroopSelectionBoxOnField =
            AccessTools.Field(typeof(EditorDirector), "troopSelectionBoxOn");
        private static readonly FieldInfo LeftMouseStateForEngineField =
            AccessTools.Field(typeof(EditorDirector), "leftMouseStateForEngine");
        private static readonly FieldInfo StateReadField =
            AccessTools.Field(typeof(EditorDirector), "stateRead");
        private static readonly FieldInfo UpPendingField =
            AccessTools.Field(typeof(EditorDirector), "upPending");
        private static readonly FieldInfo RightDownForEngineField =
            AccessTools.Field(typeof(EditorDirector), "rightDownForEngine");
        private static readonly FieldInfo RightUpForEngineField =
            AccessTools.Field(typeof(EditorDirector), "rightUpForEngine");
        private static readonly FieldInfo TroopSelectionOnField =
            AccessTools.Field(typeof(TroopSelector), "selection_on");
        private static readonly FieldInfo TroopSelectionEstablishedField =
            AccessTools.Field(typeof(TroopSelector), "selection_established");
        private static readonly FieldInfo ScheduleTroopSelectionEndField =
            AccessTools.Field(typeof(EditorDirector), "scheduleTroopSelectionEnd");
        private static readonly Type MainViewModelType = AccessTools.TypeByName("CrusaderDE.MainViewModel");
        private static readonly FieldInfo TroopPanelField = AccessTools.Field(typeof(CrusaderDE.MainViewModel), "HUDTroopPanel");
        private static readonly FieldInfo HudRootField = AccessTools.Field(typeof(CrusaderDE.MainViewModel), "HUDRoot");
        private static readonly FieldInfo RadarGridField = AccessTools.Field(typeof(CrusaderDE.MainHUD), "RefRadarMapGrid");
        private static readonly FieldInfo StopButtonField = AccessTools.Field(typeof(CrusaderDE.HUD_Troops), "RefUnitStop");
        private static readonly FieldInfo PatrolButtonField = AccessTools.Field(typeof(CrusaderDE.HUD_Troops), "RefUnitPatrol");
        private static readonly FieldInfo PatrolActiveButtonField = AccessTools.Field(typeof(CrusaderDE.HUD_Troops), "RefUnitPatrolActive");
        private static readonly KeyboardAction[] TroopCommandActions = { KeyboardAction.StopUnits, KeyboardAction.PatrolUnits };
        private static readonly FieldInfo[] TroopCommandElementFields = typeof(CrusaderDE.HUD_Troops)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.Name.StartsWith("RefUnit", StringComparison.Ordinal) &&
                typeof(Noesis.FrameworkElement).IsAssignableFrom(field.FieldType)).ToArray();
        private static readonly MethodInfo MainViewModelInstanceGetter = MainViewModelType == null
            ? null
            : AccessTools.PropertyGetter(MainViewModelType, "Instance");
        private static readonly FieldInfo HudBuildingPanelField = MainViewModelType == null
            ? null
            : AccessTools.Field(MainViewModelType, "HUDBuildingPanel");
        private static readonly FieldInfo HudMainField = MainViewModelType == null
            ? null
            : AccessTools.Field(MainViewModelType, "HUDmain");
        private static readonly FieldInfo BuildScreenIdField = MainViewModelType == null
            ? null
            : AccessTools.Field(MainViewModelType, "buildScreenID");
        private static readonly Type HudMainType = AccessTools.TypeByName("CrusaderDE.HUD_Main");
        private static readonly FieldInfo BuildButtonsField = HudMainType == null
            ? null
            : AccessTools.Field(HudMainType, "buildButtons");
        private static readonly FieldInfo BuildIconListsField = HudMainType == null
            ? null
            : AccessTools.Field(HudMainType, "BuildIconLists");
        private static readonly string[] BuildTabFieldNames =
        {
            "RefTabBuildCastle", "RefTabBuildIndustry", "RefTabBuildFarms",
            "RefTabBuildTown", "RefTabBuildWeapons", "RefTabBuildFood"
        };
        private static readonly string[] BuildPageMethodNames =
        {
            "NewBuildScreenCastle", "NewBuildScreenIndustry", "NewBuildScreenFarms",
            "NewBuildScreenTown", "NewBuildScreenWeapons", "NewBuildScreenFood"
        };
        private static readonly FieldInfo[] BuildTabFields = HudMainType == null
            ? new FieldInfo[0]
            : BuildTabFieldNames.Select(name => AccessTools.Field(HudMainType, name)).ToArray();
        private static SCDEKeyboardControlPlugin _instance;

        private readonly BindingModel _bindings = new BindingModel();
        private readonly BindingModel _draftBindings = new BindingModel();
        private readonly Dictionary<KeyboardAction, ConfigEntry<string>> _bindingConfig =
            new Dictionary<KeyboardAction, ConfigEntry<string>>();
        private readonly HashSet<int>[] _groups = Enumerable.Range(0, 10)
            .Select(_ => new HashSet<int>()).ToArray();
        private readonly float[] _lastGroupSelectionTimes = Enumerable.Range(0, 10)
            .Select(_ => -1f).ToArray();
        private readonly Dictionary<string, FieldInfo> _recruitButtonFields =
            new Dictionary<string, FieldInfo>();
        private readonly List<SettingsHitTarget> _settingsHitTargets =
            new List<SettingsHitTarget>();

        private ConfigFile _persistentConfig;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _strictBuildCheck;
        private ConfigEntry<int> _bindingSchemaVersion;
        private ConfigEntry<bool> _pushMapScrollingDefaultApplied;
        private readonly System.Random _cameraRandom = new System.Random();
        private bool _compatibleBuild;
        private bool _optionsMenuVisible;
        private bool _settingsPageVisible;
        private bool _keyMapAuditLogged;
        private bool _runtimeUpdateLogged;
        private bool _runtimeGuiLogged;
        private bool _productionHintErrorLogged;
        private bool _buildHintErrorLogged;
        private bool _troopHintErrorLogged;
        private bool _flattenHintErrorLogged;
        private int _lastObservedNativeActionFrame = -1;
        private int _lastObservedNativeAction = -1;
        private KeyboardAction? _pendingBarracksFallbackAction;
        private int _pendingBarracksFallbackDeadlineFrame = -1;
        private bool _pendingBarracksNativeSelectionIssued;
        private bool _pendingBarracksExistenceKnown;
        private bool _pendingBarracksExists;
        private bool _optionalMovementInteropResolved;
        private bool _optionalMovementControlActive;
        private MethodInfo _setExternalMovementHotkeyControlMethod;
        private MethodInfo _tryExternalAttackMoveMethod;
        private FieldInfo _hybridMovementInstanceField;
        private FieldInfo _hybridMovementControllerField;
        private ConstructorInfo _hybridMovementTargetConstructor;
        private MethodInfo _hybridMovementIssueMoveMethod;
        private FieldInfo _flatMovementInstanceField;
        private FieldInfo _flatMovementKernelField;
        private FieldInfo _flatMovementDeferredCommandsField;
        private FieldInfo _flatMovementEnabledField;
        private FieldInfo _flatMovementActiveMapField;
        private ConstructorInfo _flatMovementVectorConstructor;
        private MethodInfo _flatMovementEnrollSelectedMethod;
        private MethodInfo _flatMovementGetSelectedMethod;
        private MethodInfo _flatMovementCommandMethod;
        private MethodInfo _flatMovementFindMethod;
        private Type _flatMovementDeferredCommandType;
        private FieldInfo _flatMovementDeferredGoalField;
        private FieldInfo _flatMovementDeferredSerialField;
        private EditorDirector _sessionDirector;
        private int _lastBarracksRallyFrame = -1000;
        private bool _consumeBarracksRightClick;
        private bool _issuingBarracksRallyPlacement;
        private int _barracksRallyReleaseFrame = -1;
        private int[] _nativeSelectionSequence;
        private int _nativeSelectionPhase;
        private bool _nativeSelectionInputActive;
        private int[] _pendingSelectionVerification;
        private bool _centerCameraAfterSelection;
        private int _selectionVerificationFrame = -1;
        private bool _suppressSelectionMouseInput;
        private volatile bool _pendingMapWideMilitarySelection;
        private float _lastLordSelectionTime = -1f;
        private float _nextNativePatchProbeTime;
        private bool _nativePatchWaitingLogged;
        private bool _nativePatchIncompatible;
        private bool _enemyExclusionRadiusPatchReady;
        private int _enemyExclusionRadius = EnemyExclusionRadiusPatchPlan.DefaultRadius;
        private BuildingPlacementSettings _placementSettings =
            BuildingPlacementPolicyPlan.Default;
        private NativePatchSpec[] _enemyExclusionRadiusPatchSpecs;
        private IntPtr[] _enemyExclusionRadiusPatchAddresses;
        private bool[] _enemyExclusionRadiusPatchOwned;
        private bool _nativeLayoutHashChecked;
        private bool _nativeLayoutHashSupported;
        private string _nativeLayoutActualHash;
        private volatile bool _pendingGiftedStockpileRemoval;
        private int _giftedStockpileMapGeneration;
        private int _giftedStockpileSimulationGeneration;
        private NativeRemoveStockpilesForOwner _removeStockpilesForOwner;
        private int _openingLordSimulationGeneration;
        private bool _openingLordAnchorCaptured;
        private float _openingLordX;
        private float _openingLordY;
        private float _nextOpeningLordProbeTime;
        private bool _openingLordUnavailableLogged;
        private int _lastBlockedPlacementFrame = -1;
        private float _openingPlacementFeedbackUntil = -1f;
        private MinimapMoveOrder _activeMinimapMove;
        private MinimapMoveOrder _queuedMinimapMove;
        private int _minimapMovePhase;
        private bool _nativeMinimapCommandInputActive;
        private KeyboardControlRuntime _runtime;
        private object _activeOptionsRoot;
        private Noesis.FrameworkElement _optionsMenuPanel;
        private bool _optionsAnchorWarningLogged;
        private object _hiddenOptionsRoot;
        private PropertyInfo _optionsOpacityProperty;
        private PropertyInfo _optionsHitTestProperty;
        private object _originalOptionsOpacity;
        private object _originalOptionsHitTest;

        private KeyManager _mappedKeyManager;
        private int[,] _originalFunctionMap;
        private bool _keyMapDirty = true;

        private KeyboardAction? _pendingActionForKey;
        private int _selectedKeyCode;
        private Vector2 _actionScroll;
        private bool _pendingCtrl;
        private bool _pendingShift;
        private bool _pendingAlt;
        private int _clickedKeyCode;
        private float _clickedKeyUntil;
        private bool _draftDirty;
        private bool _groupCategoryExpanded;
        private bool _productionCategoryExpanded;
        private bool _buildingCategoryExpanded;
        private bool _otherCategoryExpanded;
        private string _uiStatus = "";
        private bool _settingsCloseClickActive;
        private KeyboardAction? _lastExecutedAction;
        private int _lastExecutedActionFrame = -1;
        private Texture2D _keyNormalTexture;
        private Texture2D _keyMappedTexture;
        private Texture2D _keyPressedTexture;
        private Texture2D _keyModifierTexture;
        private Texture2D _productionHintTexture;
        private GUIStyle _productionHintStyle;
        private GUIStyle _militarySelectionHintStyle;
        private int _productionHintStyleScreenHeight = -1;

        private static readonly LanguageCatalog Text = new LanguageCatalog();
        private static KeyVisual[][] KeyboardRows;
        private static KeyVisual[] AssignableKeys;
        private static Dictionary<int, string> KeyLabels;

        private void Awake()
        {
            Logger.LogInfo("Advanced Control " + PluginVersion + " bootstrap started.");
            try
            {
                Text.Load(Path.Combine(Paths.ConfigPath, "sc2-keyboard-control", "lang"),
                    System.Globalization.CultureInfo.CurrentUICulture.Name);
            }
            catch (Exception error) { Logger.LogWarning("Language initialization: " + error.Message); }
            foreach (string error in Text.Errors) Logger.LogWarning("Language pack: " + error);
            Logger.LogInfo("Language catalog ready: " + Text.Language + "; warnings=" + Text.Errors.Count + ".");
            KeyboardRows = CreateKeyboardRows();
            AssignableKeys = KeyboardRows.SelectMany(row => row)
                .Where(key => key.KeyCode > 0 && key.Modifier == ModifierKind.None)
                .GroupBy(key => key.KeyCode).Select(group => group.First()).ToArray();
            KeyLabels = AssignableKeys.ToDictionary(key => key.KeyCode, key => key.Label);
            _uiStatus = L("Choose_an_action_on_the_right_then_press_one_key_combination");
            _persistentConfig = ResolvePersistentConfig();
            LoadBuildingPlacementSettings();
            _enabled = _persistentConfig.Bind("General", "Enabled", true, L("Config_Enable"));
            _strictBuildCheck = _persistentConfig.Bind(
                "Compatibility", "StrictBuildCheck", true,
                L("Config_StrictBuild"));
            _bindingSchemaVersion = _persistentConfig.Bind(
                "General", "BindingSchemaVersion", 0,
                L("Config_BindingSchema"));
            _pushMapScrollingDefaultApplied = _persistentConfig.Bind(
                "Migration", "PushMapScrollingDefaultApplied", false,
                "Records the one-time native edge-scroll default; later player choices are preserved.");

            _bindings.ResetDefaults();
            BindConfiguration();
            _compatibleBuild = VerifyGameBuild();
            if (!_compatibleBuild)
            {
                enabled = false;
                return;
            }

            _instance = this;
            new Harmony(PluginGuid).PatchAll(typeof(SCDEKeyboardControlPlugin).Assembly);
            CreateDetachedRuntime();
            Application.wantsToQuit += OnApplicationWantsToQuit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            BeginExitAudit();
            CreateKeyboardTextures();
            InstallManagedKeyMap(KeyManager.instance);
            Logger.LogInfo(
                "JiuyeAyan's Advanced Control " + PluginVersion + " loaded: managed language loading and menu-anchored settings; " +
                "waiting for the live KeyManager.");
        }

        internal static void ApplyNativeControlDefaults()
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null) || !owner._compatibleBuild || !owner._enabled.Value ||
                owner._pushMapScrollingDefaultApplied.Value || KeyManager.instance == null) return;
            try
            {
                // Run only after the game has loaded its settings, never replace its other defaults.
                ConfigSettings.Settings_PushMapScrolling = true;
                // First-run/unreadable settings must not be written over before native setup/recovery.
                if (ConfigSettings.SettingsFileExisted) ConfigSettings.SaveSettings(false);
                owner._pushMapScrollingDefaultApplied.Value = true;
                owner._persistentConfig.Save();
                owner.Logger.LogInfo("Native push-map scrolling enabled once; future player choices are preserved.");
            }
            catch (Exception error)
            {
                owner.Logger.LogWarning("Native edge-scroll default could not be saved: " + error.Message);
            }
        }

        private void LoadBuildingPlacementSettings()
        {
            string toml;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PackedSettingsResource))
            using (StreamReader reader = new StreamReader(stream))
                toml = reader.ReadToEnd();
            BuildingPlacementSettings settings;
            if (!BuildingPlacementPolicyPlan.TryParse(toml, out settings))
                throw new InvalidDataException("Invalid embedded building-placement rules; rebuild the Mod with valid source settings.");
            _placementSettings = settings;
            _enemyExclusionRadius = settings.EnemyExclusionRadius;
            _enemyExclusionRadiusPatchSpecs =
                EnemyExclusionRadiusPatchPlan.CreateSpecs(_enemyExclusionRadius);
            _enemyExclusionRadiusPatchAddresses =
                new IntPtr[_enemyExclusionRadiusPatchSpecs.Length];
            _enemyExclusionRadiusPatchOwned =
                new bool[_enemyExclusionRadiusPatchSpecs.Length];
            Logger.LogInfo(string.Format(
                "Embedded building-placement rules loaded: activeOption={0}, option1EnemyRadius={1}, option2LordRadius={2}, option2Minutes={3}, resource={4}",
                (int)settings.Policy, settings.EnemyExclusionRadius,
                settings.OpeningLordRadius, settings.OpeningDurationMinutes,
                PackedSettingsResource));
        }

        private void CreateDetachedRuntime()
        {
            GameObject runtimeObject = new GameObject("JiuyeAyan Advanced Control Runtime");
            runtimeObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runtimeObject);
            _runtime = runtimeObject.AddComponent<KeyboardControlRuntime>();
            _runtime.Owner = this;
        }

        private void CreateKeyboardTextures()
        {
            _keyNormalTexture = SolidTexture(new Color(0.08f, 0.11f, 0.16f, 1f));
            _keyMappedTexture = SolidTexture(new Color(0.13f, 0.22f, 0.34f, 1f));
            _keyPressedTexture = SolidTexture(new Color(0.22f, 0.66f, 0.72f, 1f));
            _keyModifierTexture = SolidTexture(new Color(0.48f, 0.35f, 0.12f, 1f));
            _productionHintTexture = SolidTexture(new Color(0.025f, 0.10f, 0.22f, 1f));
        }

        private static Texture2D SolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        internal void RuntimeUpdate()
        {
            bool runtimeEnabled = _enabled.Value && _compatibleBuild;
            UpdateEnemyExclusionRadiusPatch(
                runtimeEnabled && BuildingPlacementPolicyPlan.ShouldApplyEnemyPatch(
                    _placementSettings.Policy));
            UpdateOptionalMovementHotkeyControl(runtimeEnabled);
            if (!runtimeEnabled)
            {
                RestoreOriginalKeyMap(KeyManager.instance);
                return;
            }

            UpdateOpeningLordAnchor();

            if (_consumeBarracksRightClick &&
                ((_barracksRallyReleaseFrame >= 0 &&
                  Time.frameCount > _barracksRallyReleaseFrame) ||
                 (Time.frameCount > _lastBarracksRallyFrame + 1 &&
                  !Input.GetMouseButton(1))))
            {
                _consumeBarracksRightClick = false;
                _barracksRallyReleaseFrame = -1;
            }

            VerifyPendingSelection();
            CompleteBarracksSelectOrBuildFallback();
            ReleaseSelectionMouseSuppressionWhenSafe();

            if (!_runtimeUpdateLogged)
            {
                _runtimeUpdateLogged = true;
                Logger.LogInfo("Detached keyboard runtime update loop active.");
            }

            if (_settingsCloseClickActive && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
                _settingsCloseClickActive = false;
            if (_optionsMenuVisible)
            {
                if (_settingsPageVisible)
                {
                    HandleSettingsPhysicalClick();
                    CapturePhysicalBinding();
                }
                else HandleKeyboardLauncherClick();
                return;
            }
            if (EditorDirector.instance == null)
            {
                _sessionDirector = null;
                _lastLordSelectionTime = -1f;
                CancelBarracksSelectionFallback();
                CancelNativeSelectionTransaction();
            }
        }

        internal void RuntimeOnGUI()
        {
            if (!_enabled.Value || !_compatibleBuild) return;
            if (!_optionsMenuVisible)
            {
                if (Event.current != null && Event.current.type != EventType.Repaint) return;
                DrawProductionHints();
                DrawBuildHints();
                DrawTroopCommandHints();
                DrawFlattenHint();
                return;
            }
            if (!_runtimeGuiLogged)
            {
                _runtimeGuiLogged = true;
                Logger.LogInfo("Keyboard launcher is rendering inside the active options screen.");
            }
            if (_settingsPageVisible) DrawKeyboardSettings();
            else DrawKeyboardSettingsEntry();
        }

        internal static void ReplaceOriginalKeyMap(KeyManager manager)
        {
            if (!ReferenceEquals(_instance, null)) _instance.InstallManagedKeyMap(manager);
        }

        internal static void ObserveOptionsOpened(object options)
        {
            if (ReferenceEquals(_instance, null) || options == null) return;
            _instance._activeOptionsRoot = options;
            _instance._optionsMenuPanel = null;
            _instance._optionsAnchorWarningLogged = false;
            _instance._optionsMenuVisible = true;
            _instance._settingsPageVisible = false;
            _instance._runtimeGuiLogged = false;
            _instance._keyMapDirty = true;
            _instance.Logger.LogInfo("Options screen opened; keyboard launcher enabled.");
        }

        internal static void ObserveOptionsButton(int buttonId)
        {
            if (ReferenceEquals(_instance, null) || buttonId != -1) return;
            if (_instance._settingsPageVisible) _instance.DiscardDraftBindings();
            _instance._settingsPageVisible = false;
            _instance._optionsMenuVisible = false;
            _instance.RestoreNativeOptions();
            _instance._activeOptionsRoot = null;
            _instance._optionsMenuPanel = null;
            _instance._keyMapDirty = true;
            _instance.Logger.LogInfo("Options screen closed; keyboard launcher hidden.");
        }

        internal static void ObserveNewMapLoading(bool multiplayerSave)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null)) return;
            owner._giftedStockpileMapGeneration++;
            owner._giftedStockpileSimulationGeneration = 0;
            owner._pendingGiftedStockpileRemoval = !multiplayerSave;
            owner.ResetOpeningLordAnchor();
            owner.CancelMinimapMoves();
            owner.Logger.LogInfo(string.Format(
                "Map-load stockpile guard reset: generation={0}, multiplayerSave={1}, armed={2}.",
                owner._giftedStockpileMapGeneration, multiplayerSave,
                owner._pendingGiftedStockpileRemoval));
        }

        internal static void ObserveSaveLoading()
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null)) return;
            owner._giftedStockpileMapGeneration++;
            owner._giftedStockpileSimulationGeneration = 0;
            owner._pendingGiftedStockpileRemoval = false;
            owner.ResetOpeningLordAnchor();
            owner.CancelMinimapMoves();
            owner.Logger.LogInfo(string.Format(
                "Save-load stockpile guard disabled: generation={0}.",
                owner._giftedStockpileMapGeneration));
        }

        internal static void BeforeSimulationStarts(Director director)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null)) return;
            owner._openingLordSimulationGeneration =
                owner._giftedStockpileMapGeneration;
            owner._nextOpeningLordProbeTime = 0f;
            if (!owner._pendingGiftedStockpileRemoval) return;
            owner._giftedStockpileSimulationGeneration = owner._giftedStockpileMapGeneration;
            owner.Logger.LogInfo(string.Format(
                "New simulation armed direct stockpile cleanup before its first native simulation tick: generation={0}, simTick={1}.",
                owner._giftedStockpileSimulationGeneration,
                director == null ? -1 : director.getSimTickCount()));
        }

        internal static void RemoveGiftedStockpilesAtNativeBoundary()
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (!ReferenceEquals(owner, null))
                owner.RemoveGiftedStockpilesAtNativeBoundaryImpl();
        }

        internal static void ObserveNativeAction(Enums.KeyFunctions action, bool pressed)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            int function = (int)action;
            if (ReferenceEquals(owner, null) || !pressed || !owner.CanAcceptGameplayHotkey())
            {
                return;
            }
            if (owner._lastObservedNativeActionFrame == Time.frameCount &&
                owner._lastObservedNativeAction == function) return;
            owner._lastObservedNativeActionFrame = Time.frameCount;
            owner._lastObservedNativeAction = function;

            if (function >= 19 && function <= 28)
            {
                owner.EnsureSessionInitialized();
                int slot = function == 19 ? 9 : function - 20;
                int[] selected = GetSelectedUnitIds();
                owner._groups[slot].Clear();
                owner._groups[slot].UnionWith(selected);
                owner.Logger.LogInfo(string.Format(
                    "Native group assignment observed: group={0}, units={1}.",
                    GroupLabel(slot), selected.Length));
                return;
            }

            if (function >= 29 && function <= 38)
            {
                int slot = function == 29 ? 9 : function - 30;
                owner.Logger.LogInfo("Native group selection observed: group=" + GroupLabel(slot) + ".");
                return;
            }

            if (function >= (int)Enums.KeyFunctions.SetBookmark0 &&
                function < (int)Enums.KeyFunctions.SetBookmark0 + 8)
            {
                owner.Logger.LogInfo(
                    "Native camera bookmark saved: F" +
                    (function - (int)Enums.KeyFunctions.SetBookmark0 + 1) + ".");
                return;
            }

            if (function >= (int)Enums.KeyFunctions.GotoBookmark0 &&
                function < (int)Enums.KeyFunctions.GotoBookmark0 + 8)
            {
                owner.Logger.LogInfo(
                    "Native camera bookmark recalled: F" +
                    (function - (int)Enums.KeyFunctions.GotoBookmark0 + 1) + ".");
                return;
            }

            if (action == Enums.KeyFunctions.IncreaseEngineSpeed ||
                action == Enums.KeyFunctions.DecreaseEngineSpeed)
            {
                owner.Logger.LogInfo(
                    action == Enums.KeyFunctions.IncreaseEngineSpeed
                        ? "Native game-speed increase observed."
                        : "Native game-speed decrease observed.");
                return;
            }

            if (action == Enums.KeyFunctions.ToggleFrameRate)
            {
                owner.Logger.LogInfo("Native frame-rate toggle observed.");
            }
        }

        internal static bool IsCustomTroopCommand(Enums.KeyFunctions action)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            return !ReferenceEquals(owner, null) && owner._enabled.Value && owner._compatibleBuild &&
                (action == Enums.KeyFunctions.Stop || action == Enums.KeyFunctions.Patrol);
        }

        internal static void ProcessCustomKeyActions(KeyManager manager)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null) || manager == null || owner._settingsPageVisible ||
                !owner.CanAcceptGameplayHotkey())
            {
                return;
            }

            KeyboardAction? pressedAction = owner.FindPressedCustomAction();
            if (!pressedAction.HasValue) return;
            owner.EnsureSessionInitialized();
            if (pressedAction.Value == KeyboardAction.AttackMove)
            {
                if (owner.TryExecuteOptionalAttackMove())
                {
                    owner.Logger.LogInfo("Optional hybrid-movement attack move executed.");
                }
                return;
            }
            try
            {
                owner.ExecuteAction(pressedAction.Value);
                owner.Logger.LogInfo(
                    "KeyManager gameplay hotkey executed: " + ActionLabel(pressedAction.Value) + ".");
            }
            catch (Exception error)
            {
                owner.Logger.LogWarning(
                    "Gameplay hotkey failed for " + ActionLabel(pressedAction.Value) + ": " +
                    error.GetBaseException().Message);
            }
        }

        internal static bool TryHandleBarracksRallyClick(EditorDirector director)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            return !ReferenceEquals(owner, null) &&
                   (owner.HandleBarracksRallyClick(director) || owner._consumeBarracksRightClick);
        }

        internal static bool ReadEditorMouseButtonDown(int button)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if ((button == 0 || button == 1) && !ReferenceEquals(owner, null) &&
                owner._suppressSelectionMouseInput)
                return false;
            if (button == 1 && !ReferenceEquals(owner, null) &&
                owner._consumeBarracksRightClick)
                return false;
            return Input.GetMouseButtonDown(button);
        }

        internal static bool ReadEditorMouseButtonUp(int button)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            bool released = Input.GetMouseButtonUp(button);
            if ((button == 0 || button == 1) && !ReferenceEquals(owner, null) &&
                owner._suppressSelectionMouseInput)
                return false;
            if (button == 1 && !ReferenceEquals(owner, null) &&
                owner._consumeBarracksRightClick)
            {
                if (released) owner._barracksRallyReleaseFrame = Time.frameCount;
                return false;
            }
            return released;
        }

        internal static bool ReadEditorMouseButton(int button)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if ((button == 0 || button == 1) && !ReferenceEquals(owner, null) &&
                owner._suppressSelectionMouseInput)
                return false;
            return Input.GetMouseButton(button);
        }

        internal static bool AllowNativeTroopSelectionCall()
        {
            SCDEKeyboardControlPlugin owner = _instance;
            return ReferenceEquals(owner, null) || owner._nativeSelectionInputActive ||
                   owner._nativeMinimapCommandInputActive ||
                   owner._nativeSelectionSequence == null;
        }

        internal static void PrepareSynchronizedNativeSelection(EditorDirector director)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null)) return;
            owner.PreparePendingMapWideMilitarySelection(director);
            owner.PrepareNativeSelectionInput(director);
        }

        internal static void CompleteSynchronizedNativeSelection(ref int mouseLogicX, ref int mouseLogicY)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null)) return;
            owner.CompleteNativeSelectionInput();
            owner.FlushMinimapMove(ref mouseLogicX, ref mouseLogicY);
        }

        internal static void ObserveMinimapInput(FatControler controller)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (!ReferenceEquals(owner, null)) owner.HandleMinimapMoveClick(controller);
        }

        private static bool OwnsMinimapMouseInput()
        {
            SCDEKeyboardControlPlugin owner = _instance;
            return !ReferenceEquals(owner, null) && owner._enabled.Value && owner._compatibleBuild &&
                !owner._optionsMenuVisible && Director.instance != null && Director.instance.SimRunning &&
                (int)FatControler.currentScene == 2;
        }

        internal static bool ReadMinimapCameraHeld(FatControler controller)
        {
            return OwnsMinimapMouseInput()
                ? Input.GetMouseButton(MinimapControlPlan.CameraButton(ConfigSettings.Settings_SH1RTSControls))
                : (bool)RadarMouseHeldField.GetValue(controller);
        }

        internal static bool ReadMinimapCameraDown()
        {
            return OwnsMinimapMouseInput()
                ? Input.GetMouseButtonDown(MinimapControlPlan.CameraButton(ConfigSettings.Settings_SH1RTSControls))
                : FatControler.MouseIsDownStroke;
        }

        internal static void ObserveNativeKeyMapMutation(KeyManager manager)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null)) return;
            owner._keyMapDirty = true;
            owner.InstallManagedKeyMap(manager);
        }

        internal static bool LoadNativeKeyMap(KeyManager manager)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null) || !owner._enabled.Value || !owner._compatibleBuild)
                return true;
            // The Mod config is authoritative. The vanilla reader also throws when its
            // unbound stance keys are absent from its used-key dictionary.
            ObserveNativeKeyMapMutation(manager);
            return false;
        }

        internal static bool AllowLocalPlacement(
            int mapper, int logicX, int logicY, int player,
            bool inGameNotEditor, int mouseState)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            return ReferenceEquals(owner, null) || owner.AllowLocalPlacementImpl(
                mapper, logicX, logicY, player, inGameNotEditor, mouseState);
        }

        internal static void OverrideOpeningPlacementFeedback(
            ref int panelSection, ref int panelText, ref bool force)
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (ReferenceEquals(owner, null) ||
                !BuildingPlacementPolicyPlan.ShouldHoldNativeFeedback(
                    Time.unscaledTime, owner._openingPlacementFeedbackUntil))
                return;
            panelSection = BuildingPlacementPolicyPlan.NativeTooFarPanelSection;
            panelText = BuildingPlacementPolicyPlan.NativeTooFarText;
            force = true;
        }

        private void UpdateOptionalMovementHotkeyControl(bool shouldControl)
        {
            if (shouldControl && !_optionalMovementInteropResolved)
            {
                ResolveOptionalMovementInterop();
            }
            if (_setExternalMovementHotkeyControlMethod == null ||
                _optionalMovementControlActive == shouldControl) return;

            try
            {
                _setExternalMovementHotkeyControlMethod.Invoke(
                    null, new object[] { shouldControl });
                _optionalMovementControlActive = shouldControl;
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Optional hybrid-movement hotkey control could not be changed: " +
                    error.GetBaseException().Message);
                _setExternalMovementHotkeyControlMethod = null;
                _tryExternalAttackMoveMethod = null;
                _optionalMovementControlActive = false;
            }
        }

        private void ResolveOptionalMovementInterop()
        {
            _optionalMovementInteropResolved = true;
            Type legacyMovementType = null;
            Type flatMovementType = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (legacyMovementType == null)
                    legacyMovementType = assembly.GetType(HybridMovementTypeName, false);
                if (flatMovementType == null)
                    flatMovementType = assembly.GetType(FlatMovementTypeName, false);
                if (legacyMovementType != null || flatMovementType != null) break;
            }
            if (legacyMovementType == null && flatMovementType == null)
            {
                Logger.LogInfo(
                    "Optional sc2-hybrid-movement integration is unavailable; keyboard features remain active.");
                return;
            }

            if (flatMovementType != null)
                ResolveOptionalFlatMovementInterop(flatMovementType);
            if (legacyMovementType == null)
            {
                Logger.LogInfo(
                    "Optional SCDEFlatMovement implementation detected; minimap movement is integrated, while the legacy attack-move hotkey interface is unavailable.");
                return;
            }

            ResolveOptionalMinimapMovementInterop(legacyMovementType);

            _setExternalMovementHotkeyControlMethod = legacyMovementType.GetMethod(
                "SetExternalHotkeyControl",
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(bool) }, null);
            _tryExternalAttackMoveMethod = legacyMovementType.GetMethod(
                "TryArmAttackMoveFromExternalBinding",
                BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            if (_setExternalMovementHotkeyControlMethod == null ||
                _tryExternalAttackMoveMethod == null ||
                _tryExternalAttackMoveMethod.ReturnType != typeof(bool))
            {
                _setExternalMovementHotkeyControlMethod = null;
                _tryExternalAttackMoveMethod = null;
                Logger.LogWarning(
                    "sc2-hybrid-movement was found, but it does not expose the optional 0.1.1 hotkey interface; keyboard features remain active.");
                return;
            }
            Logger.LogInfo("Optional sc2-hybrid-movement 0.1.1 hotkey interface detected.");
        }

        private void ResolveOptionalFlatMovementInterop(Type movementType)
        {
            Type kernelType = movementType.Assembly.GetType(
                "SCDEFlatMovement.FlatMovementKernel", false);
            Type vectorType = movementType.Assembly.GetType(
                "SCDEFlatMovement.FlatVector", false);
            _flatMovementDeferredCommandType = movementType.GetNestedType(
                "DeferredMovementCommand", BindingFlags.NonPublic);
            _flatMovementInstanceField = movementType.GetField(
                "_instance", BindingFlags.NonPublic | BindingFlags.Static);
            _flatMovementKernelField = movementType.GetField(
                "_kernel", BindingFlags.NonPublic | BindingFlags.Instance);
            _flatMovementDeferredCommandsField = movementType.GetField(
                "_deferredCommands", BindingFlags.NonPublic | BindingFlags.Instance);
            _flatMovementEnabledField = movementType.GetField(
                "_enabled", BindingFlags.NonPublic | BindingFlags.Instance);
            _flatMovementActiveMapField = movementType.GetField(
                "_activeMap", BindingFlags.NonPublic | BindingFlags.Instance);
            _flatMovementVectorConstructor = vectorType == null
                ? null
                : vectorType.GetConstructor(new[] { typeof(float), typeof(float) });
            _flatMovementEnrollSelectedMethod = movementType.GetMethod(
                "EnrollSelected", BindingFlags.NonPublic | BindingFlags.Instance);
            _flatMovementGetSelectedMethod = movementType.GetMethod(
                "GetSelectedCombatUnits", BindingFlags.NonPublic | BindingFlags.Instance);
            _flatMovementCommandMethod = kernelType == null || vectorType == null
                ? null
                : kernelType.GetMethod(
                    "Command", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(IEnumerable<int>), vectorType }, null);
            _flatMovementFindMethod = kernelType == null
                ? null
                : kernelType.GetMethod(
                    "Find", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);
            _flatMovementDeferredGoalField = _flatMovementDeferredCommandType == null
                ? null
                : _flatMovementDeferredCommandType.GetField(
                    "Goal", BindingFlags.Public | BindingFlags.Instance);
            _flatMovementDeferredSerialField = _flatMovementDeferredCommandType == null
                ? null
                : _flatMovementDeferredCommandType.GetField(
                    "CommandSerial", BindingFlags.Public | BindingFlags.Instance);
            if (_flatMovementInstanceField == null ||
                _flatMovementKernelField == null ||
                _flatMovementDeferredCommandsField == null ||
                _flatMovementEnabledField == null ||
                _flatMovementActiveMapField == null ||
                _flatMovementVectorConstructor == null ||
                _flatMovementEnrollSelectedMethod == null ||
                _flatMovementGetSelectedMethod == null ||
                _flatMovementCommandMethod == null ||
                _flatMovementFindMethod == null ||
                _flatMovementDeferredCommandType == null ||
                _flatMovementDeferredGoalField == null ||
                _flatMovementDeferredSerialField == null)
            {
                ClearOptionalFlatMovementInterop();
                Logger.LogWarning(
                    "SCDEFlatMovement was found, but its optional minimap-move bridge could not be resolved.");
                return;
            }
            Logger.LogInfo(
                "Optional SCDEFlatMovement minimap-move bridge detected without adding a dependency.");
        }

        private void ClearOptionalFlatMovementInterop()
        {
            _flatMovementInstanceField = null;
            _flatMovementKernelField = null;
            _flatMovementDeferredCommandsField = null;
            _flatMovementEnabledField = null;
            _flatMovementActiveMapField = null;
            _flatMovementVectorConstructor = null;
            _flatMovementEnrollSelectedMethod = null;
            _flatMovementGetSelectedMethod = null;
            _flatMovementCommandMethod = null;
            _flatMovementFindMethod = null;
            _flatMovementDeferredCommandType = null;
            _flatMovementDeferredGoalField = null;
            _flatMovementDeferredSerialField = null;
        }

        private void ResolveOptionalMinimapMovementInterop(Type movementType)
        {
            Type controllerType = movementType.Assembly.GetType(
                "SCDEHybridMovement.MovementController", false);
            Type targetType = movementType.Assembly.GetType(
                "SCDEHybridMovement.NativeMovementTarget", false);
            Type vecType = movementType.Assembly.GetType(
                "SCDEHybridMovement.Vec2", false);
            _hybridMovementInstanceField = movementType.GetField(
                "_instance", BindingFlags.NonPublic | BindingFlags.Static);
            _hybridMovementControllerField = movementType.GetField(
                "_movementController", BindingFlags.NonPublic | BindingFlags.Instance);
            _hybridMovementTargetConstructor = targetType == null || vecType == null
                ? null
                : targetType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { vecType, typeof(int), typeof(int), typeof(bool) }, null);
            _hybridMovementIssueMoveMethod = controllerType == null || targetType == null
                ? null
                : controllerType.GetMethod(
                    "IssueMove", BindingFlags.Instance | BindingFlags.Public,
                    null, new[] { typeof(int[]), targetType, typeof(bool) }, null);
            if (_hybridMovementInstanceField == null ||
                _hybridMovementControllerField == null ||
                _hybridMovementTargetConstructor == null ||
                _hybridMovementIssueMoveMethod == null || vecType == null)
            {
                _hybridMovementInstanceField = null;
                _hybridMovementControllerField = null;
                _hybridMovementTargetConstructor = null;
                _hybridMovementIssueMoveMethod = null;
                Logger.LogWarning(
                    "sc2-hybrid-movement was found, but its optional minimap-move bridge could not be resolved; vanilla movement remains available when the movement Mod is inactive.");
                return;
            }
            Logger.LogInfo(
                "Optional sc2-hybrid-movement minimap-move bridge detected without adding a dependency.");
        }

        private bool TryExecuteOptionalAttackMove()
        {
            if (!_optionalMovementInteropResolved) ResolveOptionalMovementInterop();
            if (_tryExternalAttackMoveMethod == null)
            {
                Logger.LogDebug(
                    "Attack move binding ignored because compatible sc2-hybrid-movement is not loaded.");
                return false;
            }
            try
            {
                return (bool)_tryExternalAttackMoveMethod.Invoke(null, null);
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Optional hybrid-movement attack move failed: " +
                    error.GetBaseException().Message);
                return false;
            }
        }

        private bool TryIssueOptionalHybridMove(
            int[] unitIds, int logicX, int logicY,
            int screenX, int screenY, bool overTopHalf)
        {
            if (TryIssueOptionalFlatMove(logicX, logicY)) return true;
            if (Director.instance == null || Director.instance.MultiplayerGame ||
                _hybridMovementInstanceField == null ||
                _hybridMovementControllerField == null ||
                _hybridMovementTargetConstructor == null ||
                _hybridMovementIssueMoveMethod == null)
                return false;
            try
            {
                object movementOwner = _hybridMovementInstanceField.GetValue(null);
                object controller = movementOwner == null
                    ? null
                    : _hybridMovementControllerField.GetValue(movementOwner);
                if (controller == null) return false;
                Type vecType = _hybridMovementTargetConstructor.GetParameters()[0].ParameterType;
                object goal = Activator.CreateInstance(
                    vecType, new object[] { logicX + 0.5f, logicY + 0.5f });
                object target = _hybridMovementTargetConstructor.Invoke(
                    new[] { goal, (object)screenX, screenY, overTopHalf });
                _hybridMovementIssueMoveMethod.Invoke(
                    controller, new[] { unitIds, target, (object)false });
                return true;
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Optional hybrid-movement minimap order failed: " +
                    error.GetBaseException().Message);
                return false;
            }
        }

        private bool TryIssueOptionalFlatMove(int logicX, int logicY)
        {
            if (_flatMovementInstanceField == null ||
                _flatMovementKernelField == null ||
                _flatMovementDeferredCommandsField == null ||
                _flatMovementVectorConstructor == null ||
                _flatMovementEnrollSelectedMethod == null ||
                _flatMovementGetSelectedMethod == null ||
                _flatMovementCommandMethod == null ||
                _flatMovementFindMethod == null)
                return false;
            try
            {
                object movementOwner = _flatMovementInstanceField.GetValue(null);
                if (movementOwner == null ||
                    !ReferenceEquals(
                        _flatMovementActiveMapField.GetValue(movementOwner),
                        GameMap.instance))
                    return false;
                ConfigEntry<bool> enabled =
                    _flatMovementEnabledField.GetValue(movementOwner) as ConfigEntry<bool>;
                if (enabled == null || !enabled.Value) return false;

                IEnumerable selectedEnumerable =
                    _flatMovementGetSelectedMethod.Invoke(movementOwner, null) as IEnumerable;
                if (selectedEnumerable == null) return false;
                List<int> selected = new List<int>();
                foreach (object value in selectedEnumerable)
                {
                    if (value is int && (int)value > 0) selected.Add((int)value);
                }
                if (selected.Count == 0) return false;
                int[] selectedIds = selected.Distinct().ToArray();
                _flatMovementEnrollSelectedMethod.Invoke(
                    movementOwner, new object[] { selectedIds });

                object kernel = _flatMovementKernelField.GetValue(movementOwner);
                IDictionary deferred = _flatMovementDeferredCommandsField.GetValue(
                    movementOwner) as IDictionary;
                if (kernel == null || deferred == null) return false;
                object goal = _flatMovementVectorConstructor.Invoke(
                    new object[] { logicX + 0.5f, logicY + 0.5f });
                int commandSerial = (int)_flatMovementCommandMethod.Invoke(
                    kernel, new[] { (object)selectedIds, goal });
                foreach (int unitId in selectedIds)
                {
                    object agent = _flatMovementFindMethod.Invoke(
                        kernel, new object[] { unitId });
                    if (agent != null)
                    {
                        deferred.Remove(unitId);
                        continue;
                    }
                    object pending = Activator.CreateInstance(
                        _flatMovementDeferredCommandType);
                    _flatMovementDeferredGoalField.SetValue(pending, goal);
                    _flatMovementDeferredSerialField.SetValue(
                        pending, commandSerial);
                    deferred[unitId] = pending;
                }
                return true;
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Optional SCDEFlatMovement minimap order failed: " +
                    error.GetBaseException().Message);
                return false;
            }
        }

        private void HandleMinimapMoveClick(FatControler controller)
        {
            bool classic = ConfigSettings.Settings_SH1RTSControls;
            if (controller == null || !Input.GetMouseButtonDown(MinimapControlPlan.MoveButton(classic)) ||
                !CanAcceptMinimapCommand() || _suppressSelectionMouseInput ||
                RadarMousePointField == null || MainControls.instance == null ||
                !MainControls.instance.IsUIVisible || (int)FatControler.currentScene != 2)
                return;

            CrusaderDE.MainViewModel viewModel = CrusaderDE.MainViewModel.Instance;
            if (viewModel == null || viewModel.Show_HUD_Briefing || !viewModel.RadarLoaded ||
                !viewModel.MainUILoaded || viewModel.MPChatVisible) return;

            Noesis.Point mousePoint =
                (Noesis.Point)RadarMousePointField.GetValue(controller);
            if (controller.SHRadarRectSize <= 1 ||
                mousePoint.X < 0f || mousePoint.Y < 0f ||
                mousePoint.X >= controller.SHRadarRectSize ||
                mousePoint.Y >= controller.SHRadarRectSize)
                return;

            int[] selected = GetSelectedUnitIds();
            if (selected.Length == 0) return;
            int logicX;
            int logicY;
            int screenX;
            int screenY;
            bool overTopHalf;
            if (!TryResolveRadarTarget(
                    controller, mousePoint, out logicX, out logicY,
                    out screenX, out screenY, out overTopHalf))
            {
                Logger.LogWarning(
                    "Minimap movement ignored because the current radar projection is unavailable.");
                return;
            }

            EnsureSessionInitialized();
            if (TryIssueOptionalHybridMove(
                    selected, logicX, logicY, screenX, screenY, overTopHalf))
            {
                Logger.LogInfo(string.Format(
                    "Minimap movement queued through optional sc2-hybrid-movement: units={0}, logic=({1},{2}).",
                    selected.Length, logicX, logicY));
                return;
            }

            MinimapMoveOrder order = new MinimapMoveOrder(
                selected, logicX, logicY, screenX, screenY, overTopHalf, classic);
            if (_activeMinimapMove == null)
            {
                _activeMinimapMove = order;
                _minimapMovePhase = 0;
            }
            else
            {
                _queuedMinimapMove = order;
            }
            Logger.LogInfo(string.Format(
                "Minimap move queued through the native pointer pipeline: units={0}, logic=({1},{2}), classic={3}.",
                selected.Length, logicX, logicY, classic));
        }

        private bool TryResolveRadarTarget(
            FatControler controller, Noesis.Point mousePoint,
            out int logicX, out int logicY,
            out int screenX, out int screenY, out bool overTopHalf)
        {
            logicX = 0;
            logicY = 0;
            screenX = 0;
            screenY = 0;
            overTopHalf = false;
            GameMap map = GameMap.instance;
            TilemapManager tilemaps = TilemapManager.instance;
            if (map == null || tilemaps == null || tilemaps.gameTileMap == null ||
                GameMap.tilemapSize <= 1 ||
                map.RadarMapWidth <= 1 || map.RadarMapHeight <= 1 ||
                controller.SHRadarScalar <= 0f)
                return false;

            int mapSize = GameMap.tilemapSize;
            int mapOffset = (GameMap.RAW_MAP_SIZE - mapSize) / 2;
            int radarLogicWidth = Math.Min(mapSize, map.RadarMapWidth);
            int radarLogicHeight = Math.Min(mapSize, map.RadarMapHeight);
            int startX = mapOffset + Math.Max(0, (mapSize - radarLogicWidth) / 2);
            int startY = mapOffset + Math.Max(0, (mapSize - radarLogicHeight) / 2);
            int endX = startX + radarLogicWidth - 1;
            int endY = startY + radarLogicHeight - 1;

            Vector3 p00 = GameToWorld(map, tilemaps.gameTileMap, startX, startY);
            Vector3 p10 = GameToWorld(map, tilemaps.gameTileMap, endX, startY);
            Vector3 p01 = GameToWorld(map, tilemaps.gameTileMap, startX, endY);
            Vector3 p11 = GameToWorld(map, tilemaps.gameTileMap, endX, endY);
            float projectedMinX = Mathf.Min(p00.x, p10.x, p01.x, p11.x);
            float projectedMaxX = Mathf.Max(p00.x, p10.x, p01.x, p11.x);
            float projectedMinY = Mathf.Min(p00.y, p10.y, p01.y, p11.y);
            float projectedMaxY = Mathf.Max(p00.y, p10.y, p01.y, p11.y);
            float radarMinX = Mathf.Lerp(projectedMinX, projectedMaxX, 0.25f);
            float radarMaxX = Mathf.Lerp(projectedMinX, projectedMaxX, 0.75f);
            float radarMinY = Mathf.Lerp(projectedMinY, projectedMaxY, 0.25f);
            float radarMaxY = Mathf.Lerp(projectedMinY, projectedMaxY, 0.75f);
            float radarPixelX = mousePoint.X * controller.SHRadarScalar;
            float radarPixelY = mousePoint.Y * controller.SHRadarScalar;
            float normalizedX = Mathf.Clamp01(radarPixelX / (map.RadarMapWidth - 1f));
            float normalizedY = RadarProjectionPlan.NormalizeNoesisY(
                radarPixelY, map.RadarMapHeight);
            if (!RadarProjectionPlan.TryInverseProjectedPoint(
                    p00.x, p00.y, p10.x, p10.y, p01.x, p01.y,
                    radarMinX, radarMaxX, radarMinY, radarMaxY,
                    normalizedX, normalizedY,
                    startX, endX, startY, endY,
                    out logicX, out logicY))
                return false;

            screenX = map.ScreenCentreTileScreenSpaceX;
            screenY = map.ScreenCentreTileScreenSpaceY;
            overTopHalf = map.overTopHalf;
            return true;
        }

        private static Vector3 GameToWorld(
            GameMap map, Tilemap tilemap, int logicX, int logicY)
        {
            int tileX;
            int tileY;
            map.mapGameTileToTilemapCoord(logicX, logicY, out tileX, out tileY);
            return tilemap.GetCellCenterWorld(new Vector3Int(tileX, tileY, 0));
        }

        private void FlushMinimapMove(ref int mouseLogicX, ref int mouseLogicY)
        {
            if (_activeMinimapMove == null ||
                Director.instance == null || !Director.instance.SimRunning ||
                Director.instance.Paused || _nativeSelectionSequence != null ||
                _pendingSelectionVerification != null)
                return;
            MinimapMoveOrder order = _activeMinimapMove;
            if (order.Classic != ConfigSettings.Settings_SH1RTSControls)
            {
                CancelMinimapMoves();
                return;
            }
            bool press = _minimapMovePhase == 0;
            _nativeMinimapCommandInputActive = true;
            try
            {
                EngineInterface.TroopSelection(
                    MinimapControlPlan.LeftMouseState(order.Classic, press),
                    !order.Classic && press, !order.Classic && !press, order.UnitIds,
                    false, false, EmptyUnitIds,
                    order.ScreenX, order.ScreenY, order.OverTopHalf, EmptyUnitIds);
                // preDLLCallActions returns the cursor's logical tile directly to DLL_RunTick.
                // Supply the minimap target here; camera/render projection remains untouched.
                mouseLogicX = order.LogicX;
                mouseLogicY = order.LogicY;
            }
            finally
            {
                _nativeMinimapCommandInputActive = false;
            }
            if (press)
            {
                _minimapMovePhase = 1;
                return;
            }
            _activeMinimapMove = _queuedMinimapMove;
            _queuedMinimapMove = null;
            _minimapMovePhase = 0;
        }

        private void CancelMinimapMoves()
        {
            _activeMinimapMove = null;
            _queuedMinimapMove = null;
            _minimapMovePhase = 0;
        }

        private bool HandleBarracksRallyClick(EditorDirector director)
        {
            if (director == null || !CanAcceptGameplayHotkey() ||
                !Input.GetMouseButtonDown(1))
                return false;
            if (_lastBarracksRallyFrame == Time.frameCount) return true;

            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            int[] mappers = state == null || state.app_mode != 16 ||
                            state.numSelectedChimps > 0 || _nativeSelectionSequence != null ||
                            _pendingSelectionVerification != null
                ? null
                : BarracksRallyPlan.MappersForMode(state.app_sub_mode);
            if (mappers == null) return false;
            if (OverNoesisUiField != null && (bool)OverNoesisUiField.GetValue(director)) return false;

            GameMap map = GameMap.instance;
            if (map == null) return false;
            Vector3 worldPosition = Vector3.zero;
            Vector3Int tilePosition = Vector3Int.zero;
            int depth = -1;
            map.CalcMapTileFromMousePos(
                Input.mousePosition, ref worldPosition, ref tilePosition, ref depth, false, true);
            GameMapTile tile = map.getMapTile(tilePosition.x, tilePosition.y);
            if (tile == null) return false;

            _lastBarracksRallyFrame = Time.frameCount;
            _consumeBarracksRightClick = true;
            _barracksRallyReleaseFrame = -1;
            int player = GameData.Instance.playerID;
            int placed = 0;
            _issuingBarracksRallyPlacement = true;
            try
            {
                foreach (int mapper in mappers)
                {
                    int started = EngineInterface.StartMapperItem(mapper);
                    if (started < 0) continue;
                    EngineInterface.PlaceMapperItem(
                        started > 0 ? started : mapper,
                        tile.gameMapX, tile.gameMapY, 0, player, true, false, 1);
                    placed++;
                }
            }
            finally
            {
                _issuingBarracksRallyPlacement = false;
            }
            Logger.LogInfo(string.Format(
                "All barracks assembly points placed by right-click: mode={0}, requested={1}, placed={2}, logic=({3},{4}).",
                state.app_sub_mode, mappers.Length, placed, tile.gameMapX, tile.gameMapY));
            return true;
        }

        private void CompleteBarracksSelectOrBuildFallback()
        {
            if (!_pendingBarracksFallbackAction.HasValue) return;
            KeyboardAction action = _pendingBarracksFallbackAction.Value;
            if (!CanAcceptGameplayHotkey())
            {
                CancelBarracksSelectionFallback();
                return;
            }

            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            int expectedMode = BarracksSelectionPlan.BuildingModeForAction(action);
            if (!_pendingBarracksNativeSelectionIssued)
            {
                bool selectionTransactionActive = _nativeSelectionSequence != null ||
                                                  _pendingSelectionVerification != null;
                if (state == null || !BarracksSelectionFallbackPlan.CanIssueNativeSelection(
                        selectionTransactionActive, state.numSelectedChimps))
                {
                    if (Time.frameCount >= _pendingBarracksFallbackDeadlineFrame)
                    {
                        CancelBarracksSelectionFallback();
                        Logger.LogWarning(
                            "Barracks selection cancelled because the troop selection did not clear: " +
                            ActionLabel(action) + ".");
                    }
                    return;
                }

                if (BarracksSelectionFallbackPlan.ShouldEnterPlacement(
                        _pendingBarracksExistenceKnown, _pendingBarracksExists))
                {
                    CancelBarracksSelectionFallback();
                    EnterBarracksPlacement(action);
                    return;
                }

                int selector = BarracksSelectionPlan.NativeSelectorForAction(action);
                int result = EngineInterface.GameAction(
                    Enums.GameActionCommand.SelectBuildingType, selector, -1, 0);
                _pendingBarracksNativeSelectionIssued = true;
                _pendingBarracksFallbackDeadlineFrame =
                    Time.frameCount + BarracksSelectionFallbackPlan.NativeSelectionWaitFrames;
                Logger.LogInfo(
                    string.Format(
                        "Barracks hotkey issued native selector after troop selection cleared: action={0}, selector={1}, result={2}.",
                        ActionLabel(action), selector, result));
                return;
            }

            if (state != null && BarracksSelectionFallbackPlan.IsExpectedSelection(
                    state.app_mode, state.app_sub_mode, expectedMode) &&
                state.numSelectedChimps <= 0)
            {
                CancelBarracksSelectionFallback();
                Logger.LogInfo("Barracks hotkey selected an existing building: " + ActionLabel(action) + ".");
                return;
            }
            if (Time.frameCount < _pendingBarracksFallbackDeadlineFrame) return;

            CancelBarracksSelectionFallback();
            Logger.LogWarning(
                "Barracks selector did not enter the expected production mode; placement was not started because the building exists or its existence could not be verified: " +
                ActionLabel(action) + ".");
        }

        private void BeginBarracksSelectionTransition(KeyboardAction action)
        {
            CancelBarracksSelectionFallback();
            CancelNativeSelectionTransaction();
            MainControls controls = MainControls.instance;
            if (controls != null) controls.StopAllPlacement();
            BeginNativeSelectionSequence(EmptyUnitIds);
            _pendingBarracksFallbackAction = action;
            _pendingBarracksFallbackDeadlineFrame =
                Time.frameCount + BarracksSelectionFallbackPlan.NativeSelectionWaitFrames;
            _pendingBarracksNativeSelectionIssued = false;
            _pendingBarracksExistenceKnown = TryHasOwnedBarracks(action, out _pendingBarracksExists);
            Logger.LogInfo(string.Format(
                "Barracks hotkey queued a synchronized troop-selection clear: action={0}, buildingKnown={1}, buildingExists={2}.",
                ActionLabel(action), _pendingBarracksExistenceKnown, _pendingBarracksExists));
        }

        private void EnterBarracksPlacement(KeyboardAction action)
        {
            int mapper = BarracksSelectionPlan.MapperForAction(action);
            EditorDirector director = EditorDirector.instance;
            if (director == null || mapper < 0 || !EngineInterface.IsMapperAvailable(mapper))
            {
                Logger.LogInfo(
                    "Barracks placement unavailable for: " + ActionLabel(action) + ".");
                return;
            }

            try
            {
                director.placeBuildingInteraction((Enums.eMappers)mapper);
                Logger.LogInfo(
                    "Barracks hotkey entered native placement mode after confirming the building is absent: " +
                    ActionLabel(action) + ".");
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Barracks placement failed for " + ActionLabel(action) + ": " +
                    error.GetBaseException().Message);
            }
        }

        private bool TryHasOwnedBarracks(KeyboardAction action, out bool exists)
        {
            exists = false;
            try
            {
                string actualHash;
                if (!HasSupportedNativeLayout(out actualHash)) return false;
                IntPtr module = GetModuleHandle("CrusaderDE.dll");
                if (module == IntPtr.Zero || GameData.Instance == null ||
                    GameData.Instance.playerID <= 0) return false;

                IntPtr table = IntPtr.Add(module, NativeStructureTableRva);
                int structureCount = Marshal.ReadInt32(
                    IntPtr.Add(table, NativeStructureCountOffset));
                if (structureCount < 1 || structureCount > 100000) return false;
                int owner = GameData.Instance.playerID;
                int structureType = BarracksSelectionPlan.NativeSelectorForAction(action);
                for (int structureId = 1; structureId < structureCount; structureId++)
                {
                    IntPtr record = IntPtr.Add(table, structureId * NativeStructureStride);
                    short status = Marshal.ReadInt16(
                        IntPtr.Add(record, NativeStructureStatusOffset));
                    if (status == 0 || status == 3 ||
                        Marshal.ReadInt16(IntPtr.Add(record, NativeStructureOwnerOffset)) != owner ||
                        Marshal.ReadInt16(IntPtr.Add(record, NativeStructureTypeOffset)) !=
                        structureType) continue;
                    exists = true;
                    break;
                }
                return true;
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Barracks existence check failed safely: " + error.GetBaseException().Message);
                return false;
            }
        }

        private void CancelBarracksSelectionFallback()
        {
            _pendingBarracksFallbackAction = null;
            _pendingBarracksFallbackDeadlineFrame = -1;
            _pendingBarracksNativeSelectionIssued = false;
            _pendingBarracksExistenceKnown = false;
            _pendingBarracksExists = false;
        }

        private void InstallManagedKeyMap(KeyManager manager)
        {
            if (manager == null || FunctionMapField == null) return;
            if (!_enabled.Value)
            {
                RestoreOriginalKeyMap(manager);
                return;
            }
            if (_mappedKeyManager == manager && !_keyMapDirty) return;
            int[,] functionMap = FunctionMapField.GetValue(manager) as int[,];
            if (functionMap == null) return;

            if (_mappedKeyManager != manager)
            {
                _mappedKeyManager = manager;
                _originalFunctionMap = (int[,])functionMap.Clone();
                _keyMapAuditLogged = false;
                _keyMapDirty = true;
            }

            for (int action = 0; action < functionMap.GetLength(0); action++)
            {
                functionMap[action, 0] = -1;
                functionMap[action, 1] = -1;
            }

            // While any options page is open, gameplay keys must stay inert. This also prevents
            // a key captured by the editor from issuing an empty native group command.
            if (_optionsMenuVisible)
            {
                SetNativeBinding(functionMap, KeyboardAction.StopUnits, Enums.KeyFunctions.Stop);
                SetNativeBinding(functionMap, KeyboardAction.PatrolUnits, Enums.KeyFunctions.Patrol);
                functionMap[(int)Enums.KeyFunctions.OptionsMenu, 0] = (int)KeyCode.Escape;
                _keyMapDirty = false;
                return;
            }

            SetNativeBinding(functionMap, KeyboardAction.CameraLeft, Enums.KeyFunctions.Left);
            SetNativeBinding(functionMap, KeyboardAction.CameraRight, Enums.KeyFunctions.Right);
            SetNativeBinding(functionMap, KeyboardAction.CameraUp, Enums.KeyFunctions.Up);
            SetNativeBinding(functionMap, KeyboardAction.CameraDown, Enums.KeyFunctions.Down);

            for (int slot = 0; slot < 10; slot++)
            {
                SetNativeBinding(
                    functionMap,
                    (KeyboardAction)((int)KeyboardAction.ReplaceGroup1 + slot),
                    (Enums.KeyFunctions)NativeCommandPlan.Group(slot, NativeGroupMode.Replace).Function);
            }
            for (int slot = 0; slot < 8; slot++)
            {
                SetNativeBinding(
                    functionMap,
                    (KeyboardAction)((int)KeyboardAction.SaveCamera1 + slot),
                    (Enums.KeyFunctions)((int)Enums.KeyFunctions.SetBookmark0 + slot));
                SetNativeBinding(
                    functionMap,
                    (KeyboardAction)((int)KeyboardAction.RecallCamera1 + slot),
                    (Enums.KeyFunctions)((int)Enums.KeyFunctions.GotoBookmark0 + slot));
            }
            SetNativeBinding(functionMap, KeyboardAction.PauseGame, Enums.KeyFunctions.Pause);
            // Mirror the display, but dispatch once through the contextual UI command path.
            SetNativeBinding(functionMap, KeyboardAction.StopUnits, Enums.KeyFunctions.Stop);
            SetNativeBinding(functionMap, KeyboardAction.PatrolUnits, Enums.KeyFunctions.Patrol);
            SetNativeBinding(functionMap, KeyboardAction.CenterOnKeep, Enums.KeyFunctions.HomeKeep);
            SetNativeBinding(
                functionMap, KeyboardAction.FlattenLandscape, Enums.KeyFunctions.FlattenLandscape);
            SetNativeBinding(functionMap, KeyboardAction.OpenChat, Enums.KeyFunctions.OpenChat);
            SetNativeBinding(functionMap, KeyboardAction.MultiplayerPing, Enums.KeyFunctions.MPPing);
            SetNativeBinding(functionMap, KeyboardAction.ToggleFrameRate, Enums.KeyFunctions.ToggleFrameRate);
            SetNativeBinding(functionMap, KeyboardAction.ToggleGoods, Enums.KeyFunctions.ToggleGoods);
            SetNativeBinding(
                functionMap, KeyboardAction.IncreaseGameSpeed,
                Enums.KeyFunctions.IncreaseEngineSpeed, 0);
            SetNativeBinding(
                functionMap, KeyboardAction.IncreaseGameSpeedKeypad,
                Enums.KeyFunctions.IncreaseEngineSpeed, 1);
            SetNativeBinding(
                functionMap, KeyboardAction.DecreaseGameSpeed,
                Enums.KeyFunctions.DecreaseEngineSpeed, 0);
            SetNativeBinding(
                functionMap, KeyboardAction.DecreaseGameSpeedKeypad,
                Enums.KeyFunctions.DecreaseEngineSpeed, 1);

            // Escape remains a menu/navigation key, not a gameplay shortcut.
            functionMap[(int)Enums.KeyFunctions.OptionsMenu, 0] = (int)KeyCode.Escape;
            _keyMapDirty = false;

            if (!_keyMapAuditLogged)
            {
                _keyMapAuditLogged = true;
                int originalCount = CountMappedKeys(_originalFunctionMap);
                int activeCount = CountMappedKeys(functionMap);
                Logger.LogInfo(string.Format(
                    "Native key map replaced and audited: originalBindings={0}, activeBindings={1} " +
                    "(configured camera/group/bookmark/barracks/utility/speed/native FPS bindings + Escape; " +
                    "group selection is handled before barracks context).",
                    originalCount, activeCount));
            }
        }

        private void RestoreOriginalKeyMap(KeyManager manager)
        {
            if (manager == null || manager != _mappedKeyManager ||
                _originalFunctionMap == null || FunctionMapField == null) return;
            int[,] functionMap = FunctionMapField.GetValue(manager) as int[,];
            if (functionMap != null &&
                functionMap.GetLength(0) == _originalFunctionMap.GetLength(0) &&
                functionMap.GetLength(1) == _originalFunctionMap.GetLength(1))
            {
                Array.Copy(_originalFunctionMap, functionMap, _originalFunctionMap.Length);
            }
            _mappedKeyManager = null;
            _originalFunctionMap = null;
            _keyMapAuditLogged = false;
            _keyMapDirty = true;
        }

        private static int CountMappedKeys(int[,] functionMap)
        {
            if (functionMap == null) return 0;
            int count = 0;
            for (int action = 0; action < functionMap.GetLength(0); action++)
            {
                for (int column = 0; column < functionMap.GetLength(1); column++)
                {
                    if (functionMap[action, column] >= 0) count++;
                }
            }
            return count;
        }

        private void SetNativeBinding(
            int[,] functionMap, KeyboardAction action, Enums.KeyFunctions nativeAction, int column = 0)
        {
            KeyChord chord;
            if (_bindings.TryGet(action, out chord))
            {
                functionMap[(int)nativeAction, column] = chord.ToNativeEncoding();
            }
        }

        private void BindConfiguration()
        {
            BindingModel defaults = new BindingModel();
            defaults.ResetDefaults();
            bool migrateLegacyDefaults = _bindingSchemaVersion.Value < 2;
            int existingSchemaVersion = _bindingSchemaVersion.Value;
            foreach (KeyboardAction action in Actions)
            {
                KeyChord defaultChord;
                bool hasDefault = defaults.TryGet(action, out defaultChord);
                string defaultValue = hasDefault ? defaultChord.Serialize() : "";
                ConfigEntry<string> entry = _persistentConfig.Bind(
                    "Bindings", action.ToString(), defaultValue, ActionLabel(action));
                _bindingConfig[action] = entry;

                // A new default must not steal M from an existing player-customized action.
                if (existingSchemaVersion < 6 && action == KeyboardAction.ToggleGoods &&
                    entry.Value == defaultValue && _bindings.Bindings.Any(pair =>
                        pair.Key != action && pair.Value.Equals(defaultChord))) entry.Value = "";

                string legacyDefault = migrateLegacyDefaults
                    ? BindingMigrationPlan.LegacyDefaultForAction(action)
                    : null;
                if (legacyDefault != null && entry.Value == legacyDefault)
                    entry.Value = defaultValue;
                if (BindingMigrationPlan.ShouldApplyNewDefault(
                        existingSchemaVersion, action, entry.Value))
                    entry.Value = defaultValue;

                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    _bindings.Remove(action);
                    continue;
                }

                KeyChord parsed;
                if (KeyChord.TryParse(entry.Value, out parsed) && parsed.KeyCode != (int)KeyCode.Escape)
                {
                    _bindings.Set(action, parsed);
                }
                else
                {
                    entry.Value = defaultValue;
                    if (!hasDefault) _bindings.Remove(action);
                }
            }
            _bindingSchemaVersion.Value = 6;
            _persistentConfig.Save();
        }

        private ConfigFile ResolvePersistentConfig()
        {
            try
            {
                string managerDataRoot =
                    Environment.GetEnvironmentVariable("SCDEModManagerDataRoot");
                ConfigFile managerConfig = TryCreateManagerConfig(managerDataRoot);
                if (managerConfig != null) return managerConfig;

                DirectoryInfo gameRoot = new DirectoryInfo(Paths.GameRootPath);
                DirectoryInfo stagingRoot = gameRoot.Parent;
                DirectoryInfo managerRoot = stagingRoot == null ? null : stagingRoot.Parent;
                if (stagingRoot != null && managerRoot != null &&
                    string.Equals(stagingRoot.Name, "staging", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(gameRoot.Name, "game", StringComparison.OrdinalIgnoreCase))
                {
                    managerConfig = TryCreateManagerConfig(managerRoot.FullName);
                    if (managerConfig != null) return managerConfig;
                }

                if (gameRoot.Name.EndsWith(
                        " - SCDE Modded", StringComparison.OrdinalIgnoreCase))
                {
                    string localAppData = Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData);
                    managerConfig = TryCreateManagerConfig(Path.Combine(
                        localAppData, "SCDE Mod Manager", "manager-data"));
                    if (managerConfig != null) return managerConfig;
                }
            }
            catch (Exception error)
            {
                Logger.LogWarning("Manager config path was unavailable; using the BepInEx config path: " + error.Message);
            }
            return Config;
        }

        private ConfigFile TryCreateManagerConfig(string managerDataRoot)
        {
            if (String.IsNullOrWhiteSpace(managerDataRoot)) return null;
            string configDirectory = Path.Combine(
                Path.GetFullPath(managerDataRoot), "config");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(
                configDirectory, "sc2-keyboard-control.cfg");
            PersistentConfigurationPlan.CopyIfMissing(Config.ConfigFilePath, configPath);
            Logger.LogInfo("Persistent keyboard settings: " + configPath);
            return new ConfigFile(configPath, true);
        }

        private void StageBinding(KeyboardAction action, KeyChord chord)
        {
            if (chord.KeyCode == (int)KeyCode.Escape)
            {
                _uiStatus = L("Esc_is_reserved_for_the_game_menu_and_cannot_be_assigned_to_gameplay");
                return;
            }
            if (IsCameraAction(action) && (chord.Ctrl || chord.Alt))
            {
                _uiStatus = L("Camera_movement_cannot_use_Ctrl_or_Alt_use_an_unmodified_key_or_Shift");
                return;
            }

            KeyboardAction? displaced = _draftBindings.Set(action, chord);
            _draftDirty = true;
            if (displaced.HasValue)
            {
                _uiStatus = L("Draft_changed_conflicting_action_unbound") +
                            ActionLabel(displaced.Value) +
                            L("Choose_Save_and_Apply_to_activate_it");
            }
            else
            {
                _uiStatus = L("Draft_changed") + ActionLabel(action) +
                            L("Text_") + FormatChord(chord) +
                            L("Choose_Save_and_Apply_to_activate_it_2");
            }
        }

        private void ResetDraftBindings()
        {
            _draftBindings.ResetDefaults();
            _draftDirty = true;
            CancelBindingCapture();
            _uiStatus = L("Defaults_loaded_into_the_draft_W_S_D_do_not_move_the_camera_A_is_reserved_f");
        }

        private void RemovePendingBinding()
        {
            if (!_pendingActionForKey.HasValue)
            {
                _uiStatus = L("Choose_the_action_to_unbind_on_the_right_first");
                return;
            }
            KeyboardAction action = _pendingActionForKey.Value;
            _draftBindings.Remove(action);
            _draftDirty = true;
            CancelBindingCapture();
            _uiStatus = L("Action_unbound_in_draft") +
                        ActionLabel(action) + L("Save_to_apply");
        }

        private void BeginDraftBindings()
        {
            _draftBindings.CopyFrom(_bindings);
            _draftDirty = false;
            _groupCategoryExpanded = false;
            _productionCategoryExpanded = false;
            _buildingCategoryExpanded = false;
            _otherCategoryExpanded = false;
            _actionScroll = Vector2.zero;
            CancelBindingCapture();
        }

        private void ApplyDraftBindings()
        {
            _bindings.CopyFrom(_draftBindings);
            foreach (KeyboardAction action in Actions)
            {
                KeyChord chord;
                _bindingConfig[action].Value = _bindings.TryGet(action, out chord)
                    ? chord.Serialize()
                    : "";
            }
            _persistentConfig.Save();
            _keyMapDirty = true;
            _draftDirty = false;
            CancelBindingCapture();
            _uiStatus = L("Bindings_saved_and_applied");
            Logger.LogInfo("Keyboard binding draft saved and applied.");
        }

        private void DiscardDraftBindings()
        {
            _draftBindings.CopyFrom(_bindings);
            _draftDirty = false;
            CancelBindingCapture();
        }

        private void EnsureSessionInitialized()
        {
            if (_sessionDirector == EditorDirector.instance) return;
            _sessionDirector = EditorDirector.instance;
            CancelBarracksSelectionFallback();
            CancelNativeSelectionTransaction();
            _suppressSelectionMouseInput = false;
            _lastLordSelectionTime = -1f;
            for (int slot = 0; slot < _lastGroupSelectionTimes.Length; slot++)
                _lastGroupSelectionTimes[slot] = -1f;
            foreach (HashSet<int> group in _groups) group.Clear();
            Logger.LogInfo("Control-group state initialized for the current map.");
        }

        private void ExecuteAction(KeyboardAction action)
        {
            if (action == KeyboardAction.StopUnits || action == KeyboardAction.PatrolUnits)
            {
                Noesis.Button button = GetTroopCommandButton(action);
                if (button == null || !button.IsEnabled || button.Command == null ||
                    !button.Command.CanExecute(button.CommandParameter)) return;
                button.Command.Execute(button.CommandParameter);
                return;
            }
            _lastExecutedAction = action;
            _lastExecutedActionFrame = Time.frameCount;
            int slot;
            if (action >= KeyboardAction.ExclusiveGroup1 &&
                action <= KeyboardAction.ExclusiveGroup10)
            {
                PruneControlGroupCache();
            }
            if (TryActionSlot(action, KeyboardAction.ReplaceGroup1, 10, out slot))
            {
                int[] selected = GetSelectedUnitIds();
                _groups[slot].Clear();
                _groups[slot].UnionWith(selected);
                ExecuteNative(NativeCommandPlan.Group(slot, NativeGroupMode.Replace));
                return;
            }
            if (TryActionSlot(action, KeyboardAction.SelectGroup1, 10, out slot))
            {
                SelectCachedControlGroup(slot);
                return;
            }
            if (TryActionSlot(action, KeyboardAction.AddGroup1, 10, out slot))
            {
                int[] selected = GetSelectedUnitIds();
                int[] merged = ControlGroupPlan.Merge(_groups[slot], selected);
                _groups[slot].Clear();
                _groups[slot].UnionWith(merged);
                Logger.LogInfo(string.Format(
                    "Control-group add committed without changing the current selection: group={0}, selectedAdditions={1}, mergedUnits={2}.",
                    GroupLabel(slot), selected.Length, merged.Length));
                return;
            }
            if (TryActionSlot(action, KeyboardAction.ExclusiveGroup1, 10, out slot))
            {
                SetExclusiveGroup(slot, GetSelectedUnitIds());
                return;
            }
            if (action >= KeyboardAction.SelectMercenaryPost &&
                action <= KeyboardAction.SelectCathedral)
            {
                BeginBarracksSelectionTransition(action);
                return;
            }
            if (TryActionSlot(action, KeyboardAction.ProduceSlot1, 8, out slot))
            {
                ProduceFromSelectedBarracks(slot);
                return;
            }
            if (TryActionSlot(action, KeyboardAction.BuildPageCastle, 6, out slot))
            {
                SwitchBuildPage(slot);
                return;
            }
            if (TryActionSlot(action, KeyboardAction.BuildSlot1, BuildMenuPlan.MaxSlots, out slot))
            {
                ActivateBuildSlot(slot);
                return;
            }
            if (action == KeyboardAction.SelectAllMilitary)
            {
                SelectNearbyMilitary();
                return;
            }
            if (action == KeyboardAction.SelectAllMilitaryMap)
            {
                QueueMapWideMilitarySelection();
                return;
            }
            if (action == KeyboardAction.SelectLord)
            {
                SelectLord();
            }
        }

        private void ExitBarracksSelectionForTroopCommand()
        {
            CancelBarracksSelectionFallback();
            MainControls controls = MainControls.instance;
            if (controls != null) controls.StopAllPlacement();
        }

        private void SelectCachedControlGroup(int slot)
        {
            if (_nativeSelectionSequence != null || _pendingSelectionVerification != null ||
                _pendingMapWideMilitarySelection)
            {
                Logger.LogInfo(
                    "Control-group selection ignored because a synchronized native input transaction is still active.");
                return;
            }

            PruneControlGroupCache();
            int[] units = _groups[slot].OrderBy(unitId => unitId).ToArray();
            ExitBarracksSelectionForTroopCommand();
            if (units.Length == 0)
            {
                ExecuteNative(NativeCommandPlan.SelectGroup(slot));
                Logger.LogInfo(
                    "Control-group cache was empty; selection fell back to the game's native group " +
                    GroupLabel(slot) + ".");
                return;
            }

            float now = Time.unscaledTime;
            bool centerCamera = LordSelectionPlan.IsDoubleClick(
                _lastGroupSelectionTimes[slot], now);
            _lastGroupSelectionTimes[slot] = centerCamera ? -1f : now;
            ApplyPersistentSelection(units);
            _centerCameraAfterSelection = centerCamera;
            Logger.LogInfo(string.Format(
                centerCamera
                    ? "Control group {0} submitted; camera will center on confirmed selection after a double press: candidates={1}."
                    : "Control group {0} selected through the synchronized native pipeline: units={1}.",
                GroupLabel(slot), units.Length));
        }

        private void PruneControlGroupCache()
        {
            HashSet<int> liveUnitIds;
            if (!TryReadNativeLiveUnitIds(out liveUnitIds)) return;
            foreach (HashSet<int> group in _groups)
            {
                group.RemoveWhere(objectId => !liveUnitIds.Contains(objectId));
            }
        }

        private void ProduceFromSelectedBarracks(int slot)
        {
            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            int[] chimpTypes = state == null || state.app_mode != 16 ||
                               state.numSelectedChimps > 0
                ? null
                : BarracksProductionPlan.ChimpTypesForMode(state.app_sub_mode);
            if (chimpTypes == null || slot < 0 || slot >= chimpTypes.Length) return;

            int result = EngineInterface.GameAction(
                Enums.GameActionCommand.MakeTroop, 1, chimpTypes[slot], 0);
            Logger.LogInfo(string.Format(
                "Barracks production hotkey executed: mode={0}, slot={1}, chimpType={2}, result={3}.",
                state.app_sub_mode, slot + 1, chimpTypes[slot], result));
        }

        private void SwitchBuildPage(int slot)
        {
            object viewModel;
            object hud;
            int screenId;
            if (!TryGetBuildUi(out viewModel, out hud, out screenId) ||
                slot < 0 || slot >= BuildPageMethodNames.Length) return;

            MethodInfo method = AccessTools.Method(
                hud.GetType(), BuildPageMethodNames[slot],
                new[] { typeof(object), typeof(Noesis.RoutedEventArgs) });
            if (method == null) return;
            method.Invoke(hud, new object[] { null, null });
            Logger.LogInfo("Build page hotkey opened: " + ActionLabel(
                (KeyboardAction)((int)KeyboardAction.BuildPageCastle + slot)) + ".");
        }

        private void ActivateBuildSlot(int slot)
        {
            Noesis.Button button;
            if (!TryGetBuildButton(slot, out button) || button == null ||
                !button.IsVisible || !button.IsEnabled) return;

            object parameter = button.CommandParameter;
            if (button.Command != null)
            {
                if (!button.Command.CanExecute(parameter)) return;
                button.Command.Execute(parameter);
            }
            else
            {
                button.RaiseEvent(new Noesis.RoutedEventArgs(Noesis.Button.ClickEvent, button));
            }
            Logger.LogInfo("Build-menu button hotkey executed: slot=" + (slot + 1) + ".");
        }

        private static bool TryGetBuildUi(out object viewModel, out object hud, out int screenId)
        {
            viewModel = MainViewModelInstanceGetter == null
                ? null
                : MainViewModelInstanceGetter.Invoke(null, null);
            hud = viewModel == null || HudMainField == null ? null : HudMainField.GetValue(viewModel);
            screenId = viewModel == null || BuildScreenIdField == null
                ? -1
                : (int)BuildScreenIdField.GetValue(viewModel);
            return hud != null && screenId >= 0;
        }

        private static bool TryGetBuildButton(int slot, out Noesis.Button button)
        {
            button = null;
            object viewModel;
            object hud;
            int screenId;
            if (slot < 0 || slot >= BuildMenuPlan.MaxSlots ||
                !TryGetBuildUi(out viewModel, out hud, out screenId) ||
                BuildButtonsField == null || BuildIconListsField == null) return false;

            return TryGetBuildButton(hud, screenId, slot, out button);
        }

        private static bool TryGetBuildButton(
            object hud, int screenId, int slot, out Noesis.Button button)
        {
            button = null;
            if (hud == null || slot < 0 || slot >= BuildMenuPlan.MaxSlots ||
                BuildButtonsField == null || BuildIconListsField == null) return false;

            Noesis.Button[] buttons = BuildButtonsField.GetValue(hud) as Noesis.Button[];
            int[,] iconLists = BuildIconListsField.GetValue(hud) as int[,];
            if (buttons == null || iconLists == null || screenId < 0 ||
                screenId >= iconLists.GetLength(0)) return false;
            int skip = BuildMenuPlan.FirstButtonIndex(screenId);
            int visibleSlot = 0;
            for (int index = 0; index < iconLists.GetLength(1); index++)
            {
                int buttonIndex = iconLists[screenId, index];
                if (buttonIndex <= 0) continue;
                if (skip > 0)
                {
                    skip--;
                    continue;
                }
                if (buttonIndex >= buttons.Length) return false;
                Noesis.Button candidate = buttons[buttonIndex];
                if (candidate == null || !candidate.IsVisible ||
                    candidate.ActualWidth <= 0f || candidate.ActualHeight <= 0f) continue;
                if (visibleSlot++ != slot) continue;
                button = candidate;
                return true;
            }
            return false;
        }

        private void DrawProductionHints()
        {
            if (!CanAcceptGameplayHotkey()) return;

            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            string[] buttonFields = state == null || state.app_mode != 16 ||
                                    state.numSelectedChimps > 0
                ? null
                : BarracksProductionPlan.ButtonFieldsForMode(state.app_sub_mode);
            if (buttonFields == null) return;

            try
            {
                object viewModel = MainViewModelInstanceGetter == null
                    ? null
                    : MainViewModelInstanceGetter.Invoke(null, null);
                object panel = viewModel == null || HudBuildingPanelField == null
                    ? null
                    : HudBuildingPanelField.GetValue(viewModel);
                if (panel == null) return;

                GUIStyle style = GetProductionHintStyle();
                GUI.depth = -9000;

                for (int slot = 0; slot < buttonFields.Length; slot++)
                {
                    FieldInfo buttonField;
                    if (!_recruitButtonFields.TryGetValue(buttonFields[slot], out buttonField))
                    {
                        buttonField = AccessTools.Field(panel.GetType(), buttonFields[slot]);
                        _recruitButtonFields[buttonFields[slot]] = buttonField;
                    }

                    Noesis.FrameworkElement button = buttonField == null
                        ? null
                        : buttonField.GetValue(panel) as Noesis.FrameworkElement;
                    if (button == null || !button.IsVisible ||
                        button.ActualWidth <= 0f || button.ActualHeight <= 0f) continue;

                    Noesis.Point topLeft = button.PointToScreen(new Noesis.Point(0f, 0f));
                    Noesis.Point bottomRight = button.PointToScreen(
                        new Noesis.Point(button.ActualWidth, button.ActualHeight));
                    float left = Math.Min(topLeft.X, bottomRight.X);
                    float right = Math.Max(topLeft.X, bottomRight.X);
                    float bottom = Math.Max(topLeft.Y, bottomRight.Y);
                    float width = right - left;
                    if (width < 20f) continue;

                    KeyChord chord;
                    string caption = _bindings.TryGet(
                        (KeyboardAction)((int)KeyboardAction.ProduceSlot1 + slot), out chord)
                        ? FormatChord(chord)
                        : "—";
                    const float hintHeight = 20f;
                    GUI.Label(new Rect(left, bottom - hintHeight, width, hintHeight), caption, style);
                }
                _productionHintErrorLogged = false;
            }
            catch (Exception error)
            {
                if (_productionHintErrorLogged) return;
                _productionHintErrorLogged = true;
                Logger.LogWarning("Production shortcut hints could not be drawn: " + error.Message);
            }
        }

        private static Noesis.Button GetTroopCommandButton(KeyboardAction action)
        {
            object viewModel = MainViewModelInstanceGetter == null ? null : MainViewModelInstanceGetter.Invoke(null, null);
            object panel = viewModel == null || TroopPanelField == null ? null : TroopPanelField.GetValue(viewModel);
            if (panel == null) return null;
            FieldInfo field = action == KeyboardAction.StopUnits ? StopButtonField : PatrolButtonField;
            Noesis.Button button = field == null ? null : field.GetValue(panel) as Noesis.Button;
            if (button != null && button.IsVisible) return button;
            if (action != KeyboardAction.PatrolUnits || PatrolActiveButtonField == null) return null;
            button = PatrolActiveButtonField.GetValue(panel) as Noesis.Button;
            return button != null && button.IsVisible ? button : null;
        }

        private void DrawTroopCommandHints()
        {
            if (!CanAcceptGameplayHotkey() || !CanExecuteInCurrentContext(KeyboardAction.StopUnits)) return;
            try
            {
                GUI.depth = -9000;
                GUIStyle style = GetProductionHintStyle();
                foreach (KeyboardAction action in TroopCommandActions)
                {
                    Noesis.Button button = GetTroopCommandButton(action);
                    Rect rect;
                    if (button == null || !TryGetElementRect(button, out rect)) continue;
                    KeyChord chord;
                    string caption = _bindings.TryGet(action, out chord) ? FormatChord(chord) : "—";
                    GUI.Label(new Rect(rect.x, rect.yMax - 5f, rect.width, 20f), caption, style);
                }
                DrawMilitarySelectionHints(style);
                _troopHintErrorLogged = false;
            }
            catch (Exception error)
            {
                if (_troopHintErrorLogged) return;
                _troopHintErrorLogged = true;
                Logger.LogWarning("Troop command hints could not be drawn: " + error.Message);
            }
        }

        private void DrawMilitarySelectionHints(GUIStyle commandStyle)
        {
            object viewModel = MainViewModelInstanceGetter == null ? null : MainViewModelInstanceGetter.Invoke(null, null);
            object panel = viewModel == null || TroopPanelField == null ? null : TroopPanelField.GetValue(viewModel);
            if (panel == null) return;
            Rect buttonRect;
            Noesis.Button reference = GetTroopCommandButton(KeyboardAction.StopUnits);
            if (reference == null || !TryGetElementRect(reference, out buttonRect)) return;
            float left = buttonRect.x;
            float top = buttonRect.y;
            foreach (FieldInfo field in TroopCommandElementFields)
            {
                Noesis.FrameworkElement element = field.GetValue(panel) as Noesis.FrameworkElement;
                Rect rect;
                if (element == null || !element.IsVisible || !TryGetElementRect(element, out rect)) continue;
                left = Math.Min(left, rect.x);
                top = Math.Min(top, rect.y);
            }
            if (_militarySelectionHintStyle == null)
                _militarySelectionHintStyle = new GUIStyle(commandStyle) { wordWrap = true };
            GUIStyle style = _militarySelectionHintStyle;
            style.fontSize = Math.Max(9, Math.Min(commandStyle.fontSize, (int)(buttonRect.width / 7f)));
            float x = Math.Max(0f, left - buttonRect.width - 4f);
            DrawMilitarySelectionHint(new Rect(x, top, buttonRect.width, buttonRect.height),
                KeyboardAction.SelectAllMilitaryMap, L("Select_All_Military"), style);
            DrawMilitarySelectionHint(new Rect(x, top + buttonRect.height + 4f, buttonRect.width, buttonRect.height),
                KeyboardAction.SelectAllMilitary, L("Select_Nearby_Military"), style);
        }

        private void DrawMilitarySelectionHint(Rect rect, KeyboardAction action, string label, GUIStyle style)
        {
            KeyChord chord;
            string key = _bindings.TryGet(action, out chord) ? FormatChord(chord) : "—";
            key = key.Replace("Ctrl + ", "CTRL+");
            GUI.Label(rect, key + "\n" + label, style);
        }

        private void DrawFlattenHint()
        {
            if (!CanAcceptGameplayHotkey()) return;
            try
            {
                object viewModel = MainViewModelInstanceGetter == null ? null : MainViewModelInstanceGetter.Invoke(null, null);
                object root = viewModel == null || HudRootField == null ? null : HudRootField.GetValue(viewModel);
                Noesis.FrameworkElement radar = root == null || RadarGridField == null
                    ? null : RadarGridField.GetValue(root) as Noesis.FrameworkElement;
                Rect rect;
                if (radar == null || !radar.IsVisible || !TryGetElementRect(radar, out rect)) return;
                KeyChord chord;
                string key = !_bindings.TryGet(KeyboardAction.FlattenLandscape, out chord) ? "—" :
                    chord.KeyCode == (int)KeyCode.BackQuote && !chord.Ctrl && !chord.Shift && !chord.Alt
                    ? "~" : FormatChord(chord);
                string caption = key + "\n" + L("Flatten_Landscape");
                GUIStyle style = GetProductionHintStyle();
                float width = Math.Max(68f, style.CalcSize(new GUIContent(caption)).x + 10f);
                float height = style.lineHeight * 2f + 6f;
                GUI.depth = -9000;
                GUI.Label(new Rect(Math.Max(0f, rect.xMax - width),
                    Math.Max(0f, rect.y - height - 4f), width, height), caption, style);
                _flattenHintErrorLogged = false;
            }
            catch (Exception error)
            {
                if (_flattenHintErrorLogged) return;
                _flattenHintErrorLogged = true;
                Logger.LogWarning("Radar flatten hint could not be drawn: " + error.Message);
            }
        }

        private void DrawBuildHints()
        {
            if (!CanAcceptGameplayHotkey() || !IsBuildMenuContext()) return;

            try
            {
                object viewModel;
                object hud;
                int screenId;
                if (!TryGetBuildUi(out viewModel, out hud, out screenId)) return;

                GUIStyle style = GetProductionHintStyle();
                GUI.depth = -9000;
                for (int slot = 0; slot < BuildMenuPlan.MaxSlots; slot++)
                {
                    Noesis.Button button;
                    if (!TryGetBuildButton(hud, screenId, slot, out button)) continue;
                    Rect rect;
                    if (!TryGetElementRect(button, out rect) || rect.width < 20f) continue;

                    KeyChord chord;
                    string caption = _bindings.TryGet(
                        (KeyboardAction)((int)KeyboardAction.BuildSlot1 + slot), out chord)
                        ? FormatChord(chord)
                        : "—";
                    const float hintHeight = 20f;
                    GUI.Label(new Rect(rect.x, rect.yMax - hintHeight, rect.width, hintHeight), caption, style);
                }

                for (int slot = 0; slot < BuildTabFields.Length; slot++)
                {
                    FieldInfo field = BuildTabFields[slot];
                    Noesis.FrameworkElement tab = field == null
                        ? null
                        : field.GetValue(hud) as Noesis.FrameworkElement;
                    Rect rect;
                    if (tab == null || !tab.IsVisible || !TryGetElementRect(tab, out rect) ||
                        rect.width < 20f) continue;

                    KeyChord chord;
                    string caption = _bindings.TryGet(
                        (KeyboardAction)((int)KeyboardAction.BuildPageCastle + slot), out chord)
                        ? FormatChord(chord)
                        : "—";
                    float hintHeight = Math.Min(18f, rect.height);
                    float hintWidth = Math.Min(rect.width, Math.Max(18f, style.CalcSize(new GUIContent(caption)).x + 6f));
                    GUI.Label(new Rect(Math.Max(0f, rect.x - hintWidth + 3f),
                        rect.y + (rect.height - hintHeight) * 0.5f, hintWidth, hintHeight), caption, style);
                }
                _buildHintErrorLogged = false;
            }
            catch (Exception error)
            {
                if (_buildHintErrorLogged) return;
                _buildHintErrorLogged = true;
                Logger.LogWarning("Build shortcut hints could not be drawn: " + error.Message);
            }
        }

        private static bool TryGetElementRect(Noesis.FrameworkElement element, out Rect rect)
        {
            rect = new Rect();
            if (element == null || element.ActualWidth <= 0f || element.ActualHeight <= 0f) return false;
            Noesis.Point topLeft = element.PointToScreen(new Noesis.Point(0f, 0f));
            Noesis.Point bottomRight = element.PointToScreen(
                new Noesis.Point(element.ActualWidth, element.ActualHeight));
            float left = Math.Min(topLeft.X, bottomRight.X);
            float top = Math.Min(topLeft.Y, bottomRight.Y);
            float right = Math.Max(topLeft.X, bottomRight.X);
            float bottom = Math.Max(topLeft.Y, bottomRight.Y);
            rect = Rect.MinMaxRect(left, top, right, bottom);
            return rect.width > 0f && rect.height > 0f;
        }

        private GUIStyle GetProductionHintStyle()
        {
            if (_productionHintStyle != null &&
                _productionHintStyleScreenHeight == Screen.height)
            {
                return _productionHintStyle;
            }
            _productionHintStyleScreenHeight = Screen.height;
            _productionHintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Math.Max(11, Screen.height / 90),
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            _productionHintStyle.normal.background = _productionHintTexture;
            _productionHintStyle.normal.textColor = Color.white;
            return _productionHintStyle;
        }

        private void SetExclusiveGroup(int targetSlot, int[] selected)
        {
            HashSet<int> selectedSet = new HashSet<int>(selected);
            bool[] changed = new bool[10];
            for (int slot = 0; slot < 10; slot++)
            {
                if (slot == targetSlot) continue;
                int removed = _groups[slot].RemoveWhere(selectedSet.Contains);
                changed[slot] = removed > 0;
            }

            _groups[targetSlot].Clear();
            _groups[targetSlot].UnionWith(selectedSet);
            changed[targetSlot] = true;

            foreach (int slot in Enumerable.Range(0, 10).Where(
                         index => index != targetSlot && changed[index]))
                ExecuteNative(NativeCommandPlan.Group(slot, NativeGroupMode.Delete));
            ExecuteNative(NativeCommandPlan.Group(
                targetSlot, NativeGroupMode.Replace));
            Logger.LogInfo(string.Format(
                "Exclusive control-group assignment committed without changing the current selection: group={0}, units={1}.",
                GroupLabel(targetSlot), selectedSet.Count));
        }

        private void TryCenterCameraOnUnits(IEnumerable<int> unitIds)
        {
            try
            {
                GameData gameData = GameData.Instance;
                GameMap map = GameMap.instance;
                EditorDirector director = EditorDirector.instance;
                if (gameData == null || map == null || CameraControls2D.instance == null) return;
                int player = director == null ? gameData.playerID : director.ActivePlayerID;
                int mapSize = GameMap.tilemapSize;
                string actualHash;
                if (!HasSupportedNativeLayout(out actualHash)) return;
                IntPtr module = GetModuleHandle("CrusaderDE.dll");
                object engineLock = EngineThreadLockField == null
                    ? null
                    : EngineThreadLockField.GetValue(null);
                if (module == IntPtr.Zero || engineLock == null) return;

                RandomUnitCameraTarget target = new RandomUnitCameraTarget();
                lock (engineLock)
                {
                    IntPtr unitManager = IntPtr.Add(module, NativeUnitManagerRva);
                    int count = Marshal.ReadInt32(unitManager);
                    if (!MilitarySelectionPlan.IsNativeUnitCountValid(
                            count, NativeMaximumUnitId)) return;
                    foreach (int unitId in unitIds)
                    {
                        if (unitId <= 0 || unitId >= count) continue;
                        IntPtr unit = IntPtr.Add(
                            unitManager, unitId * NativeUnitStride);
                        int cellX = Marshal.ReadInt16(unit, NativeUnitCellXOffset);
                        int cellY = Marshal.ReadInt16(unit, NativeUnitCellYOffset);
                        int fineX = Marshal.ReadInt16(unit, NativeUnitFineXOffset);
                        int fineY = Marshal.ReadInt16(unit, NativeUnitFineYOffset);
                        double logicX, logicY;
                        if (!GroupCameraPlan.TryGetPosition(
                                Marshal.ReadInt16(unit, NativeUnitActiveOffset),
                                Marshal.ReadByte(unit, NativeUnitOwnerOffset), player,
                                cellX, cellY, fineX, fineY, mapSize, out logicX, out logicY)) continue;
                        target.Consider(unitId, logicX, logicY, _cameraRandom);
                    }
                }
                if (target.Count == 0) return;

                EngineInterface.PlayState cameraState = new EngineInterface.PlayState();
                cameraState.camera_target_x = (short)Math.Max(
                    1, Math.Min(Int16.MaxValue, (int)Math.Round(target.X)));
                cameraState.camera_target_y = (short)Math.Max(
                    1, Math.Min(Int16.MaxValue, (int)Math.Round(target.Y)));
                cameraState.camera_target_z = -123;
                gameData.SetCameraFromGameState(cameraState);
                Logger.LogInfo(string.Format(
                    "Control-group camera target: liveUnits={0}, unit={1}, logic=({2},{3}).",
                    target.Count, target.UnitId, cameraState.camera_target_x, cameraState.camera_target_y));
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Control-group camera centering failed safely: " +
                    error.GetBaseException().Message);
            }
        }

        private void SelectNearbyMilitary()
        {
            if (_nativeSelectionSequence != null || _pendingSelectionVerification != null ||
                _pendingMapWideMilitarySelection)
            {
                Logger.LogInfo("Nearby military selection ignored because a synchronized native input transaction is still active.");
                return;
            }

            GameMap map = GameMap.instance;
            Dictionary<int, Chimp> units = map == null || ChimpsField == null
                ? null
                : ChimpsField.GetValue(map) as Dictionary<int, Chimp>;
            if (units == null)
            {
                Logger.LogWarning("Nearby military selection skipped: rendered unit dictionary is unavailable.");
                return;
            }

            List<int> militaryCandidates = new List<int>();
            foreach (Chimp unit in units.Values)
            {
                if (unit != null && unit.objectID > 0 && unit.gameObjectType != LordChimpType &&
                    IsMilitaryChimpType(unit.gameObjectType))
                {
                    militaryCandidates.Add(unit.objectID);
                }
            }

            int[] candidateIds = militaryCandidates.Distinct().ToArray();
            ExitBarracksSelectionForTroopCommand();
            ApplyPersistentSelection(candidateIds);
            Logger.LogInfo(string.Format(
                "Nearby military selection queued from rendered objects with native owner filtering: totalObjects={0}, militaryCandidates={1}.",
                units.Count, candidateIds.Length));
        }

        private void QueueMapWideMilitarySelection()
        {
            if (_nativeSelectionSequence != null || _pendingSelectionVerification != null ||
                _pendingMapWideMilitarySelection)
            {
                Logger.LogInfo("Map-wide military selection ignored because a synchronized native input transaction is still active.");
                return;
            }

            string actualHash;
            if (!HasSupportedNativeLayout(out actualHash))
            {
                Logger.LogWarning(
                    "Map-wide military selection skipped for an untested CrusaderDE.dll. Expected " +
                    SupportedNativeHash + ", found " + actualHash + ".");
                return;
            }

            ExitBarracksSelectionForTroopCommand();
            _pendingMapWideMilitarySelection = true;
            Logger.LogInfo("Map-wide military selection queued for the next synchronized native boundary.");
        }

        private void PreparePendingMapWideMilitarySelection(EditorDirector director)
        {
            if (!_pendingMapWideMilitarySelection) return;
            _pendingMapWideMilitarySelection = false;

            try
            {
                IntPtr module = GetModuleHandle("CrusaderDE.dll");
                if (module == IntPtr.Zero)
                    throw new InvalidOperationException("CrusaderDE.dll is not loaded");

                int activePlayer = director == null ? 0 : director.ActivePlayerID;
                if (activePlayer <= 0 && GameData.Instance != null)
                    activePlayer = GameData.Instance.playerID;
                if (activePlayer <= 0)
                    throw new InvalidOperationException("active player is unavailable");

                IntPtr unitManager = IntPtr.Add(module, NativeUnitManagerRva);
                long scanStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                int count = Marshal.ReadInt32(unitManager);
                if (!MilitarySelectionPlan.IsNativeUnitCountValid(
                        count, NativeMaximumUnitId))
                    throw new InvalidOperationException(
                        "native unit-table count is outside the supported range: " + count);
                List<int> candidates = new List<int>();
                for (int unitId = 1; unitId < count; unitId++)
                {
                    IntPtr unit = IntPtr.Add(unitManager, unitId * NativeUnitStride);
                    bool active = Marshal.ReadInt16(unit, NativeUnitActiveOffset) != 0;
                    if (!active) continue;
                    int unitType = (ushort)Marshal.ReadInt16(unit, NativeUnitTypeOffset);
                    if (!MilitarySelectionPlan.IsMilitaryType(unitType)) continue;
                    int owner = Marshal.ReadByte(unit, NativeUnitOwnerOffset);
                    if (owner == activePlayer) candidates.Add(unitId);
                }
                double scanMilliseconds =
                    (System.Diagnostics.Stopwatch.GetTimestamp() - scanStarted) * 1000.0 /
                    System.Diagnostics.Stopwatch.Frequency;

                BeginNativeSelectionSequence(candidates.ToArray());
                Logger.LogInfo(string.Format(
                    "Map-wide military selection resolved from the native unit table: activePlayer={0}, scannedRecords={1}, militaryCandidates={2}, nativeScan={3:0.000}ms.",
                    activePlayer, count - 1, candidates.Count, scanMilliseconds));
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Map-wide military selection failed safely: " +
                    error.GetBaseException().Message);
            }
        }

        private bool TryReadNativeLiveUnitIds(out HashSet<int> liveUnitIds)
        {
            liveUnitIds = null;
            try
            {
                HashSet<int> cachedUnitIds = new HashSet<int>();
                foreach (HashSet<int> group in _groups) cachedUnitIds.UnionWith(group);
                if (cachedUnitIds.Count == 0)
                {
                    liveUnitIds = cachedUnitIds;
                    return true;
                }

                string actualHash;
                if (!HasSupportedNativeLayout(out actualHash)) return false;
                IntPtr module = GetModuleHandle("CrusaderDE.dll");
                object engineLock = EngineThreadLockField == null
                    ? null
                    : EngineThreadLockField.GetValue(null);
                if (module == IntPtr.Zero || engineLock == null) return false;

                lock (engineLock)
                {
                    IntPtr unitManager = IntPtr.Add(module, NativeUnitManagerRva);
                    int count = Marshal.ReadInt32(unitManager);
                    if (!MilitarySelectionPlan.IsNativeUnitCountValid(
                            count, NativeMaximumUnitId))
                        return false;
                    HashSet<int> result = new HashSet<int>();
                    foreach (int unitId in cachedUnitIds)
                    {
                        if (unitId <= 0 || unitId >= count) continue;
                        IntPtr unit = IntPtr.Add(unitManager, unitId * NativeUnitStride);
                        if (Marshal.ReadInt16(unit, NativeUnitActiveOffset) != 0)
                            result.Add(unitId);
                    }
                    liveUnitIds = result;
                }
                return true;
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Authoritative control-group cache pruning failed safely: " +
                    error.GetBaseException().Message);
                return false;
            }
        }

        private void SelectLord()
        {
            float now = Time.unscaledTime;
            bool centerCamera = LordSelectionPlan.IsDoubleClick(_lastLordSelectionTime, now);
            bool selectionBusy = _nativeSelectionSequence != null || _pendingSelectionVerification != null ||
                _pendingMapWideMilitarySelection;
            if (selectionBusy && !centerCamera)
            {
                Logger.LogInfo("Lord selection ignored because a synchronized native input transaction is still active.");
                return;
            }

            int lordId = FindLocalLordInNativeTable();
            if (lordId <= 0) return;
            int[] lordCandidates = { lordId };
            _lastLordSelectionTime = centerCamera ? -1f : now;
            if (!selectionBusy)
            {
                ExitBarracksSelectionForTroopCommand();
                ApplyPersistentSelection(lordCandidates);
            }
            if (centerCamera)
                TryCenterCameraOnUnits(lordCandidates);
            Logger.LogInfo(centerCamera
                ? "Lord selected for control and centered after a double press."
                : "Lord selection queued with native owner filtering.");
        }

        private int FindLocalLordInNativeTable()
        {
            string actualHash;
            if (!HasSupportedNativeLayout(out actualHash)) return 0;
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            object engineLock = EngineThreadLockField.GetValue(null);
            EditorDirector director = EditorDirector.instance;
            int player = director == null ? 0 : director.ActivePlayerID;
            if (player <= 0 && GameData.Instance != null) player = GameData.Instance.playerID;
            if (module == IntPtr.Zero || engineLock == null || player <= 0) return 0;
            lock (engineLock)
            {
                IntPtr table = IntPtr.Add(module, NativeUnitManagerRva);
                int count = Marshal.ReadInt32(table);
                if (!MilitarySelectionPlan.IsNativeUnitCountValid(count, NativeMaximumUnitId)) return 0;
                for (int id = 1; id < count; id++)
                {
                    IntPtr unit = IntPtr.Add(table, id * NativeUnitStride);
                    if (Marshal.ReadInt16(unit, NativeUnitActiveOffset) == NativeLiveUnitState &&
                        (ushort)Marshal.ReadInt16(unit, NativeUnitTypeOffset) == LordChimpType &&
                        Marshal.ReadByte(unit, NativeUnitOwnerOffset) == player) return id;
                }
            }
            return 0;
        }

        private void ApplyPersistentSelection(int[] unitIds)
        {
            int[] selection = unitIds == null ? new int[0] : (int[])unitIds.Clone();
            BeginNativeSelectionSequence(selection);
        }

        private void BeginNativeSelectionSequence(int[] selection)
        {
            _centerCameraAfterSelection = false;
            CancelBarracksSelectionFallback();
            ResetNativeSelectionVisualState();
            _nativeSelectionSequence = (int[])selection.Clone();
            _nativeSelectionPhase = 0;
            _nativeSelectionInputActive = false;
            _pendingSelectionVerification = null;
            _selectionVerificationFrame = -1;
            _suppressSelectionMouseInput = true;
        }

        private void PrepareNativeSelectionInput(EditorDirector director)
        {
            if (_nativeSelectionSequence == null) return;
            TroopSelector selector = TroopSelector.instance;
            MainControls controls = MainControls.instance;
            if (director == null || selector == null || controls == null)
            {
                Logger.LogWarning(
                    "Synchronized military selection cancelled because the native input owners are unavailable.");
                CancelNativeSelectionTransaction();
                return;
            }

            try
            {
                NativeSelectionPhaseSpec phase = NativeSelectionPlan.Phase(_nativeSelectionPhase);
                int[] selectedUnits = phase.UseSelectedUnits
                    ? _nativeSelectionSequence
                    : EmptyUnitIds;
                int[] onScreenUnits = phase.UseOnScreenUnits
                    ? _nativeSelectionSequence
                    : EmptyUnitIds;

                SelectedChimpListField.SetValue(director, selectedUnits);
                UnderCursorChimpListField.SetValue(director, EmptyUnitIds);
                OnScreenChimpsListField.SetValue(director, onScreenUnits);
                GotNewSelectionInfoField.SetValue(director, true);
                TroopSelectionBoxOnField.SetValue(director, phase.TroopSelectionBoxOn);
                LeftMouseStateForEngineField.SetValue(director, phase.MouseState);
                StateReadField.SetValue(director, false);
                UpPendingField.SetValue(director, false);
                RightDownForEngineField.SetValue(director, false);
                RightUpForEngineField.SetValue(director, false);
                TroopSelectionOnField.SetValue(selector, phase.SelectionOn);
                TroopSelectionEstablishedField.SetValue(selector, phase.SelectionEstablished);
                controls.CurrentAction = phase.CurrentAction;
                _nativeSelectionInputActive = true;
            }
            catch (Exception error)
            {
                _nativeSelectionInputActive = false;
                CancelNativeSelectionTransaction();
                Logger.LogError("Synchronized military selection input failed: " + error.Message);
            }
        }

        private void CompleteNativeSelectionInput()
        {
            if (!_nativeSelectionInputActive || _nativeSelectionSequence == null) return;
            _nativeSelectionInputActive = false;
            _nativeSelectionPhase++;
            if (_nativeSelectionPhase < NativeSelectionPlan.PhaseCount) return;

            _pendingSelectionVerification = (int[])_nativeSelectionSequence.Clone();
            _selectionVerificationFrame = Time.frameCount + 2;
            _nativeSelectionSequence = null;
            _nativeSelectionPhase = 0;
            ResetNativeSelectionVisualState();
            Logger.LogInfo(
                "Military selection committed through the game's synchronized native input pipeline.");
        }

        private void VerifyPendingSelection()
        {
            if (_pendingSelectionVerification == null ||
                Time.frameCount < _selectionVerificationFrame) return;

            int[] requested = _pendingSelectionVerification;
            bool centerCamera = _centerCameraAfterSelection;
            _centerCameraAfterSelection = false;
            _pendingSelectionVerification = null;
            _selectionVerificationFrame = -1;
            HashSet<int> requestedSet = new HashSet<int>(requested);
            HashSet<int> confirmedSet = new HashSet<int>(GetSelectedUnitIds());
            bool validSubset = NativeSelectionVerification.SelectedUnitsAreCandidateSubset(
                requestedSet, confirmedSet);
            if (centerCamera && validSubset) TryCenterCameraOnUnits(confirmedSet);
            Logger.LogInfo(string.Format(
                "Native-filtered military selection snapshot: candidates={0}, selectedControllable={1}, validSubset={2}.",
                requestedSet.Count, confirmedSet.Count, validSubset));
            if (!validSubset)
            {
                Logger.LogWarning(
                    "Native military selection returned an object outside the submitted candidate set.");
            }
        }

        private void CancelNativeSelectionTransaction()
        {
            _centerCameraAfterSelection = false;
            _pendingMapWideMilitarySelection = false;
            _nativeSelectionSequence = null;
            _nativeSelectionPhase = 0;
            _nativeSelectionInputActive = false;
            _pendingSelectionVerification = null;
            _selectionVerificationFrame = -1;
            ResetNativeSelectionVisualState();
        }

        private void ResetNativeSelectionVisualState()
        {
            EditorDirector director = EditorDirector.instance;
            TroopSelector selector = TroopSelector.instance;
            MainControls controls = MainControls.instance;
            try
            {
                if (director != null)
                {
                    if (TroopSelectionBoxOnField != null)
                        TroopSelectionBoxOnField.SetValue(director, false);
                    if (LeftMouseStateForEngineField != null)
                        LeftMouseStateForEngineField.SetValue(director, 0);
                    if (RightDownForEngineField != null)
                        RightDownForEngineField.SetValue(director, false);
                    if (RightUpForEngineField != null)
                        RightUpForEngineField.SetValue(director, false);
                    if (ScheduleTroopSelectionEndField != null)
                        ScheduleTroopSelectionEndField.SetValue(director, false);
                }
                if (selector != null)
                {
                    if (TroopSelectionOnField != null)
                        TroopSelectionOnField.SetValue(selector, false);
                    if (TroopSelectionEstablishedField != null)
                        TroopSelectionEstablishedField.SetValue(selector, false);
                }
                if (controls != null && (controls.CurrentAction == 8 || controls.CurrentAction == 9))
                    controls.CurrentAction = 0;
            }
            catch (Exception error)
            {
                Logger.LogWarning("Selection visual-state cleanup failed: " + error.Message);
            }
        }

        private void ReleaseSelectionMouseSuppressionWhenSafe()
        {
            if (!_suppressSelectionMouseInput || _nativeSelectionSequence != null) return;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) return;
            ResetNativeSelectionVisualState();
            _suppressSelectionMouseInput = false;
        }

        private static bool IsMilitaryChimpType(int type)
        {
            return MilitarySelectionPlan.IsMilitaryType(type);
        }

        private static int[] GetSelectedUnitIds()
        {
            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            if (state == null || state.selectedChimps == null || state.numSelectedChimps <= 0)
            {
                return new int[0];
            }
            return state.selectedChimps.Take(Math.Min(state.numSelectedChimps, state.selectedChimps.Length))
                .Where(id => id > 0).Distinct().ToArray();
        }

        private static void ExecuteNative(NativeCommandSpec command)
        {
            EngineInterface.GameAction(
                (Enums.KeyFunctions)command.Function,
                command.Value1,
                command.Value2,
                command.Value3);
        }

        private static bool TryActionSlot(
            KeyboardAction action, KeyboardAction first, int count, out int slot)
        {
            slot = (int)action - (int)first;
            return slot >= 0 && slot < count;
        }

        private static bool IsCameraAction(KeyboardAction action)
        {
            return action >= KeyboardAction.CameraLeft && action <= KeyboardAction.CameraDown;
        }

        private static bool IsNativeKeyManagerAction(KeyboardAction action)
        {
            return IsCameraAction(action) ||
                   action >= KeyboardAction.ReplaceGroup1 && action <= KeyboardAction.ReplaceGroup10 ||
                   action >= KeyboardAction.SaveCamera1 && action <= KeyboardAction.SaveCamera8 ||
                   action >= KeyboardAction.RecallCamera1 && action <= KeyboardAction.RecallCamera8 ||
                   action >= KeyboardAction.PauseGame && action <= KeyboardAction.MultiplayerPing ||
                   action == KeyboardAction.ToggleFrameRate ||
                   action == KeyboardAction.ToggleGoods ||
                   action >= KeyboardAction.IncreaseGameSpeed &&
                   action <= KeyboardAction.DecreaseGameSpeedKeypad;
        }

        private KeyboardAction? FindPressedCustomAction()
        {
            CrusaderDE.MainViewModel viewModel = CrusaderDE.MainViewModel.Instance;
            if (viewModel != null && viewModel.MPChatVisible) return null;
            foreach (KeyValuePair<KeyboardAction, KeyChord> pair in _bindings.Bindings)
            {
                if (IsNativeKeyManagerAction(pair.Key) ||
                    !Input.GetKeyDown((KeyCode)pair.Value.KeyCode) ||
                    pair.Value.Ctrl != IsCtrlDown() ||
                    pair.Value.Shift != IsShiftDown() ||
                    pair.Value.Alt != IsAltDown() ||
                    !CanExecuteInCurrentContext(pair.Key)) continue;
                return pair.Key;
            }
            return null;
        }

        private bool CanExecuteInCurrentContext(KeyboardAction action)
        {
            if (action >= KeyboardAction.ProduceSlot1 && action <= KeyboardAction.ProduceSlot8)
                return IsBarracksProductionContext();
            if (action >= KeyboardAction.BuildPageCastle && action <= KeyboardAction.BuildSlot10)
                return IsBuildMenuContext();
            if (action == KeyboardAction.AttackMove ||
                action == KeyboardAction.StopUnits || action == KeyboardAction.PatrolUnits)
            {
                EngineInterface.PlayState state = GameData.Instance.lastGameState;
                return state != null && state.numSelectedChimps > 0;
            }
            return true;
        }

        private static bool IsBarracksProductionContext()
        {
            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            return state != null && state.app_mode == 16 && state.numSelectedChimps <= 0 &&
                   BarracksProductionPlan.ChimpTypesForMode(state.app_sub_mode) != null;
        }

        private static bool IsBuildMenuContext()
        {
            EngineInterface.PlayState state = GameData.Instance.lastGameState;
            return state != null && state.app_mode == (int)Enums.AppModes.APP_MODE_MAIN_GAME &&
                   state.numSelectedChimps <= 0;
        }

        private static bool IsCtrlDown()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static bool IsShiftDown()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool IsAltDown()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
        }

        private bool CanAcceptGameplayHotkey()
        {
            return _enabled.Value && _compatibleBuild && !_optionsMenuVisible &&
                   Director.instance != null && Director.instance.SimRunning &&
                   EditorDirector.instance != null;
        }

        private bool CanAcceptMinimapCommand()
        {
            return _enabled.Value && _compatibleBuild && !_optionsMenuVisible &&
                   Director.instance != null && Director.instance.SimRunning &&
                   !Director.instance.Paused && EditorDirector.instance != null;
        }

        private void ResetOpeningLordAnchor()
        {
            _openingLordSimulationGeneration = 0;
            _openingLordAnchorCaptured = false;
            _openingLordX = 0f;
            _openingLordY = 0f;
            _nextOpeningLordProbeTime = 0f;
            _openingLordUnavailableLogged = false;
            _lastBlockedPlacementFrame = -1;
            _openingPlacementFeedbackUntil = -1f;
        }

        private void UpdateOpeningLordAnchor()
        {
            if (_placementSettings.Policy != BuildingPlacementPolicy.OpeningLordRadius ||
                _openingLordAnchorCaptured ||
                _openingLordSimulationGeneration != _giftedStockpileMapGeneration ||
                Time.unscaledTime < _nextOpeningLordProbeTime ||
                Director.instance == null || !Director.instance.SimRunning)
                return;
            EngineInterface.PlayState state =
                GameData.Instance == null ? null : GameData.Instance.lastGameState;
            if (state != null && !BuildingPlacementPolicyPlan.IsOpeningWindow(
                    state.game_time, _placementSettings.OpeningDurationMinutes))
                return;
            _nextOpeningLordProbeTime = Time.unscaledTime + 0.25f;
            TryCaptureOpeningLordAnchor();
        }

        private bool TryCaptureOpeningLordAnchor()
        {
            if (_openingLordAnchorCaptured) return true;
            try
            {
                string actualHash;
                if (!HasSupportedNativeLayout(out actualHash))
                {
                    LogOpeningLordUnavailableOnce(
                        "unsupported CrusaderDE.dll " + actualHash);
                    return false;
                }
                IntPtr module = GetModuleHandle("CrusaderDE.dll");
                object engineLock = EngineThreadLockField == null
                    ? null
                    : EngineThreadLockField.GetValue(null);
                EditorDirector editor = EditorDirector.instance;
                int activePlayer = editor == null ? 0 : editor.ActivePlayerID;
                if (activePlayer <= 0 && GameData.Instance != null)
                    activePlayer = GameData.Instance.playerID;
                if (module == IntPtr.Zero || engineLock == null || activePlayer <= 0)
                {
                    LogOpeningLordUnavailableOnce(
                        "native unit table or active player is not ready");
                    return false;
                }

                bool found = false;
                float foundX = 0f;
                float foundY = 0f;
                lock (engineLock)
                {
                    IntPtr unitManager = IntPtr.Add(module, NativeUnitManagerRva);
                    int count = Marshal.ReadInt32(unitManager);
                    if (!MilitarySelectionPlan.IsNativeUnitCountValid(
                            count, NativeMaximumUnitId))
                        return false;
                    for (int unitId = 1; unitId < count; unitId++)
                    {
                        IntPtr unit = IntPtr.Add(unitManager, unitId * NativeUnitStride);
                        if (Marshal.ReadInt16(unit, NativeUnitActiveOffset) !=
                                NativeLiveUnitState ||
                            (ushort)Marshal.ReadInt16(unit, NativeUnitTypeOffset) !=
                                LordChimpType ||
                            Marshal.ReadByte(unit, NativeUnitOwnerOffset) != activePlayer)
                            continue;
                        int cellX = Marshal.ReadInt16(unit, NativeUnitCellXOffset);
                        int cellY = Marshal.ReadInt16(unit, NativeUnitCellYOffset);
                        if (cellX < 0 || cellY < 0 || cellX >= GameMap.RAW_MAP_SIZE ||
                            cellY >= GameMap.RAW_MAP_SIZE)
                            continue;
                        int fineX = Marshal.ReadInt16(unit, NativeUnitFineXOffset);
                        int fineY = Marshal.ReadInt16(unit, NativeUnitFineYOffset);
                        foundX = fineX > 0 ? fineX / 8f : cellX + 0.5f;
                        foundY = fineY > 0 ? fineY / 8f : cellY + 0.5f;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
                _openingLordX = foundX;
                _openingLordY = foundY;
                _openingLordAnchorCaptured = true;
                Logger.LogInfo(string.Format(
                    "Opening building anchor frozen at the local lord's first observed position: logic=({0:0.###},{1:0.###}), radius={2}, durationMinutes={3}.",
                    foundX, foundY, _placementSettings.OpeningLordRadius,
                    _placementSettings.OpeningDurationMinutes));
                return true;
            }
            catch (Exception error)
            {
                LogOpeningLordUnavailableOnce(
                    error.GetBaseException().Message);
                return false;
            }
        }

        private void LogOpeningLordUnavailableOnce(string reason)
        {
            if (_openingLordUnavailableLogged) return;
            _openingLordUnavailableLogged = true;
            Logger.LogInfo(
                "Opening lord position is not ready yet; placement remains vanilla until it can be captured: " +
                reason + ".");
        }

        private bool AllowLocalPlacementImpl(
            int mapper, int logicX, int logicY, int player,
            bool inGameNotEditor, int mouseState)
        {
            if (!_enabled.Value || !_compatibleBuild ||
                _issuingBarracksRallyPlacement ||
                _placementSettings.Policy != BuildingPlacementPolicy.OpeningLordRadius ||
                !inGameNotEditor || Director.instance == null ||
                !Director.instance.SimRunning)
                return true;
            MainControls controls = MainControls.instance;
            if (controls == null || !BuildingPlacementPolicyPlan.ShouldRestrictCurrentAction(
                    controls.CurrentAction))
                return true;
            EditorDirector editor = EditorDirector.instance;
            int activePlayer = editor == null ? 0 : editor.ActivePlayerID;
            if (activePlayer <= 0 && GameData.Instance != null)
                activePlayer = GameData.Instance.playerID;
            if (player <= 0 || player != activePlayer) return true;

            EngineInterface.PlayState state =
                GameData.Instance == null ? null : GameData.Instance.lastGameState;
            if (state == null || !BuildingPlacementPolicyPlan.IsOpeningWindow(
                    state.game_time, _placementSettings.OpeningDurationMinutes))
                return true;
            if (!_openingLordAnchorCaptured && !TryCaptureOpeningLordAnchor())
                return true;
            if (BuildingPlacementPolicyPlan.IsWithinLordRadius(
                    _openingLordX, _openingLordY, logicX, logicY,
                    _placementSettings.OpeningLordRadius))
                return true;

            if (_lastBlockedPlacementFrame != Time.frameCount)
            {
                _lastBlockedPlacementFrame = Time.frameCount;
                bool feedbackWasInactive =
                    Time.unscaledTime > _openingPlacementFeedbackUntil;
                ShowNativeTooFarFromCastleFeedback();
                if (feedbackWasInactive)
                {
                    Logger.LogInfo(string.Format(
                        "Opening building placement blocked with native localized feedback: mapper={0}, target=({1},{2}), lord=({3:0.###},{4:0.###}), radius={5}, gameTick={6}.",
                        mapper, logicX, logicY, _openingLordX, _openingLordY,
                        _placementSettings.OpeningLordRadius, state.game_time));
                }
            }
            return false;
        }

        private void ShowNativeTooFarFromCastleFeedback()
        {
            // PlaceMapperItem runs on the simulation thread. Only publish a short-lived
            // request here; the HUD's normal main-thread SetEnginePanelText call is patched
            // to consume it safely during the same frame and the following UI refreshes.
            _openingPlacementFeedbackUntil =
                Time.unscaledTime + PlacementFeedbackHoldSeconds;
        }

        private bool VerifyGameBuild()
        {
            if (FunctionMapField == null || ChimpsField == null || RadarMousePointField == null ||
                RadarMouseHeldField == null ||
                SelectedChimpListField == null ||
                UnderCursorChimpListField == null || OnScreenChimpsListField == null ||
                GotNewSelectionInfoField == null || TroopSelectionBoxOnField == null ||
                LeftMouseStateForEngineField == null || StateReadField == null ||
                UpPendingField == null || RightDownForEngineField == null ||
                RightUpForEngineField == null || TroopSelectionOnField == null ||
                TroopSelectionEstablishedField == null || MainViewModelInstanceGetter == null ||
                HudBuildingPanelField == null || HudMainField == null ||
                BuildScreenIdField == null || HudMainType == null ||
                 BuildButtonsField == null || BuildIconListsField == null ||
                 BuildTabFields.Length != 6 || BuildTabFields.Any(field => field == null) ||
                 AccessTools.Method(typeof(EngineInterface), "loadMap", new[]
                 {
                     typeof(int), typeof(string), typeof(bool), typeof(bool), typeof(int),
                     typeof(int), typeof(bool)
                 }) == null ||
                 AccessTools.Method(typeof(EngineInterface), "LoadSaveFile",
                     new[] { typeof(string) }) == null ||
                 AccessTools.Method(typeof(EngineInterface), "StartMapperItem",
                     new[] { typeof(int) }) == null ||
                 AccessTools.Method(typeof(EngineInterface), "PlaceMapperItem",
                     new[]
                     {
                         typeof(int), typeof(int), typeof(int), typeof(int),
                         typeof(int), typeof(bool), typeof(bool), typeof(int)
                     }) == null ||
                 AccessTools.Method(typeof(Director), "startSimThread", Type.EmptyTypes) == null ||
                 AccessTools.Method(typeof(FatControler), "RadarScrollMap", Type.EmptyTypes) == null ||
                 BuildPageMethodNames.Any(name => AccessTools.Method(
                     HudMainType, name,
                     new[] { typeof(object), typeof(Noesis.RoutedEventArgs) }) == null))
            {
                Logger.LogError(
                    "Required keyboard, production-panel, radar, placement, live-unit, or synchronized-selection members were not found; hooks were disabled.");
                return false;
            }

            string assemblyPath = Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll");
            string actualHash;
            using (FileStream stream = File.OpenRead(assemblyPath))
            using (SHA256 sha = SHA256.Create())
            {
                actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }

            if (actualHash == SupportedAssemblyHash) return true;
            if (!_strictBuildCheck.Value)
            {
                Logger.LogWarning("Untested game build allowed by config. Assembly hash: " + actualHash);
                return true;
            }
            Logger.LogError(
                "Unsupported game build; hooks were disabled. Expected " + SupportedAssemblyHash +
                ", found " + actualHash + ".");
            return false;
        }

        private void RemoveGiftedStockpilesAtNativeBoundaryImpl()
        {
            if (!_pendingGiftedStockpileRemoval) return;
            Director director = Director.instance;
            if (director == null ||
                _giftedStockpileSimulationGeneration != _giftedStockpileMapGeneration) return;
            if (!_enabled.Value || !_compatibleBuild)
            {
                _pendingGiftedStockpileRemoval = false;
                return;
            }

            try
            {
                string actualHash;
                if (!HasSupportedNativeLayout(out actualHash))
                {
                    Logger.LogWarning(
                        "Gifted stockpile removal skipped for an untested CrusaderDE.dll. Expected " +
                        SupportedNativeHash + ", found " + actualHash + ".");
                    _pendingGiftedStockpileRemoval = false;
                    return;
                }

                IntPtr table;
                int localPlayer;
                List<StockpilePoint> stockpiles = ReadStockpiles(
                    director, out table, out localPlayer);
                if (stockpiles.Count == 0)
                {
                    _pendingGiftedStockpileRemoval = false;
                    Logger.LogInfo(string.Format(
                        "No gifted stockpile was present before the first native simulation tick: generation={0}, simTick={1}.",
                        _giftedStockpileMapGeneration, director.getSimTickCount()));
                    return;
                }

                int[] owners = GiftedStockpileRemovalPlan.OwnersWithOneArea(
                    stockpiles, _pendingGiftedStockpileRemoval);
                foreach (IGrouping<int, StockpilePoint> ownerGroup in
                    stockpiles.GroupBy(point => point.Owner).OrderBy(group => group.Key))
                {
                    int areaCount = GiftedStockpileRemovalPlan.CountAreas(ownerGroup.ToList());
                    if (!owners.Contains(ownerGroup.Key))
                    {
                        Logger.LogInfo(string.Format(
                            "Gifted stockpile removal preserved player {0}: components={1}, independentAreas={2}.",
                            ownerGroup.Key, ownerGroup.Count(), areaCount));
                    }
                }
                if (owners.Length == 0)
                {
                    _pendingGiftedStockpileRemoval = false;
                    return;
                }

                StockpilePoint[] targets = stockpiles
                    .Where(point => owners.Contains(point.Owner))
                    .OrderBy(point => point.Owner)
                    .ThenBy(point => point.StructureId)
                    .ToArray();
                IntPtr module = GetModuleHandle("CrusaderDE.dll");
                if (module == IntPtr.Zero)
                    throw new InvalidOperationException("CrusaderDE.dll is not loaded");
                if (_removeStockpilesForOwner == null)
                {
                    _removeStockpilesForOwner =
                        (NativeRemoveStockpilesForOwner)Marshal.GetDelegateForFunctionPointer(
                            IntPtr.Add(module, NativeRemoveStockpilesForOwnerRva),
                            typeof(NativeRemoveStockpilesForOwner));
                }

                foreach (int owner in owners)
                    _removeStockpilesForOwner(table, owner);

                int remaining = 0;
                foreach (StockpilePoint point in targets)
                {
                    IntPtr record = IntPtr.Add(
                        table, point.StructureId * NativeStructureStride);
                    short status = Marshal.ReadInt16(
                        IntPtr.Add(record, NativeStructureStatusOffset));
                    if (status != 0 && status != 3) remaining++;
                }
                _pendingGiftedStockpileRemoval = false;
                if (remaining == 0)
                {
                    Logger.LogInfo(string.Format(
                        "Direct native gifted-stockpile cleanup completed before DLL_RunTick: generation={0}, simTick={1}, multiplayer={2}, localPlayer={3}, owners={4}, components={5}, remainingComponents=0, nativeRva=0x{6:X}.",
                        _giftedStockpileMapGeneration, director.getSimTickCount(),
                        director.MultiplayerGame, localPlayer, string.Join(",", owners),
                        targets.Length, NativeRemoveStockpilesForOwnerRva));
                }
                else
                {
                    Logger.LogWarning(string.Format(
                        "Direct native gifted-stockpile cleanup returned with live components: generation={0}, simTick={1}, remainingComponents={2}; no delayed retry or mouse input is scheduled.",
                        _giftedStockpileMapGeneration, director.getSimTickCount(), remaining));
                }
            }
            catch (Exception error)
            {
                Logger.LogWarning(string.Format(
                    "Direct native gifted-stockpile cleanup failed safely before DLL_RunTick: {0}; no delayed retry or mouse input is scheduled.",
                    error.GetBaseException().Message));
                _pendingGiftedStockpileRemoval = false;
            }
        }

        private List<StockpilePoint> ReadStockpiles(
            Director director, out IntPtr table, out int localPlayer)
        {
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            if (module == IntPtr.Zero)
                throw new InvalidOperationException("CrusaderDE.dll is not loaded");

            table = IntPtr.Add(module, NativeStructureTableRva);
            int structureCount = Marshal.ReadInt32(
                IntPtr.Add(table, NativeStructureCountOffset));
            if (structureCount < 1 || structureCount > 100000)
                throw new InvalidOperationException("native structure count is outside its valid range");
            localPlayer = GameData.Instance == null ? 0 : GameData.Instance.playerID;
            if (!director.MultiplayerGame && localPlayer <= 0) localPlayer = 1;
            List<StockpilePoint> stockpiles = new List<StockpilePoint>();

            for (int structureId = 1; structureId < structureCount; structureId++)
            {
                IntPtr record = IntPtr.Add(table, structureId * NativeStructureStride);
                short status = Marshal.ReadInt16(
                    IntPtr.Add(record, NativeStructureStatusOffset));
                if (status == 0 || status == 3) continue;
                if (Marshal.ReadInt16(IntPtr.Add(record, NativeStructureTypeOffset)) !=
                    StockpileStructureType) continue;
                int owner = Marshal.ReadInt16(IntPtr.Add(record, NativeStructureOwnerOffset));
                int x = Marshal.ReadInt16(IntPtr.Add(record, NativeStructureXOffset));
                int y = Marshal.ReadInt16(IntPtr.Add(record, NativeStructureYOffset));
                if (owner <= 0 || x < 0 || x > 799 || y < 0 || y > 799) continue;
                if (!director.MultiplayerGame && owner != localPlayer) continue;
                stockpiles.Add(new StockpilePoint(structureId, owner, x, y));
            }
            return stockpiles;
        }

        private bool HasSupportedNativeLayout(out string actualHash)
        {
            if (!_nativeLayoutHashChecked)
            {
                string nativePath = Path.Combine(
                    Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data",
                    "Plugins", "x86_64", "CrusaderDE.dll");
                using (FileStream stream = File.OpenRead(nativePath))
                using (SHA256 sha = SHA256.Create())
                {
                    _nativeLayoutActualHash = BitConverter.ToString(sha.ComputeHash(stream))
                        .Replace("-", "").ToLowerInvariant();
                }
                _nativeLayoutHashSupported = _nativeLayoutActualHash == SupportedNativeHash;
                _nativeLayoutHashChecked = true;
            }
            actualHash = _nativeLayoutActualHash;
            return _nativeLayoutHashSupported;
        }

        private void UpdateEnemyExclusionRadiusPatch(bool shouldEnable)
        {
            if (!shouldEnable)
            {
                RestoreEnemyExclusionRadiusPatch();
                return;
            }
            if (_enemyExclusionRadiusPatchReady || _nativePatchIncompatible ||
                Time.unscaledTime < _nextNativePatchProbeTime) return;

            _nextNativePatchProbeTime = Time.unscaledTime + 1f;
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            if (module == IntPtr.Zero)
            {
                if (!_nativePatchWaitingLogged)
                {
                    _nativePatchWaitingLogged = true;
                    Logger.LogInfo(string.Format(
                        "Waiting for CrusaderDE.dll before applying the {0}-tile enemy building/unit exclusion radius.",
                        _enemyExclusionRadius));
                }
                return;
            }

            try
            {
                string actualHash;
                if (!HasSupportedNativeLayout(out actualHash))
                {
                    _nativePatchIncompatible = true;
                    Logger.LogWarning(
                        "Enemy building/unit exclusion patch disabled for an untested CrusaderDE.dll. Expected " +
                        SupportedNativeHash + ", found " + actualHash + ". Other keyboard features remain enabled.");
                    return;
                }

                bool allOriginal = true;
                bool allPatched = true;
                for (int index = 0; index < _enemyExclusionRadiusPatchSpecs.Length; index++)
                {
                    NativePatchSpec spec = _enemyExclusionRadiusPatchSpecs[index];
                    IntPtr address = IntPtr.Add(module, spec.Rva);
                    _enemyExclusionRadiusPatchAddresses[index] = address;
                    byte[] current = ReadNativeBytes(address, spec.Original.Length);
                    allOriginal &= current.SequenceEqual(spec.Original);
                    allPatched &= current.SequenceEqual(spec.Patched);
                }
                if (allPatched)
                {
                    _enemyExclusionRadiusPatchReady = true;
                    Logger.LogInfo(string.Format(
                        "The native {0}-tile enemy building/unit exclusion patch is already active.",
                        _enemyExclusionRadius));
                    return;
                }
                if (!allOriginal)
                {
                    _nativePatchIncompatible = true;
                    Logger.LogWarning(
                        "Enemy building/unit exclusion patch disabled because one or more locked native sites do not match. " +
                        "Other keyboard features remain enabled.");
                    return;
                }

                try
                {
                    for (int index = 0; index < _enemyExclusionRadiusPatchSpecs.Length; index++)
                    {
                        NativePatchSpec spec = _enemyExclusionRadiusPatchSpecs[index];
                        IntPtr address = _enemyExclusionRadiusPatchAddresses[index];
                        WriteNativeBytes(address, spec.Patched);
                        _enemyExclusionRadiusPatchOwned[index] = true;
                        if (!ReadNativeBytes(address, spec.Patched.Length).SequenceEqual(spec.Patched))
                            throw new InvalidOperationException(
                                "native radius bytes did not verify after writing RVA 0x" +
                                spec.Rva.ToString("X"));
                    }
                }
                catch
                {
                    RestoreEnemyExclusionRadiusPatch();
                    throw;
                }

                _enemyExclusionRadiusPatchReady = true;
                Logger.LogInfo(string.Format(
                    "Native enemy building and military-unit exclusion radius set to {0} logic tiles for all placement paths.",
                    _enemyExclusionRadius));
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Enemy building/unit exclusion patch is not ready and will retry: " +
                    error.GetBaseException().Message);
            }
        }

        private void RestoreEnemyExclusionRadiusPatch()
        {
            if (!_enemyExclusionRadiusPatchReady &&
                (_enemyExclusionRadiusPatchOwned == null ||
                 !_enemyExclusionRadiusPatchOwned.Any(owned => owned)))
                return;
            bool restoredAny = false;
            try
            {
                for (int index = _enemyExclusionRadiusPatchSpecs.Length - 1; index >= 0; index--)
                {
                    if (!_enemyExclusionRadiusPatchOwned[index] ||
                        _enemyExclusionRadiusPatchAddresses[index] == IntPtr.Zero) continue;
                    NativePatchSpec spec = _enemyExclusionRadiusPatchSpecs[index];
                    IntPtr address = _enemyExclusionRadiusPatchAddresses[index];
                    byte[] current = ReadNativeBytes(address, spec.Patched.Length);
                    if (current.SequenceEqual(spec.Patched))
                        WriteNativeBytes(address, spec.Original);
                    _enemyExclusionRadiusPatchOwned[index] = false;
                    restoredAny = true;
                }
                if (restoredAny)
                    Logger.LogInfo("Native enemy building/unit exclusion instructions restored.");
            }
            catch (Exception error)
            {
                Logger.LogWarning(
                    "Could not restore the native enemy building/unit exclusion instructions: " +
                    error.GetBaseException().Message);
            }
            finally
            {
                _enemyExclusionRadiusPatchReady = false;
            }
        }

        private static byte[] ReadNativeBytes(IntPtr address, int length)
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(address, bytes, 0, length);
            return bytes;
        }

        private static void WriteNativeBytes(IntPtr address, byte[] bytes)
        {
            uint oldProtection;
            if (!VirtualProtect(address, (UIntPtr)bytes.Length, 0x40, out oldProtection))
                throw new InvalidOperationException("VirtualProtect(PAGE_EXECUTE_READWRITE) failed");
            try
            {
                Marshal.Copy(bytes, 0, address, bytes.Length);
                FlushInstructionCache(GetCurrentProcess(), address, (UIntPtr)bytes.Length);
            }
            finally
            {
                uint ignored;
                VirtualProtect(address, (UIntPtr)bytes.Length, oldProtection, out ignored);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeRemoveStockpilesForOwner(
            IntPtr structureTable, int owner);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(
            IntPtr address, UIntPtr size, uint newProtection, out uint oldProtection);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(
            IntPtr process, IntPtr baseAddress, UIntPtr size);

        private void DrawKeyboardSettingsEntry()
        {
            Rect entry;
            if (!TryGetKeyboardLauncherRect(out entry)) return;
            GUI.depth = -10000;
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.25f, 0.85f, 1f);
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Math.Max(15, Screen.height / 58),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            if (_settingsCloseClickActive) GUI.Box(entry, L("Keyboard_Settings"), style);
            else if (GUI.Button(entry, L("Keyboard_Settings"), style))
            {
                OpenKeyboardSettings();
            }
            GUI.backgroundColor = previous;

            Event current = Event.current;
            if (current != null && entry.Contains(current.mousePosition) &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseUp))
            {
                current.Use();
            }
        }

        private bool TryGetKeyboardLauncherRect(out Rect rect)
        {
            rect = new Rect();
            try
            {
                var root = _activeOptionsRoot as Noesis.FrameworkElement;
                if (root == null) return false;
                if (_optionsMenuPanel == null)
                    _optionsMenuPanel = root.FindName("LayoutRoot") as Noesis.FrameworkElement;
                Rect menu;
                // HUD_Options itself may cover the whole screen; anchor to its centered panel.
                if (!TryGetElementRect(_optionsMenuPanel, out menu)) return false;
                var entry = SettingsLauncherPlan.BesideMenu(menu.xMax, menu.center.y, menu.height,
                    Screen.width, Screen.height);
                rect = new Rect(entry.Left, entry.Top, entry.Width, entry.Height);
                return true;
            }
            catch (Exception error)
            {
                if (!_optionsAnchorWarningLogged)
                {
                    _optionsAnchorWarningLogged = true;
                    Logger.LogWarning("Settings menu anchor unavailable: " + error.Message);
                }
                return false;
            }
        }

        private void HandleKeyboardLauncherClick()
        {
            if (_settingsCloseClickActive || !Input.GetMouseButtonDown(0)) return;
            Vector3 mouse = Input.mousePosition;
            Vector2 guiMouse = new Vector2(mouse.x, Screen.height - mouse.y);
            Rect entry;
            if (TryGetKeyboardLauncherRect(out entry) && entry.Contains(guiMouse)) OpenKeyboardSettings();
        }

        private void OpenKeyboardSettings()
        {
            if (_settingsPageVisible) return;
            BeginDraftBindings();
            _settingsPageVisible = true;
            HideNativeOptions();
            _uiStatus = L("Choose_an_action_on_the_right_then_press_one_key_combination_Changes_remain");
            Logger.LogInfo("Virtual keyboard settings opened.");
        }

        private void HideNativeOptions()
        {
            if (_activeOptionsRoot == null || ReferenceEquals(_hiddenOptionsRoot, _activeOptionsRoot)) return;
            RestoreNativeOptions();
            _hiddenOptionsRoot = _activeOptionsRoot;
            Type type = _hiddenOptionsRoot.GetType();
            _optionsOpacityProperty = type.GetProperty("Opacity");
            _optionsHitTestProperty = type.GetProperty("IsHitTestVisible");
            if (_optionsOpacityProperty != null)
            {
                _originalOptionsOpacity = _optionsOpacityProperty.GetValue(_hiddenOptionsRoot, null);
                _optionsOpacityProperty.SetValue(_hiddenOptionsRoot, 0f, null);
            }
            if (_optionsHitTestProperty != null)
            {
                _originalOptionsHitTest = _optionsHitTestProperty.GetValue(_hiddenOptionsRoot, null);
                _optionsHitTestProperty.SetValue(_hiddenOptionsRoot, false, null);
            }
        }

        private void RestoreNativeOptions()
        {
            if (_hiddenOptionsRoot == null) return;
            try
            {
                if (_optionsOpacityProperty != null)
                    _optionsOpacityProperty.SetValue(_hiddenOptionsRoot, _originalOptionsOpacity, null);
                if (_optionsHitTestProperty != null)
                    _optionsHitTestProperty.SetValue(_hiddenOptionsRoot, _originalOptionsHitTest, null);
            }
            catch
            {
                // The native options page may already have been destroyed while closing.
            }
            _hiddenOptionsRoot = null;
        }

        private void CapturePhysicalBinding()
        {
            if (!_pendingActionForKey.HasValue) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelBindingCapture();
                _uiStatus = L("This_binding_change_was_cancelled");
                return;
            }
            foreach (KeyVisual key in AssignableKeys)
            {
                if (!Input.GetKeyDown((KeyCode)key.KeyCode)) continue;
                _pendingCtrl = _pendingCtrl || IsCtrlDown();
                _pendingShift = _pendingShift || IsShiftDown();
                _pendingAlt = _pendingAlt || IsAltDown();
                SelectKey(key.KeyCode);
                return;
            }
        }

        private KeyChord SelectedChord()
        {
            return new KeyChord(_selectedKeyCode, _pendingCtrl, _pendingShift, _pendingAlt);
        }

        private void SelectKey(int keyCode)
        {
            if (!_pendingActionForKey.HasValue)
            {
                _uiStatus = L("Choose_the_action_to_edit_on_the_right_first");
                return;
            }
            if (keyCode == (int)KeyCode.Escape)
            {
                CancelBindingCapture();
                _uiStatus = L("This_binding_change_was_cancelled");
                return;
            }
            _selectedKeyCode = keyCode;
            _clickedKeyCode = keyCode;
            _clickedKeyUntil = Time.unscaledTime + 0.25f;
            CompleteBinding(_pendingActionForKey.Value, SelectedChord());
        }

        private void BeginOneShotCapture(KeyboardAction action)
        {
            CancelBindingCapture();
            _pendingActionForKey = action;
            _uiStatus = L("Waiting_for_one_input") + ActionLabel(action) +
                        L("You_may_lock_Ctrl_Shift_or_Alt_before_choosing_a_key");
            Logger.LogInfo("One-shot binding capture armed: " + ActionLabel(action) + ".");
        }

        private void CompleteBinding(KeyboardAction action, KeyChord chord)
        {
            StageBinding(action, chord);
            CancelBindingCapture();
        }

        private void CancelBindingCapture()
        {
            _pendingActionForKey = null;
            _selectedKeyCode = 0;
            _pendingCtrl = false;
            _pendingShift = false;
            _pendingAlt = false;
        }

        private void DrawKeyboardSettings()
        {
            float margin = 18f;
            Rect panel = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height - margin * 2f);
            GUI.depth = -10000;
            GUI.Box(panel, "");

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Math.Max(18, Screen.height / 45),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Math.Max(12, Screen.height / 75),
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(panel.x + 18f, panel.y + 10f, panel.width - 36f, 30f),
                L("SC2_Style_Full_Keyboard_Settings"), titleStyle);
            // Use the same physical-input route as the action rows; Noesis can consume IMGUI clicks.
            GUI.Box(SettingsCloseButtonRect(true), L("Save_and_Apply"), GUI.skin.button);
            GUI.Box(SettingsCloseButtonRect(false), L("Discard_and_Close"), GUI.skin.button);
            string selectedText = _pendingActionForKey.HasValue
                ? ActionLabel(_pendingActionForKey.Value)
                : L("None");
            string modifierText = string.Format(
                L("Modifier_Status"),
                _pendingCtrl ? L("Locked") : L("Off"),
                _pendingShift ? L("Locked") : L("Off"),
                _pendingAlt ? L("Locked") : L("Off"));
            GUI.Label(new Rect(panel.x + 18f, panel.y + 40f, panel.width - 36f, 26f),
                L("Action_awaiting_input") + selectedText + "    |    " + modifierText +
                "    |    " + (_draftDirty
                    ? L("Unsaved_changes")
                    : L("No_unsaved_changes")), statusStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 65f, panel.width - 36f, 26f),
                _uiStatus, statusStyle);

            float actionWidth = Math.Max(300f, panel.width * 0.25f);
            Rect keyboardRect = new Rect(
                panel.x + 16f, panel.y + 100f, panel.width - actionWidth - 42f, panel.height - 117f);
            Rect actionRect = new Rect(
                keyboardRect.xMax + 12f, panel.y + 100f, actionWidth, panel.height - 117f);
            DrawVirtualKeyboard(keyboardRect);
            DrawActionList(actionRect);

            Event current = Event.current;
            if (current != null && panel.Contains(current.mousePosition) &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseUp))
            {
                current.Use();
            }
        }

        private void DrawVirtualKeyboard(Rect area)
        {
            GUI.Box(area, L("Full_104_Key_Keyboard_Mouse_Side_Buttons"));
            float top = area.y + 30f;
            float rowHeight = Math.Min(58f, (area.height - 40f) / KeyboardRows.Length);
            float unit = (area.width - 16f) / 23.5f;

            for (int rowIndex = 0; rowIndex < KeyboardRows.Length; rowIndex++)
            {
                float x = area.x + 8f;
                foreach (KeyVisual key in KeyboardRows[rowIndex])
                {
                    float width = unit * key.Width;
                    if (key.KeyCode > 0)
                    {
                        Rect keyRect = new Rect(x + 2f, top + rowIndex * rowHeight + 2f,
                            Math.Max(12f, width - 4f), Math.Max(18f, rowHeight - 5f));
                        DrawVirtualKey(keyRect, key);
                    }
                    x += width;
                }
            }
        }

        private void DrawVirtualKey(Rect rect, KeyVisual key)
        {
            bool physicalDown = Input.GetKey((KeyCode)key.KeyCode);
            bool clicked = key.KeyCode == _clickedKeyCode && Time.unscaledTime < _clickedKeyUntil;
            bool pending = key.Modifier == ModifierKind.Ctrl && _pendingCtrl ||
                           key.Modifier == ModifierKind.Shift && _pendingShift ||
                           key.Modifier == ModifierKind.Alt && _pendingAlt;
            bool modifierCtrl = _pendingCtrl || IsCtrlDown();
            bool modifierShift = _pendingShift || IsShiftDown();
            bool modifierAlt = _pendingAlt || IsAltDown();
            string mappedLabel = FindDraftActionsLabelForChord(
                new KeyChord(key.KeyCode, modifierCtrl, modifierShift, modifierAlt));

            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Math.Max(9, Screen.height / 90),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            Texture2D background = _keyNormalTexture;
            if (physicalDown || clicked) background = _keyPressedTexture;
            else if (pending) background = _keyModifierTexture;
            else if (!string.IsNullOrEmpty(mappedLabel)) background = _keyMappedTexture;
            style.normal.background = background;
            style.hover.background = background;
            style.focused.background = background;
            style.active.background = _keyPressedTexture;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.active.textColor = Color.white;

            string caption = pending
                ? key.Label + "\n" + L("Locked")
                : !string.IsNullOrEmpty(mappedLabel)
                ? key.Label + "\n" + mappedLabel
                : key.Label;
            if (GUI.Button(rect, caption, style))
            {
                _clickedKeyCode = key.KeyCode;
                _clickedKeyUntil = Time.unscaledTime + 0.25f;
                if (key.Modifier != ModifierKind.None)
                {
                    if (_pendingActionForKey.HasValue)
                        TogglePendingModifier(key.Modifier);
                    else
                        _uiStatus = L("Choose_an_action_on_the_right_before_locking_Ctrl_Shift_or_Alt");
                }
                else
                {
                    SelectKey(key.KeyCode);
                }
            }
        }

        private void DrawActionList(Rect area)
        {
            GUI.Box(area, L("Configurable_Actions"));
            Rect buttons = new Rect(area.x + 8f, area.y + 27f, area.width - 16f, 30f);
            if (GUI.Button(new Rect(buttons.x, buttons.y, buttons.width * 0.48f, buttons.height),
                L("Unbind_Selected")))
            {
                RemovePendingBinding();
            }
            if (GUI.Button(new Rect(buttons.x + buttons.width * 0.52f, buttons.y,
                buttons.width * 0.48f, buttons.height), L("Restore_Defaults")))
            {
                ResetDraftBindings();
            }

            Rect view = new Rect(area.x + 7f, area.y + 64f, area.width - 14f, area.height - 72f);
            int rowCount = 4 +
                           (_groupCategoryExpanded ? GroupAndCameraActions.Length : 0) +
                           (_productionCategoryExpanded ? ProductionActions.Length : 0) +
                           (_buildingCategoryExpanded ? BuildingActions.Length : 0) +
                           (_otherCategoryExpanded ? OtherActions.Length : 0);
            Rect content = new Rect(0f, 0f, view.width - 18f, rowCount * 34f + 4f);
            GUIStyle categoryStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 4, 2, 2),
                fontStyle = FontStyle.Bold
            };
            GUIStyle actionStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _settingsHitTargets.Clear();
            _actionScroll = GUI.BeginScrollView(view, _actionScroll, content);
            float y = 0f;
            DrawActionCategory(
                L("Groups_and_Camera"), 0, GroupAndCameraActions, _groupCategoryExpanded,
                view, content.width, categoryStyle, actionStyle, ref y);
            DrawActionCategory(
                L("Production"), 1, ProductionActions, _productionCategoryExpanded,
                view, content.width, categoryStyle, actionStyle, ref y);
            DrawActionCategory(
                L("Building"), 2, BuildingActions, _buildingCategoryExpanded,
                view, content.width, categoryStyle, actionStyle, ref y);
            DrawActionCategory(
                L("Other"), 3, OtherActions, _otherCategoryExpanded,
                view, content.width, categoryStyle, actionStyle, ref y);
            GUI.EndScrollView();
        }

        private void DrawActionCategory(
            string label, int category, KeyboardAction[] actions, bool expanded,
            Rect view, float width, GUIStyle categoryStyle, GUIStyle actionStyle, ref float y)
        {
            Rect categoryRow = new Rect(0f, y, width, 30f);
            GUI.Label(categoryRow, (expanded ? "▼  " : "▶  ") + label, categoryStyle);
            AddSettingsHitTarget(categoryRow, view, new SettingsHitTarget(category));
            y += 34f;
            if (!expanded) return;

            foreach (KeyboardAction action in actions)
            {
                KeyChord chord;
                string binding = _draftBindings.TryGet(action, out chord)
                    ? FormatChord(chord)
                    : L("Unbound");
                Rect actionRow = new Rect(0f, y, width, 30f);
                GUIStyle rowStyle = actionStyle;
                if (_pendingActionForKey == action)
                {
                    rowStyle = new GUIStyle(actionStyle);
                    rowStyle.normal.background = _keyPressedTexture;
                    rowStyle.normal.textColor = Color.white;
                }
                GUI.Label(actionRow, ActionLabel(action) + "    [" + binding + "]", rowStyle);
                AddSettingsHitTarget(actionRow, view, new SettingsHitTarget(action));
                y += 34f;
            }
        }

        private void AddSettingsHitTarget(
            Rect contentRect, Rect view, SettingsHitTarget target)
        {
            Rect screenRect = new Rect(
                view.x + contentRect.x - _actionScroll.x,
                view.y + contentRect.y - _actionScroll.y,
                contentRect.width,
                contentRect.height);
            float left = Math.Max(screenRect.xMin, view.xMin);
            float top = Math.Max(screenRect.yMin, view.yMin);
            float right = Math.Min(screenRect.xMax, view.xMax);
            float bottom = Math.Min(screenRect.yMax, view.yMax);
            if (right <= left || bottom <= top) return;
            target.ScreenRect = Rect.MinMaxRect(left, top, right, bottom);
            _settingsHitTargets.Add(target);
        }

        private static Rect SettingsCloseButtonRect(bool save)
        {
            return new Rect(Screen.width - 18f - (save ? 370f : 190f), 28f, 170f, 34f);
        }

        private void CloseKeyboardSettings(bool save)
        {
            if (!_settingsPageVisible) return;
            if (save) ApplyDraftBindings();
            else DiscardDraftBindings();
            _settingsPageVisible = false;
            _settingsCloseClickActive = true;
            _settingsHitTargets.Clear();
            RestoreNativeOptions();
        }

        private void HandleSettingsPhysicalClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            Vector2 point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (SettingsCloseButtonRect(true).Contains(point)) { CloseKeyboardSettings(true); return; }
            if (SettingsCloseButtonRect(false).Contains(point)) { CloseKeyboardSettings(false); return; }
            for (int index = _settingsHitTargets.Count - 1; index >= 0; index--)
            {
                SettingsHitTarget target = _settingsHitTargets[index];
                if (!target.ScreenRect.Contains(point)) continue;
                if (target.Action.HasValue)
                {
                    BeginOneShotCapture(target.Action.Value);
                }
                else if (target.Category == 0)
                {
                    _groupCategoryExpanded = !_groupCategoryExpanded;
                }
                else if (target.Category == 1)
                {
                    _productionCategoryExpanded = !_productionCategoryExpanded;
                }
                else if (target.Category == 2)
                {
                    _buildingCategoryExpanded = !_buildingCategoryExpanded;
                }
                else if (target.Category == 3)
                {
                    _otherCategoryExpanded = !_otherCategoryExpanded;
                }
                return;
            }
        }

        private void TogglePendingModifier(ModifierKind modifier)
        {
            if (modifier == ModifierKind.Ctrl) _pendingCtrl = !_pendingCtrl;
            if (modifier == ModifierKind.Shift) _pendingShift = !_pendingShift;
            if (modifier == ModifierKind.Alt) _pendingAlt = !_pendingAlt;
        }

        private static string L(string key) { return Text.Get(key); }

        private static string ActionLabel(KeyboardAction action)
        {
            int slot;
            if (TryActionSlot(action, KeyboardAction.ReplaceGroup1, 10, out slot))
                return L("Replace_Group") + GroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.SelectGroup1, 10, out slot))
                return L("Select_Group") + GroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.AddGroup1, 10, out slot))
                return L("Add_to_Group") + GroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.ExclusiveGroup1, 10, out slot))
                return L("Exclusive_Group") + GroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.SaveCamera1, 8, out slot))
                return L("Save_Camera_F") + (slot + 1);
            if (TryActionSlot(action, KeyboardAction.RecallCamera1, 8, out slot))
                return L("Recall_Camera_F") + (slot + 1);
            if (TryActionSlot(action, KeyboardAction.ProduceSlot1, 8, out slot))
                return slot == 0
                    ? L("Produce_Slot_1_leftmost")
                    : L("Produce_Slot") + (slot + 1);
            if (TryActionSlot(action, KeyboardAction.BuildPageCastle, 6, out slot))
            {
                string[] labels = new[] { L("Castle"), L("Industry"), L("Farms"), L("Town"), L("Weapons"), L("Food_Processing") };
                return L("Open") + labels[slot] + L("Build_Page");
            }
            if (TryActionSlot(action, KeyboardAction.BuildSlot1, 10, out slot))
                return L("Current_Build_Page_Slot") + (slot + 1);

            switch (action)
            {
                case KeyboardAction.SelectMercenaryPost: return L("Select_Mercenary_Post");
                case KeyboardAction.SelectBedouinStockade: return L("Select_Bedouin_Stockade");
                case KeyboardAction.SelectBarracks: return L("Select_Barracks");
                case KeyboardAction.SelectEngineersGuild: return L("Select_Engineers_Guild");
                case KeyboardAction.SelectTunnelersGuild: return L("Select_Tunnelers_Guild");
                case KeyboardAction.SelectCathedral: return L("Select_Cathedral");
                case KeyboardAction.PauseGame: return L("Pause_Resume_Game");
                case KeyboardAction.StopUnits: return L("Stop_Units");
                case KeyboardAction.PatrolUnits: return L("Patrol_Units");
                case KeyboardAction.CenterOnKeep: return L("Center_Camera_on_Keep");
                case KeyboardAction.FlattenLandscape: return L("Flatten_Restore_Landscape");
                case KeyboardAction.OpenChat: return L("Open_Chat");
                case KeyboardAction.MultiplayerPing: return L("Multiplayer_Position_Ping");
                case KeyboardAction.CameraLeft: return L("Move_Camera_Left");
                case KeyboardAction.CameraRight: return L("Move_Camera_Right");
                case KeyboardAction.CameraUp: return L("Move_Camera_Up");
                case KeyboardAction.CameraDown: return L("Move_Camera_Down");
                case KeyboardAction.SelectAllMilitary: return L("Select_Visible_and_Nearby_Military_Units_except_Lord");
                case KeyboardAction.SelectAllMilitaryMap: return L("Select_All_Map_Military_Units_except_Lord");
                case KeyboardAction.SelectLord: return L("Select_and_Control_Lord_double_press_to_center");
                case KeyboardAction.ToggleFrameRate: return L("Show_Hide_Frame_Rate");
                case KeyboardAction.ToggleGoods: return L("Show_Hide_All_Resource_Stocks");
                case KeyboardAction.IncreaseGameSpeed: return L("Increase_Game_Speed_main");
                case KeyboardAction.IncreaseGameSpeedKeypad: return L("Increase_Game_Speed_keypad");
                case KeyboardAction.DecreaseGameSpeed: return L("Decrease_Game_Speed_main");
                case KeyboardAction.DecreaseGameSpeedKeypad: return L("Decrease_Game_Speed_keypad");
                case KeyboardAction.AttackMove: return L("Attack_Move_optional_movement_mod");
                default: return action.ToString();
            }
        }

        private string FindDraftActionsLabelForChord(KeyChord chord)
        {
            string label = null;
            foreach (KeyValuePair<KeyboardAction, KeyChord> pair in _draftBindings.Bindings)
            {
                if (!pair.Value.Equals(chord)) continue;
                string next = ShortActionLabel(pair.Key);
                if (string.IsNullOrEmpty(next)) continue;
                label = label == null ? next : label + " / " + next;
            }
            return label;
        }

        private static string ShortActionLabel(KeyboardAction action)
        {
            int slot;
            if (TryActionSlot(action, KeyboardAction.ReplaceGroup1, 10, out slot))
                return L("Group") + ShortGroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.SelectGroup1, 10, out slot))
                return L("Select") + ShortGroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.AddGroup1, 10, out slot))
                return L("Add") + ShortGroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.ExclusiveGroup1, 10, out slot))
                return L("Exclusive") + ShortGroupLabel(slot);
            if (TryActionSlot(action, KeyboardAction.SaveCamera1, 8, out slot))
                return L("SaveF") + (slot + 1);
            if (TryActionSlot(action, KeyboardAction.RecallCamera1, 8, out slot))
                return L("RecallF") + (slot + 1);
            if (TryActionSlot(action, KeyboardAction.ProduceSlot1, 8, out slot))
                return L("Produce") + (slot + 1);
            if (TryActionSlot(action, KeyboardAction.BuildPageCastle, 6, out slot))
                return L("Page") + (slot + 1);
            if (TryActionSlot(action, KeyboardAction.BuildSlot1, 10, out slot))
                return L("Build") + (slot + 1);
            switch (action)
            {
                case KeyboardAction.SelectMercenaryPost: return L("Merc_Post");
                case KeyboardAction.SelectBedouinStockade: return L("Bedouin");
                case KeyboardAction.SelectBarracks: return L("Barracks");
                case KeyboardAction.SelectEngineersGuild: return L("Engineers");
                case KeyboardAction.SelectTunnelersGuild: return L("Tunnelers");
                case KeyboardAction.SelectCathedral: return L("Cathedral");
                case KeyboardAction.PauseGame: return L("Pause");
                case KeyboardAction.StopUnits: return L("Stop");
                case KeyboardAction.PatrolUnits: return L("Patrol");
                case KeyboardAction.CenterOnKeep: return L("Keep");
                case KeyboardAction.FlattenLandscape: return L("Flatten");
                case KeyboardAction.OpenChat: return L("Chat");
                case KeyboardAction.MultiplayerPing: return L("Ping");
                case KeyboardAction.CameraLeft: return L("Cam_Left");
                case KeyboardAction.CameraRight: return L("Cam_Right");
                case KeyboardAction.CameraUp: return L("Cam_Up");
                case KeyboardAction.CameraDown: return L("Cam_Down");
                case KeyboardAction.SelectAllMilitary: return L("Nearby_Military");
                case KeyboardAction.SelectAllMilitaryMap: return L("Map_Military");
                case KeyboardAction.SelectLord: return L("Select_Lord");
                case KeyboardAction.ToggleFrameRate: return L("FPS");
                case KeyboardAction.ToggleGoods: return L("Resource_Stocks");
                case KeyboardAction.AttackMove: return L("Attack_Move");
                case KeyboardAction.IncreaseGameSpeed:
                case KeyboardAction.IncreaseGameSpeedKeypad: return L("Faster");
                case KeyboardAction.DecreaseGameSpeed:
                case KeyboardAction.DecreaseGameSpeedKeypad: return L("Slower");
                default: return "";
            }
        }

        private static string GroupLabel(int slot)
        {
            return slot == 9 ? L("Text_0_Group_10") : (slot + 1).ToString();
        }

        private static string ShortGroupLabel(int slot)
        {
            return slot == 9 ? "0" : (slot + 1).ToString();
        }

        private static string FormatChord(KeyChord chord)
        {
            string label;
            if (!KeyLabels.TryGetValue(chord.KeyCode, out label))
                label = ((KeyCode)chord.KeyCode).ToString();
            if (!chord.Ctrl && !chord.Shift && !chord.Alt) return label;

            string modifiers = chord.Ctrl ? L("Key_Ctrl") : "";
            if (chord.Shift) modifiers += (modifiers.Length == 0 ? "" : " + ") + L("Key_Shift");
            if (chord.Alt) modifiers += (modifiers.Length == 0 ? "" : " + ") + L("Key_Alt");
            return modifiers + " + " + label;
        }

        private static KeyVisual[][] CreateKeyboardRows()
        {
            return new[]
            {
                Row(
                    K(L("Key_Esc"), KeyCode.Escape), S(.45f), K("F1", KeyCode.F1), K("F2", KeyCode.F2),
                    K("F3", KeyCode.F3), K("F4", KeyCode.F4), S(.35f), K("F5", KeyCode.F5),
                    K("F6", KeyCode.F6), K("F7", KeyCode.F7), K("F8", KeyCode.F8), S(.35f),
                    K("F9", KeyCode.F9), K("F10", KeyCode.F10), K("F11", KeyCode.F11),
                    K("F12", KeyCode.F12), S(.45f), K(L("Key_PrtSc"), KeyCode.Print),
                    K(L("Key_ScrLk"), KeyCode.ScrollLock), K(L("Key_Pause"), KeyCode.Pause)),
                Row(
                    K("` ~", KeyCode.BackQuote), K("1", KeyCode.Alpha1), K("2", KeyCode.Alpha2),
                    K("3", KeyCode.Alpha3), K("4", KeyCode.Alpha4), K("5", KeyCode.Alpha5),
                    K("6", KeyCode.Alpha6), K("7", KeyCode.Alpha7), K("8", KeyCode.Alpha8),
                    K("9", KeyCode.Alpha9), K("0", KeyCode.Alpha0), K("- / _", KeyCode.Minus),
                    K("= / +", KeyCode.Equals), K(L("Key_Backspace"), KeyCode.Backspace, 2f), S(.35f),
                    K(L("Key_Ins"), KeyCode.Insert), K(L("Key_Home"), KeyCode.Home), K(L("Key_PgUp"), KeyCode.PageUp), S(.35f),
                    K(L("Key_Num"), KeyCode.Numlock), K("/", KeyCode.KeypadDivide),
                    K("*", KeyCode.KeypadMultiply), K("-", KeyCode.KeypadMinus)),
                Row(
                    K(L("Key_Tab"), KeyCode.Tab, 1.5f), K("Q", KeyCode.Q), K("W", KeyCode.W),
                    K("E", KeyCode.E), K("R", KeyCode.R), K("T", KeyCode.T), K("Y", KeyCode.Y),
                    K("U", KeyCode.U), K("I", KeyCode.I), K("O", KeyCode.O), K("P", KeyCode.P),
                    K("[", KeyCode.LeftBracket), K("]", KeyCode.RightBracket),
                    K("\\", KeyCode.Backslash, 1.5f), S(.35f), K(L("Key_Del"), KeyCode.Delete),
                    K(L("Key_End"), KeyCode.End), K(L("Key_PgDn"), KeyCode.PageDown), S(.35f),
                    K("7", KeyCode.Keypad7), K("8", KeyCode.Keypad8),
                    K("9", KeyCode.Keypad9), K("+", KeyCode.KeypadPlus)),
                Row(
                    M(L("Key_Caps"), KeyCode.CapsLock, ModifierKind.None, 1.75f), K("A", KeyCode.A),
                    K("S", KeyCode.S), K("D", KeyCode.D), K("F", KeyCode.F), K("G", KeyCode.G),
                    K("H", KeyCode.H), K("J", KeyCode.J), K("K", KeyCode.K), K("L", KeyCode.L),
                    K(";", KeyCode.Semicolon), K("'", KeyCode.Quote), K(L("Key_Enter"), KeyCode.Return, 2.25f),
                    S(3.7f), K("4", KeyCode.Keypad4), K("5", KeyCode.Keypad5),
                    K("6", KeyCode.Keypad6), K("+", KeyCode.KeypadPlus)),
                Row(
                    M(L("Key_Shift"), KeyCode.LeftShift, ModifierKind.Shift, 2.25f), K("Z", KeyCode.Z),
                    K("X", KeyCode.X), K("C", KeyCode.C), K("V", KeyCode.V), K("B", KeyCode.B),
                    K("N", KeyCode.N), K("M", KeyCode.M), K(",", KeyCode.Comma),
                    K(".", KeyCode.Period), K("/", KeyCode.Slash),
                    M(L("Key_Shift"), KeyCode.RightShift, ModifierKind.Shift, 2.75f), S(1.35f),
                    K("↑", KeyCode.UpArrow), S(1.35f), K("1", KeyCode.Keypad1),
                    K("2", KeyCode.Keypad2), K("3", KeyCode.Keypad3), K(L("Key_Enter"), KeyCode.KeypadEnter)),
                Row(
                    M(L("Key_Ctrl"), KeyCode.LeftControl, ModifierKind.Ctrl, 1.5f),
                    K(L("Key_Win"), KeyCode.LeftWindows, 1.25f),
                    M(L("Key_Alt"), KeyCode.LeftAlt, ModifierKind.Alt, 1.25f), K(L("Key_Space"), KeyCode.Space, 6f),
                    M(L("Key_Alt"), KeyCode.RightAlt, ModifierKind.Alt, 1.25f),
                    K(L("Key_Win"), KeyCode.RightWindows, 1.25f), K(L("Key_Menu"), KeyCode.Menu, 1.25f),
                    M(L("Key_Ctrl"), KeyCode.RightControl, ModifierKind.Ctrl, 1.5f), S(.35f),
                    K("←", KeyCode.LeftArrow), K("↓", KeyCode.DownArrow), K("→", KeyCode.RightArrow),
                    S(.35f), K("0", KeyCode.Keypad0, 2f), K(".", KeyCode.KeypadPeriod),
                    K(L("Key_Enter"), KeyCode.KeypadEnter)),
                Row(K(L("Mouse_4"), KeyCode.Mouse3, 2f),
                    K(L("Mouse_5"), KeyCode.Mouse4, 2f))
            };
        }

        private static KeyVisual[] Row(params KeyVisual[] keys) { return keys; }
        private static KeyVisual K(string label, KeyCode key, float width = 1f)
        {
            return new KeyVisual(label, (int)key, width, ModifierKind.None);
        }
        private static KeyVisual M(string label, KeyCode key, ModifierKind modifier, float width = 1f)
        {
            return new KeyVisual(label, (int)key, width, modifier);
        }
        private static KeyVisual S(float width) { return new KeyVisual("", 0, width, ModifierKind.None); }

        private void OnDestroy()
        {
            if (_runtime != null)
            {
                Logger.LogInfo(
                    "BepInEx plugin host was destroyed; the detached keyboard runtime remains active across game scenes.");
            }
        }

        internal void RuntimeDestroyed(KeyboardControlRuntime runtime)
        {
            if (!ReferenceEquals(_runtime, runtime)) return;
            _runtime.Owner = null;
            _runtime = null;
            Application.wantsToQuit -= OnApplicationWantsToQuit;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            UpdateOptionalMovementHotkeyControl(false);
            RestoreEnemyExclusionRadiusPatch();
            RestoreNativeOptions();
            CancelNativeSelectionTransaction();
            CancelMinimapMoves();
            RestoreOriginalKeyMap(KeyManager.instance);
            new Harmony(PluginGuid).UnpatchSelf();

            DestroyTexture(ref _keyNormalTexture);
            DestroyTexture(ref _keyMappedTexture);
            DestroyTexture(ref _keyPressedTexture);
            DestroyTexture(ref _keyModifierTexture);
            DestroyTexture(ref _productionHintTexture);
            _productionHintStyle = null;
            _militarySelectionHintStyle = null;
            _activeOptionsRoot = null;
            _optionsMenuPanel = null;
            if (ReferenceEquals(_instance, this)) _instance = null;
        }

        private string ExitAuditPath
        {
            get { return Path.Combine(Paths.BepInExRootPath, "SC2KeyboardControl-last-session.log"); }
        }

        private void BeginExitAudit()
        {
            try
            {
                string launchId = Environment.GetEnvironmentVariable("SCDEModManagerLaunchId") ??
                                  "not-provided";
                File.WriteAllText(
                    ExitAuditPath,
                    string.Format(
                        "[{0:O}] START version={1} pid={2} managerLaunch={3}{4}",
                        DateTime.UtcNow, PluginVersion,
                        System.Diagnostics.Process.GetCurrentProcess().Id,
                        launchId, Environment.NewLine));
            }
            catch (Exception error)
            {
                Logger.LogWarning("Exit audit could not be initialized: " + error.Message);
            }
        }

        private void AppendExitAudit(string eventName, bool includeStack)
        {
            try
            {
                EngineInterface.PlayState state = GameData.Instance == null
                    ? null
                    : GameData.Instance.lastGameState;
                string action = _lastExecutedAction.HasValue
                    ? ActionLabel(_lastExecutedAction.Value)
                    : "none";
                string line = string.Format(
                    "[{0:O}] {1} frame={2} lastAction={3} lastActionFrame={4} appMode={5} appSubMode={6} fatExiting={7}{8}",
                    DateTime.UtcNow, eventName, Time.frameCount, action,
                    _lastExecutedActionFrame, state == null ? -1 : state.app_mode,
                    state == null ? -1 : state.app_sub_mode,
                    FatControler.instance != null && FatControllerExitingField != null &&
                    (bool)FatControllerExitingField.GetValue(FatControler.instance),
                    Environment.NewLine);
                if (includeStack) line += Environment.StackTrace + Environment.NewLine;
                File.AppendAllText(ExitAuditPath, line);
            }
            catch (Exception error)
            {
                Logger.LogWarning("Exit audit write failed: " + error.Message);
            }
        }

        private bool OnApplicationWantsToQuit()
        {
            AppendExitAudit("UNITY_WANTS_TO_QUIT", true);
            return true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                string action = _lastExecutedAction.HasValue
                    ? _lastExecutedAction.Value.ToString()
                    : "none";
                File.AppendAllText(
                    ExitAuditPath,
                    string.Format(
                        "[{0:O}] UNHANDLED_EXCEPTION lastAction={1} lastActionFrame={2} terminating={3}{4}{5}{4}{6}{4}",
                        DateTime.UtcNow, action, _lastExecutedActionFrame,
                        args.IsTerminating, Environment.NewLine,
                        args.ExceptionObject == null ? "unknown" : args.ExceptionObject.ToString(),
                        Environment.StackTrace));
            }
            catch
            {
            }
        }

        internal void RuntimeApplicationQuitting()
        {
            AppendExitAudit("RUNTIME_ON_APPLICATION_QUIT", true);
        }

        internal static void ObserveFatControllerExitApp()
        {
            SCDEKeyboardControlPlugin owner = _instance;
            if (!ReferenceEquals(owner, null))
                owner.AppendExitAudit("FAT_CONTROLLER_EXIT_APP", true);
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture != null) Destroy(texture);
            texture = null;
        }

        private sealed class MinimapMoveOrder
        {
            internal readonly int[] UnitIds;
            internal readonly int LogicX;
            internal readonly int LogicY;
            internal readonly int ScreenX;
            internal readonly int ScreenY;
            internal readonly bool OverTopHalf;
            internal readonly bool Classic;

            internal MinimapMoveOrder(
                int[] unitIds, int logicX, int logicY,
                int screenX, int screenY, bool overTopHalf, bool classic)
            {
                UnitIds = (int[])unitIds.Clone();
                LogicX = logicX;
                LogicY = logicY;
                ScreenX = screenX;
                ScreenY = screenY;
                OverTopHalf = overTopHalf;
                Classic = classic;
            }
        }

        private enum ModifierKind { None, Ctrl, Shift, Alt }

        private struct SettingsHitTarget
        {
            internal Rect ScreenRect;
            internal readonly KeyboardAction? Action;
            internal readonly int Category;

            internal SettingsHitTarget(KeyboardAction action)
            {
                ScreenRect = new Rect();
                Action = action;
                Category = -1;
            }

            internal SettingsHitTarget(int category)
            {
                ScreenRect = new Rect();
                Action = null;
                Category = category;
            }
        }

        private sealed class KeyVisual
        {
            internal readonly string Label;
            internal readonly int KeyCode;
            internal readonly float Width;
            internal readonly ModifierKind Modifier;

            internal KeyVisual(string label, int keyCode, float width, ModifierKind modifier)
            {
                Label = label;
                KeyCode = keyCode;
                Width = width;
                Modifier = modifier;
            }
        }
    }

    internal sealed class KeyboardControlRuntime : MonoBehaviour
    {
        internal SCDEKeyboardControlPlugin Owner;

        private void Update()
        {
            SCDEKeyboardControlPlugin owner = Owner;
            if (!ReferenceEquals(owner, null)) owner.RuntimeUpdate();
        }

        private void OnGUI()
        {
            SCDEKeyboardControlPlugin owner = Owner;
            if (!ReferenceEquals(owner, null)) owner.RuntimeOnGUI();
        }

        private void OnDestroy()
        {
            SCDEKeyboardControlPlugin owner = Owner;
            if (!ReferenceEquals(owner, null)) owner.RuntimeDestroyed(this);
            Owner = null;
        }

        private void OnApplicationQuit()
        {
            SCDEKeyboardControlPlugin owner = Owner;
            if (!ReferenceEquals(owner, null)) owner.RuntimeApplicationQuitting();
        }

    }

    [HarmonyPatch(typeof(FatControler), "ExitApp")]
    internal static class ExitAppObservationPatch
    {
        private static void Prefix()
        {
            SCDEKeyboardControlPlugin.ObserveFatControllerExitApp();
        }
    }

    [HarmonyPatch(typeof(ConfigSettings), "LoadSettings")]
    internal static class NativeControlDefaultsPatch
    {
        [HarmonyAfter("com.jiuyeayan.scde.multiplayer-compatibility")]
        private static void Postfix()
        {
            SCDEKeyboardControlPlugin.ApplyNativeControlDefaults();
        }
    }

    [HarmonyPatch(typeof(KeyManager), "Update")]
    internal static class OriginalShortcutReplacementPatch
    {
        private static void Prefix(KeyManager __instance)
        {
            SCDEKeyboardControlPlugin.ReplaceOriginalKeyMap(__instance);
        }

        private static void Postfix(KeyManager __instance)
        {
            SCDEKeyboardControlPlugin.ProcessCustomKeyActions(__instance);
        }
    }

    [HarmonyPatch(typeof(KeyManager), "Awake")]
    internal static class OriginalShortcutAwakePatch
    {
        private static void Postfix(KeyManager __instance)
        {
            SCDEKeyboardControlPlugin.ReplaceOriginalKeyMap(__instance);
        }
    }

    [HarmonyPatch]
    internal static class OriginalShortcutMutationPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(KeyManager), "SetDefaultFunctionsNew");
            yield return AccessTools.Method(typeof(KeyManager), "SetDefaultFunctionsSH1");
            yield return AccessTools.Method(
                typeof(KeyManager), "LoadFromString", new[] { typeof(string) });
            yield return AccessTools.Method(
                typeof(KeyManager), "SetNewKey",
                new[] { typeof(Enums.KeyFunctions), typeof(int), typeof(int) });
        }

        private static void Postfix(KeyManager __instance)
        {
            SCDEKeyboardControlPlugin.ObserveNativeKeyMapMutation(__instance);
        }
    }

    [HarmonyPatch(typeof(KeyManager), "LoadFromString")]
    internal static class ManagedShortcutLoadPatch
    {
        private static bool Prefix(KeyManager __instance)
        {
            return SCDEKeyboardControlPlugin.LoadNativeKeyMap(__instance);
        }
    }

    [HarmonyPatch]
    internal static class NewMapLoadObservationPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EngineInterface), "loadMap", new[]
            {
                typeof(int), typeof(string), typeof(bool), typeof(bool), typeof(int),
                typeof(int), typeof(bool)
            });
        }

        private static void Prefix(bool __3)
        {
            SCDEKeyboardControlPlugin.ObserveNewMapLoading(__3);
        }

    }

    [HarmonyPatch]
    internal static class SaveLoadObservationPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(EngineInterface), "LoadSaveFile", new[] { typeof(string) });
        }

        private static void Prefix()
        {
            SCDEKeyboardControlPlugin.ObserveSaveLoading();
        }
    }

    [HarmonyPatch]
    internal static class PreSimulationGiftedStockpilePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Director), "startSimThread", Type.EmptyTypes);
        }

        private static void Prefix(Director __instance)
        {
            SCDEKeyboardControlPlugin.BeforeSimulationStarts(__instance);
        }
    }

    [HarmonyPatch]
    internal static class SynchronizedNativeSelectionInputPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(EditorDirector), "preDLLCallActions",
                new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });
        }

        private static void Prefix(EditorDirector __instance)
        {
            SCDEKeyboardControlPlugin.PrepareSynchronizedNativeSelection(__instance);
        }

        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter("scde.sc2-hybrid-movement")]
        private static void Postfix(ref int __0, ref int __1)
        {
            SCDEKeyboardControlPlugin.CompleteSynchronizedNativeSelection(ref __0, ref __1);
            SCDEKeyboardControlPlugin.RemoveGiftedStockpilesAtNativeBoundary();
        }
    }

    [HarmonyPatch]
    internal static class NativeTroopSelectionSequenceGuardPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(EngineInterface), "TroopSelection",
                new[]
                {
                    typeof(int), typeof(bool), typeof(bool), typeof(int[]),
                    typeof(bool), typeof(bool), typeof(int[]), typeof(int),
                    typeof(int), typeof(bool), typeof(int[])
                });
        }

        private static bool Prefix()
        {
            return SCDEKeyboardControlPlugin.AllowNativeTroopSelectionCall();
        }
    }

    [HarmonyPatch]
    internal static class NativeActionObservationPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(KeyManager), "IsActionPressed", new[] { typeof(Enums.KeyFunctions) });
        }

        private static void Postfix(Enums.KeyFunctions __0, ref bool __result)
        {
            if (SCDEKeyboardControlPlugin.IsCustomTroopCommand(__0))
            {
                __result = false;
                return;
            }
            SCDEKeyboardControlPlugin.ObserveNativeAction(__0, __result);
        }
    }

    [HarmonyPatch(typeof(FatControler), "RadarScrollMap")]
    internal static class MinimapMouseRoutingPatch
    {
        private static void Prefix(FatControler __instance)
        {
            SCDEKeyboardControlPlugin.ObserveMinimapInput(__instance);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo held = AccessTools.Field(typeof(FatControler), "mouseIsDown");
            MethodInfo down = AccessTools.PropertyGetter(typeof(FatControler), "MouseIsDownStroke");
            foreach (CodeInstruction instruction in instructions)
            {
                // Only replace camera input reads in RadarScrollMap; preserve its drag,
                // bounds, briefing, media-overlay and native RadarClicked command logic.
                if (instruction.opcode == System.Reflection.Emit.OpCodes.Ldfld && Equals(instruction.operand, held))
                {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(SCDEKeyboardControlPlugin), "ReadMinimapCameraHeld");
                }
                else if (instruction.Calls(down))
                    instruction.operand = AccessTools.Method(typeof(SCDEKeyboardControlPlugin), "ReadMinimapCameraDown");
                yield return instruction;
            }
        }
    }

    [HarmonyPatch]
    internal static class OpeningLordPlacementPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(EngineInterface), "PlaceMapperItem",
                new[]
                {
                    typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(int), typeof(bool), typeof(bool), typeof(int)
                });
        }

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(
            int __0, int __1, int __2, int __4, bool __5, int __7)
        {
            return SCDEKeyboardControlPlugin.AllowLocalPlacement(
                __0, __1, __2, __4, __5, __7);
        }
    }

    [HarmonyPatch(typeof(CrusaderDE.HUD_Main), "SetEnginePanelText")]
    internal static class OpeningLordPlacementFeedbackPatch
    {
        private static void Prefix(
            ref int __0, ref int __1, ref bool __2)
        {
            SCDEKeyboardControlPlugin.OverrideOpeningPlacementFeedback(
                ref __0, ref __1, ref __2);
        }
    }

    [HarmonyPatch]
    internal static class BarracksRallyRightClickPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(EditorDirector), "getMouseStateForEngine",
                new[] { typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType() });
        }

        private static bool Prefix(
            EditorDirector __instance, ref bool __0, ref bool __1, ref int __result)
        {
            if (!SCDEKeyboardControlPlugin.TryHandleBarracksRallyClick(__instance)) return true;
            __0 = false;
            __1 = false;
            __result = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(EditorDirector), "Update")]
    internal static class BarracksRallyEarlyUpdatePatch
    {
        private static void Prefix(EditorDirector __instance)
        {
            SCDEKeyboardControlPlugin.TryHandleBarracksRallyClick(__instance);
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(
                typeof(Input), "GetMouseButtonDown", new[] { typeof(int) });
            MethodInfo replacement = AccessTools.Method(
                typeof(SCDEKeyboardControlPlugin), "ReadEditorMouseButtonDown");
            MethodInfo originalUp = AccessTools.Method(
                typeof(Input), "GetMouseButtonUp", new[] { typeof(int) });
            MethodInfo replacementUp = AccessTools.Method(
                typeof(SCDEKeyboardControlPlugin), "ReadEditorMouseButtonUp");
            MethodInfo originalHeld = AccessTools.Method(
                typeof(Input), "GetMouseButton", new[] { typeof(int) });
            MethodInfo replacementHeld = AccessTools.Method(
                typeof(SCDEKeyboardControlPlugin), "ReadEditorMouseButton");
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original)) instruction.operand = replacement;
                else if (instruction.Calls(originalUp)) instruction.operand = replacementUp;
                else if (instruction.Calls(originalHeld)) instruction.operand = replacementHeld;
                yield return instruction;
            }
        }
    }

    [HarmonyPatch]
    internal static class OptionsOpenObservationPatch
    {
        private static MethodBase TargetMethod()
        {
            Type optionsType = AccessTools.TypeByName("CrusaderDE.HUD_Options");
            return optionsType == null ? null : AccessTools.Method(optionsType, "Init");
        }

        private static void Postfix(object __instance)
        {
            SCDEKeyboardControlPlugin.ObserveOptionsOpened(__instance);
        }
    }

    [HarmonyPatch]
    internal static class OptionsCloseObservationPatch
    {
        private static MethodBase TargetMethod()
        {
            Type optionsType = AccessTools.TypeByName("CrusaderDE.HUD_Options");
            return optionsType == null
                ? null
                : AccessTools.Method(optionsType, "ButtonClicked", new[] { typeof(int) });
        }

        private static void Postfix(int __0)
        {
            SCDEKeyboardControlPlugin.ObserveOptionsButton(__0);
        }
    }

}
