param(
  [string]$GameDir = 'D:\0_zhuangji\Softwares\Steam\steamapps\common\Stronghold Crusader Definitive Edition - SCDE Modded'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$bepInExRoot = Join-Path $projectRoot 'third_party\BepInEx_win_x64_5.4.23.5'
$managedRoot = Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Managed'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$buildRoot = Join-Path $PSScriptRoot 'build'
$payloadRoot = Join-Path $buildRoot 'payload'
$pluginRoot = Join-Path $payloadRoot 'BepInEx\plugins\SC2KeyboardControl'
$testRoot = Join-Path $buildRoot 'test'
$releaseRoot = Join-Path $projectRoot 'release'
$packagePath = Join-Path $releaseRoot "JiuyeAyan's Advanced Control-0.2.22.scdemod"
$rulesPath = Join-Path $PSScriptRoot 'config\sc2-keyboard-control.toml'

foreach ($required in @(
  $compiler,
  (Join-Path $bepInExRoot 'BepInEx\core\BepInEx.dll'),
  (Join-Path $managedRoot 'Assembly-CSharp.dll')
)) {
  if (-not (Test-Path -LiteralPath $required)) {
    throw "Missing build dependency: $required"
  }
}

if (Test-Path -LiteralPath $buildRoot) {
  $resolvedBuild = Resolve-Path -LiteralPath $buildRoot
  if (-not $resolvedBuild.Path.StartsWith($PSScriptRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove unexpected build path: $resolvedBuild"
  }
  Remove-Item -LiteralPath $resolvedBuild.Path -Recurse -Force
}

New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination $buildRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.txt') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $pluginRoot

$modelSource = Join-Path $PSScriptRoot 'src\KeyboardBindingModel.cs'
$pluginSource = Join-Path $PSScriptRoot 'src\SCDEKeyboardControlPlugin.cs'
$testSource = Join-Path $PSScriptRoot 'test\KeyboardBindingModelTests.cs'
$languageSource = Join-Path $PSScriptRoot 'src\LanguageCatalog.cs'
$languageDefaults = Join-Path $buildRoot 'LanguageDefaults.cs'
$languageJson = Join-Path $PSScriptRoot 'src\LanguageJson.cs'
$languagePayload = Join-Path $payloadRoot 'BepInEx\config\sc2-keyboard-control\lang'
New-Item -ItemType Directory -Path $languagePayload -Force | Out-Null
$languageResources = @()
foreach ($language in @('en', 'zh-CN')) {
  $source = Join-Path $PSScriptRoot "locales\$language.json"
  Copy-Item -LiteralPath $source -Destination $languagePayload
  $languageResources += "/resource:$source,SCDEKeyboardControl.Locales.$language.json"
}
& node (Join-Path $PSScriptRoot 'generate-language-defaults.cjs') $languageDefaults
if ($LASTEXITCODE -ne 0) { throw 'Language defaults generation failed' }

& $compiler /nologo /target:exe "/out:$testRoot\LanguageCatalogTests.exe" $languageSource $languageJson $languageDefaults (Join-Path $PSScriptRoot 'test\LanguageCatalogTests.cs') @languageResources
if ($LASTEXITCODE -ne 0) { throw 'Language test compilation failed' }
& (Join-Path $testRoot 'LanguageCatalogTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Language tests failed' }

& $compiler /nologo /target:exe /optimize+ "/out:$testRoot\KeyboardBindingModelTests.exe" $modelSource $testSource
if ($LASTEXITCODE -ne 0) { throw "Binding model test compilation failed with exit code $LASTEXITCODE" }
& (Join-Path $testRoot 'KeyboardBindingModelTests.exe') $rulesPath
if ($LASTEXITCODE -ne 0) { throw "Binding model tests failed with exit code $LASTEXITCODE" }

$references = @(
  (Join-Path $bepInExRoot 'BepInEx\core\BepInEx.dll'),
  (Join-Path $bepInExRoot 'BepInEx\core\0Harmony.dll'),
  (Join-Path $managedRoot 'Assembly-CSharp.dll'),
  (Join-Path $managedRoot 'UnityEngine.dll'),
  (Join-Path $managedRoot 'UnityEngine.CoreModule.dll'),
  (Join-Path $managedRoot 'UnityEngine.InputLegacyModule.dll'),
  (Join-Path $managedRoot 'UnityEngine.IMGUIModule.dll'),
  (Join-Path $managedRoot 'UnityEngine.TextRenderingModule.dll'),
  (Join-Path $managedRoot 'UnityEngine.GridModule.dll'),
  (Join-Path $managedRoot 'UnityEngine.TilemapModule.dll'),
  (Join-Path $managedRoot 'Noesis.NoesisGUI.dll')
)
$compilerArgs = @('/nologo', '/target:library', '/optimize+', "/out:$pluginRoot\SCDEKeyboardControl.dll")
$compilerArgs += $references | ForEach-Object { "/reference:$_" }
$compilerArgs += @($modelSource, $pluginSource, $languageSource, $languageJson, $languageDefaults)
$compilerArgs += $languageResources
$compilerArgs += "/resource:$rulesPath,SCDEKeyboardControl.PackedSettings.toml"

& $compiler $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Plugin compilation failed with exit code $LASTEXITCODE" }

& $compiler /nologo /target:exe "/out:$testRoot\PluginBootstrapTests.exe" (Join-Path $PSScriptRoot 'test\PluginBootstrapTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Plugin bootstrap test compilation failed' }
& (Join-Path $testRoot 'PluginBootstrapTests.exe') (Join-Path $pluginRoot 'SCDEKeyboardControl.dll') $managedRoot (Join-Path $bepInExRoot 'BepInEx\core')
if ($LASTEXITCODE -ne 0) { throw 'Plugin managed bootstrap verification failed' }

$cecilPath = Join-Path $projectRoot 'tools\Mono.Cecil.dll'
Copy-Item -LiteralPath $cecilPath -Destination $testRoot
& $compiler /nologo /target:exe "/reference:$cecilPath" "/out:$testRoot\PackedRulesTests.exe" (Join-Path $PSScriptRoot 'test\PackedRulesTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Packed rules test compilation failed' }
& (Join-Path $testRoot 'PackedRulesTests.exe') (Join-Path $pluginRoot 'SCDEKeyboardControl.dll') $rulesPath (Join-Path $managedRoot 'Assembly-CSharp.dll')
if ($LASTEXITCODE -ne 0) { throw 'Packed rules verification failed' }

& node (Join-Path $PSScriptRoot 'test\verify-0.2.21.cjs')
if ($LASTEXITCODE -ne 0) { throw 'Language, lord-selection or settings source contract failed' }

& node (Join-Path $PSScriptRoot 'package.js') $buildRoot $packagePath
if ($LASTEXITCODE -ne 0) { throw "Mod packaging failed with exit code $LASTEXITCODE" }

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Package: $packagePath"
Write-Output "SHA256: $hash"
