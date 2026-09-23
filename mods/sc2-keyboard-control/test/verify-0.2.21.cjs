const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const source = fs.readFileSync(path.join(root, 'src/SCDEKeyboardControlPlugin.cs'), 'utf8').replace(/\r\n/g, '\n');
function section(start, end) {
  const a = source.indexOf(start), b = source.indexOf(end, a + start.length);
  assert.ok(a >= 0 && b > a);
  return source.slice(a,b);
}
// Byte-for-byte source guards for previously working selection behavior (0.2.20 baseline).
for (const [a,b,expected] of [
  ['        private void SelectNearbyMilitary()', '        private bool TryReadNativeLiveUnitIds', 'a33e51999f30f662fd79df9ad6e685cf35a7746b10615877a12152b4af9e0972'],
  // Camera-only completion hooks changed in 0.2.23; native input phases remain byte-for-byte unchanged.
  ['        private void PrepareNativeSelectionInput', '        private void VerifyPendingSelection', '1f4665abdce87af9a30a1ade77f97e49f1dcf72650dbc87d5269951a1f5d569c']
]) assert.equal(crypto.createHash('sha256').update(section(a,b)).digest('hex'), expected);
const lord = section('        private void SelectLord()', '        private void ApplyPersistentSelection');
assert.doesNotMatch(lord, /ChimpsField|units.Values|Enums.KeyFunctions.Lord/);
assert.match(lord, /lock \(engineLock\)/);
assert.match(lord, /NativeLiveUnitState/);
assert.match(lord, /NativeUnitTypeOffset\) == LordChimpType/);
assert.match(lord, /NativeUnitOwnerOffset\) == player/);
assert.match(lord, /TryCenterCameraOnUnits\(lordCandidates\)/);
assert.match(lord, /if \(selectionBusy && !centerCamera\)/);
const close = section('        private void CloseKeyboardSettings', '        private void TogglePendingModifier');
assert.match(close, /if \(save\) ApplyDraftBindings\(\);\s*else DiscardDraftBindings\(\);/);
assert.match(close, /_settingsPageVisible = false/);
assert.match(close, /_settingsCloseClickActive = true/);
assert.match(close, /RestoreNativeOptions\(\)/);
assert.match(close, /SettingsCloseButtonRect\(true\).Contains\(point\)/);
assert.match(close, /SettingsCloseButtonRect\(false\).Contains\(point\)/);
const discard = section('        private void DiscardDraftBindings()', '        private void EnsureSessionInitialized');
assert.match(discard, /_draftBindings.CopyFrom\(_bindings\)/);
assert.doesNotMatch(discard, /\.Save\(/);
assert.match(source, /if \(_settingsCloseClickActive \|\| !Input.GetMouseButtonDown\(0\)\) return/);
assert.match(source, /SetNativeBinding\(functionMap, KeyboardAction.ToggleGoods, Enums.KeyFunctions.ToggleGoods\)/);
assert.match(section('        private static bool IsNativeKeyManagerAction', '        private KeyboardAction? FindPressedCustomAction'), /KeyboardAction.ToggleGoods/);
assert.doesNotMatch(source, /\bT\(|IsChineseLanguage|Application.systemLanguage/);
const en = JSON.parse(fs.readFileSync(path.join(root, 'locales/en.json'), 'utf8')).strings.ui;
const zh = JSON.parse(fs.readFileSync(path.join(root, 'locales/zh-CN.json'), 'utf8')).strings.ui;
assert.deepEqual(Object.keys(en), Object.keys(zh));
const used = new Set([...source.matchAll(/\bL\("([A-Za-z0-9_]+)"\)/g)].map(m=>m[1]));
for (const key of used) assert.equal(typeof en[key], 'string', 'Missing translation '+key);
for (const key of Object.keys(en)) assert.ok(used.has(key), 'Unused language key '+key);
console.log(JSON.stringify({result:'ADVANCED_021_SOURCE_CONTRACT_OK', languageKeys:used.size, militarySelectionUnchanged:true, nativeGoodsToggle:true, lordUsesNativeTable:true}));
