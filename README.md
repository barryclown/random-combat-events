# Random Combat Events / 戰鬥隨機事件

An early-alpha mod for the Slay the Spire 2 Early Access `public-beta` branch. It adds telegraphed, counterable, and configurable incidents to combat.

## Features

- A deterministic route plans candidate checkpoints through turn 100. Saving and reloading does not reroll it.
- Standard mode checks every 3–5 turns; each checkpoint has a 50% chance to contain an incident.
- Advance warnings show the exact target, damage, and effect. They have no countdown bar and can be closed with a dedicated × button.
- Direct damage resolves at turn end and can be Blocked.
- Seven incidents are included: Rockfall, Sword Rain, Toxic Fog, Vine Snare, Damp Sea Wind, Hive Onslaught, and Gentle Rain.
- The in-game settings page exposes presets, schedule controls, combat types, incident toggles, weights, and effect values.
- Full in-game localization is included for English, Simplified Chinese, and Traditional Chinese. Other locales fall back to English.

The current alpha is designed and tested for single-player. Multiplayer is not supported yet.

## Requirements

- [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127) 3.4.4 or newer
- [RitsuLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295) 0.5.11 or newer
- Slay the Spire 2 Early Access `public-beta` 0.110.x (tested on 0.110.1)

## Build

Install the .NET 9 SDK and keep a legal local installation of Slay the Spire 2. The project discovers the default Steam installation automatically; a local, ignored `Directory.Build.props` can override the game or MegaDot paths when needed.

```powershell
dotnet build -c Release
```

The Release build copies the DLL and manifest into the game's local `mods/BattlefieldIncidents` folder for testing.

## Ideas and contributions

If you have ideas for the mod or are interested in contributing, feel free to contact Barry through [Steam](https://steamcommunity.com/profiles/76561198420391746) or open a [GitHub issue](https://github.com/barryclown/random-combat-events/issues).

## Credits

- Concept, design, and hands-on testing: Barry
- Development assistance: OpenAI Codex
- Built with C#, .NET 9, MegaDot/Godot, Harmony, BaseLib, and RitsuLib

Source code in this repository is released under the [MIT License](LICENSE). Slay the Spire 2 and its assets belong to Mega Crit. This is an unofficial fan mod and is not affiliated with or endorsed by Mega Crit.
