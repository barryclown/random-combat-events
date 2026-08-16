# Random Combat Events / 戰鬥隨機事件

An early-alpha mod for the Slay the Spire 2 Early Access `public-beta` branch. It adds telegraphed, counterable, and configurable incidents to combat.

## Features

- A deterministic route plans candidate checkpoints through turn 100. Saving and reloading does not reroll it.
- Standard mode checks every 3–5 turns; each checkpoint has a 50% chance to contain an incident.
- Advance warnings show the exact target, damage, and effect. They have no countdown bar and can be closed with a dedicated × button.
- Direct damage resolves at turn end and can be Blocked.
- Twenty-five kinds of incident, with over sixty distinct outcomes:
  - **Hazard** — Rockfall, Sword Rain, Hive Onslaught, Laser.
  - **Debuff** — Toxic Fog, Vine Snare, Damp Sea Wind, the Architect's Curse.
  - **Aid** — Gentle Rain, Neow's Blessing, the Last Miracle.
  - **Encounter** — a wandering monster picks a side, a mercenary sells its help, an enemy recruit can be bought off or bought over, and a challenger pays out if you take the fight. Extra monsters raise the spoils.
  - **The Ancients** — one event for each of the seven shrines that are not Neow: Vakuu plays your next hand and burns it, Darv fills your hand and scrambles the prices after it, Nonupeipe leaves max HP or gold or a marked room, Tanx lends a weapon for the turn, Tezcatara lends a relic cast in wax, Pael pays energy and a card, and Orobas offers cards from a discipline that is not yours.
  - **Co-op** — Strangling Vines pins one player until an ally cuts them loose.
- The blessing and the curse are also rolled once on turn 1, at 33% each. That roll cannot be telegraphed, so it is labelled a Combat Start event and can be switched off on its own.
- The Last Miracle is a standing rule rather than a stop on the route. Each unit rolls for it when combat begins, and a unit holding one survives its first lethal blow at 1 HP. Both sides can hold one, and both are announced on screen for as long as the charge lasts, so a killing blow that fails to land is never a surprise. Later hits of the same attack still go through.
- Nothing is scheduled early enough to land before its own warning could be shown, so turn 1 never carries a route incident.
- A "Combat Events" entry on the main menu opens the settings page directly, alongside presets, schedule controls, combat types, incident toggles, weights, and effect values.
- Full in-game localization is included for English, Simplified Chinese, and Traditional Chinese. Other locales fall back to English.

Co-op support arrived in `0.4.0-alpha.1`: the whole table shares one route, shared decisions are settled by majority vote with the tie broken by the combat seed, priced offers are split rather than billed to whoever clicked, and effects land on every seat rather than on whichever player the game handed the hook to first. **It has not yet been tested against a real lobby — treat it as experimental and please report what breaks.**

## Requirements

- [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127) 3.4.4 or newer
- [RitsuLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295) 0.5.12 or newer (older builds cannot start a run on 0.111.0)
- Slay the Spire 2 Early Access `public-beta` 0.111.0 (the branch this build targets)

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
