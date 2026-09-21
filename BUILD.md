# 构建

保留 `mods/sc2-keyboard-control` 原工程目录结构。Windows 上需 Node.js、.NET Framework 4.x C# 编译器，以及自行取得的依赖（不随源码分发）：

- BepInEx 5.4.23.5 解压至仓库的 `third_party/BepInEx_win_x64_5.4.23.5/`，其下含 `BepInEx/core/`。
- Mono.Cecil 放在 `tools/Mono.Cecil.dll`，供内嵌规则验证测试使用。
- 正版游戏的 `Stronghold Crusader Definitive Edition_Data/Managed/` 程序集；兼容版本见 `manifest.json`。

在仓库根目录执行（将路径替换为自己的游戏路径）：

```powershell
npm install
powershell -NoProfile -ExecutionPolicy Bypass -File .\mods\sc2-keyboard-control\build.ps1 -GameDir "D:\YourGamePath"
```

构建会运行语言、键位、托管启动、内嵌规则及源码检查，生成 `release/JiuyeAyan's Advanced Control-0.2.22.scdemod`。通过 SCDE Mod Manager 导入；不向游戏原目录复制 DLL。

规则源文件为 `mods/sc2-keyboard-control/config/sc2-keyboard-control.toml`。修改后须重新构建。自动检查不等同于单人或多人实机验收。依赖完整管理器工程的集成测试未包含在本独立源码仓库中。
