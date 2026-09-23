const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../src/SCDEKeyboardControlPlugin.cs'), 'utf8');
function section(start, end) {
  const a = source.indexOf(start), b = source.indexOf(end, a + start.length);
  assert.ok(a >= 0 && b > a);
  return source.slice(a, b);
}
const select = section('private void SelectCachedControlGroup', 'private void PruneControlGroupCache');
assert.doesNotMatch(select, /TryCenterCameraOnUnits\(units\)/,
  'Do not center on unconfirmed group candidates before native selection completes.');
assert.match(select, /ApplyPersistentSelection\(units\);\s*_centerCameraAfterSelection = centerCamera;/);
const verify = section('private void VerifyPendingSelection', 'private void CancelNativeSelectionTransaction');
assert.match(verify, /_centerCameraAfterSelection = false/);
assert.match(verify, /if \(centerCamera && validSubset\) TryCenterCameraOnUnits\(confirmedSet\)/);
for (const [a,b] of [
  ['private void BeginNativeSelectionSequence', 'private void PrepareNativeSelectionInput'],
  ['private void CancelNativeSelectionTransaction', 'private void ResetNativeSelectionVisualState']
]) assert.match(section(a,b), /_centerCameraAfterSelection = false/);
const center = section('private void TryCenterCameraOnUnits', 'private void SelectNearbyMilitary');
assert.match(center, /GroupCameraPlan.TryGetPosition/);
assert.match(center, /NativeUnitOwnerOffset/);
assert.match(center, /SetCameraFromGameState\(cameraState\)/);
assert.doesNotMatch(center, /RadarClicked/);
assert.match(center, /RandomUnitCameraTarget/);
assert.match(center, /target.Consider\(unitId, logicX, logicY, _cameraRandom\)/);
assert.doesNotMatch(center, /minX|maxX|minY|maxY|UnityEngine.Random|ApplyPersistentSelection|TroopSelection\(/);
assert.match(center, /Math.Round\(target.X\)/);
assert.match(center, /Math.Round\(target.Y\)/);
assert.match(source, /readonly System.Random _cameraRandom = new System.Random\(\)/);
const defaults = section('internal static void ApplyNativeControlDefaults', 'private void LoadBuildingPlacementSettings');
assert.match(defaults, /!owner._enabled.Value/);
assert.match(defaults, /owner._pushMapScrollingDefaultApplied.Value/);
assert.match(defaults, /ConfigSettings.Settings_PushMapScrolling = true/);
assert.match(defaults, /ConfigSettings.SaveSettings\(false\)/);
assert.match(defaults, /if \(ConfigSettings.SettingsFileExisted\) ConfigSettings.SaveSettings\(false\)/);
assert.match(defaults, /owner._pushMapScrollingDefaultApplied.Value = true/);
assert.match(source, /HarmonyPatch\(typeof\(ConfigSettings\), "LoadSettings"\)/);
assert.match(source, /Postfix\(\)\s*\{\s*SCDEKeyboardControlPlugin.ApplyNativeControlDefaults\(\)/);
console.log('GROUP_CAMERA_CONTRACT_OK random_member=true confirmed_selection=true selection_unchanged=true native_projection=true');
console.log('EDGE_SCROLL_DEFAULT_CONTRACT_OK apply_once=true after_native_load=true preserve_player_choice=true');
