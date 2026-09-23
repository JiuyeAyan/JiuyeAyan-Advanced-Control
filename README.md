# JiuyeAyan's Advanced Control

A control enhancement mod for Stronghold: Crusader Definitive Edition, source version **0.2.24**. Please load it with**SCDE Mod Manager** ; it depends on `bepinex-runtime 5.4.23.5`。

**Script Extender：** 
Not required as a dependency. Both modify the input flow, so SE script key interception may fail to intercept keys that this mod reads directly; the script's unselectable-unit rules may also affect select-all/formations. Coexistence is verified in-game, but future compatibility is not guaranteed.


- Custom key bindings, a full keyboard settings interface, and key hints for buildings/unit production/commands, displayed in both Chinese and English.
- Formations unit groups and screen groups; stop and patrol; building pagination and construction hotkeys.
- Double-press a group key to center on a random living, selected member; the whole group stays selected, even when split across the map.
- Native mouse edge scrolling is enabled once on first use; later player changes are preserved.
- Quick selection of six types of barracks, quick placement when not yet built; quick unit production and a unified rally point.
- `Space` selects nearby troops; `Ctrl+A` selects all friendly troops on the map (excluding the Lord).
- L selects and controls the Lord, double-click to locate; minimap commands adapted for both classic and modern controls.
- Remove the single initial storage area so the first one can be placed freely; preserved when loading a save or when there are multiple initial ones.
- Within the first 5 simulated minutes of a game, buildings are restricted to within 85 tiles of the Lord's initial position; afterward, the original behavior is restored.
- Pause, speed, native frame rate/resource panel, flattened scene; optional compatibility with attack-move mods (unpublish).

Contains only source code, language, and build files; no game files or compiled artifacts. See [BUILD.md](BUILD.md) for building. 
