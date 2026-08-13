using BattlefieldIncidents.Localization;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace BattlefieldIncidents.Settings;

public static class SettingsBootstrap
{
    public static void Register()
    {
        using (RitsuLibFramework.BeginModDataRegistration(MainFile.ModId))
        {
            RitsuLibFramework.GetDataStore(MainFile.ModId).Register(
                key: MainFile.SettingsKey,
                fileName: "settings.json",
                scope: SaveScope.Global,
                defaultFactory: () => new IncidentSettings(),
                autoCreateIfMissing: true);
        }

        RegisterMainPage();
        RegisterIncidentsPage();
    }

    public static IncidentSettings Read()
    {
        var settings = RitsuLibFramework.GetDataStore(MainFile.ModId)
            .Get<IncidentSettings>(MainFile.SettingsKey);
        Normalize(settings);
        return settings;
    }

    private static void RegisterMainPage()
    {
        RitsuLibFramework.RegisterModSettings(MainFile.ModId, page => page
            .WithTitle(T("mod.name", "Random Combat Events"))
            .WithModDisplayName(T("mod.name", "Random Combat Events"))
            .WithDescription(T("settings.main.description",
                "At combat start, a fixed seed plans checkpoints for the first 100 turns. Reloading will not reroll them."))
            .AddSection("presets", section => section
                .WithTitle(T("settings.section.presets", "Modes"))
                .AddButton("preset_casual", T("settings.preset.casual", "Casual"),
                    T("settings.action.apply", "Apply"), host =>
                    ApplyPreset(IncidentPreset.Casual, host),
                    description: T("settings.preset.casual.description",
                        "Checks every 5–7 turns, schedules an event 35% of the time, and warns 2 turns ahead."))
                .AddButton("preset_standard", T("settings.preset.standard", "Standard"),
                    T("settings.action.apply", "Apply"), host =>
                    ApplyPreset(IncidentPreset.Standard, host),
                    description: T("settings.preset.standard.description",
                        "Checks every 3–5 turns, schedules an event 50% of the time, and warns 1 turn ahead."))
                .AddButton("preset_chaos", T("settings.preset.chaos", "Chaos"),
                    T("settings.action.apply", "Apply"), host =>
                    ApplyPreset(IncidentPreset.Chaos, host),
                    description: T("settings.preset.chaos.description",
                        "Checks every 1–3 turns and schedules an event 80% of the time.")))
            .AddSection("schedule", section => section
                .WithTitle(T("settings.section.route", "Fixed Route"))
                .AddToggle("enabled", T("settings.enabled", "Enable Random Combat Events"),
                    BindBool(s => s.Enabled, (s, value) => s.Enabled = value))
                .AddIntSlider("minimum_gap", T("settings.minimum_gap", "Minimum Checkpoint Gap"),
                    BindInt(s => s.MinimumCheckpointGap, (s, value) => MarkCustom(s, () => s.MinimumCheckpointGap = value)),
                    1, 20, valueFormatter: ModLocalization.Turns)
                .AddIntSlider("maximum_gap", T("settings.maximum_gap", "Maximum Checkpoint Gap"),
                    BindInt(s => s.MaximumCheckpointGap, (s, value) => MarkCustom(s, () => s.MaximumCheckpointGap = value)),
                    1, 20, valueFormatter: ModLocalization.Turns)
                .AddIntSlider("incident_chance", T("settings.incident_chance", "Event Chance at Each Checkpoint"),
                    BindInt(s => s.IncidentChancePercent, (s, value) => MarkCustom(s, () => s.IncidentChancePercent = value)),
                    0, 100, 5, value => $"{value}%",
                    T("settings.incident_chance.description",
                        "An empty checkpoint still rolls the next 3–5 turn gap, so events may occur on turns 3, 11, and 15."))
                .AddIntSlider("warning_turns", T("settings.warning_turns", "Advance Warning"),
                    BindInt(s => s.WarningTurns, (s, value) => MarkCustom(s, () => s.WarningTurns = value)),
                    0, 3, valueFormatter: ModLocalization.Turns)
                .AddToggle("allow_overlap", T("settings.allow_overlap", "Allow Ongoing Events to Overlap"),
                    BindBool(s => s.AllowOverlap, (s, value) => MarkCustom(s, () => s.AllowOverlap = value)))
                .AddIntSlider("maximum_turn", T("settings.maximum_turn", "Route Endpoint"),
                    BindInt(s => s.MaximumScheduledTurn, (s, value) => MarkCustom(s, () => s.MaximumScheduledTurn = value)),
                    10, 100, 5, value => ModLocalization.Format("format.turn_number", "Turn {0}", value)))
            .AddSection("combat_types", section => section
                .WithTitle(T("settings.section.combat_types", "Combat Types"))
                .AddToggle("normal", T("settings.combat.normal", "Normal Combat"),
                    BindBool(s => s.EnableNormalCombats, (s, value) => s.EnableNormalCombats = value))
                .AddToggle("elite", T("settings.combat.elite", "Elite Combat"),
                    BindBool(s => s.EnableEliteCombats, (s, value) => s.EnableEliteCombats = value))
                .AddToggle("boss", T("settings.combat.boss", "Boss Combat"),
                    BindBool(s => s.EnableBossCombats, (s, value) => s.EnableBossCombats = value),
                    T("settings.combat.boss.description",
                        "Enabled by default in this version. If a specific Boss conflicts, only that combination will be disabled later.")))
            .AddSection("more", section => section
                .WithTitle(T("settings.section.individual", "Individual Content"))
                .AddSubpage("incidents", T("settings.incidents.page_title", "Event Toggles and Values"),
                    "incidents", T("settings.action.open", "Open"))));
    }

    private static void RegisterIncidentsPage()
    {
        RitsuLibFramework.RegisterModSettings(MainFile.ModId, page => page
            .AsChildOf(MainFile.ModId)
            .WithTitle(T("settings.incidents.page_title", "Event Toggles and Values"))
            .AddSection("common", section => section
                .WithTitle(T("settings.section.common_incidents", "Common Events"))
                .AddToggle("rockfall", T("settings.incident.rockfall", "Rockfall"),
                    BindBool(s => s.EnableRockfall, (s, value) => s.EnableRockfall = value))
                .AddIntSlider("rockfall_damage", T("settings.rockfall.damage", "Rockfall Damage"),
                    BindInt(s => s.RockfallDamage, (s, value) => s.RockfallDamage = value), 1, 30)
                .AddIntSlider("rockfall_weight", T("settings.rockfall.weight", "Rockfall Weight"),
                    BindInt(s => s.RockfallWeight, (s, value) => s.RockfallWeight = value), 0, 30)
                .AddToggle("sword_rain", T("settings.incident.sword_rain", "Sword Rain"),
                    BindBool(s => s.EnableSwordRain, (s, value) => s.EnableSwordRain = value))
                .AddIntSlider("sword_rain_damage", T("settings.sword_rain.damage", "Sword Rain Damage per Hit"),
                    BindInt(s => s.SwordRainDamagePerHit, (s, value) => s.SwordRainDamagePerHit = value), 1, 10)
                .AddIntSlider("sword_rain_hits", T("settings.sword_rain.hits", "Sword Rain Hit Count"),
                    BindInt(s => s.SwordRainHitCount, (s, value) => s.SwordRainHitCount = value), 1, 10)
                .AddIntSlider("sword_rain_weight", T("settings.sword_rain.weight", "Sword Rain Weight"),
                    BindInt(s => s.SwordRainWeight, (s, value) => s.SwordRainWeight = value), 0, 30)
                .AddToggle("toxic_fog", T("settings.incident.toxic_fog", "Toxic Fog"),
                    BindBool(s => s.EnableToxicFog, (s, value) => s.EnableToxicFog = value))
                .AddIntSlider("toxic_fog_poison_per_hit",
                    T("settings.toxic_fog.poison_per_hit", "Poison Gained per Damaging Hit"),
                    BindInt(s => s.ToxicFogPoisonPerHit, (s, value) => s.ToxicFogPoisonPerHit = value), 1, 10,
                    valueFormatter: ModLocalization.Layers,
                    description: T("settings.toxic_fog.description",
                        "During a Toxic Fog turn, each attack hit that is not fully Blocked triggers this once."))
                .AddIntSlider("toxic_fog_weight", T("settings.toxic_fog.weight", "Toxic Fog Weight"),
                    BindInt(s => s.ToxicFogWeight, (s, value) => s.ToxicFogWeight = value), 0, 30)
                .AddToggle("gentle_rain", T("settings.incident.gentle_rain", "Gentle Rain"),
                    BindBool(s => s.EnableGentleRain, (s, value) => s.EnableGentleRain = value))
                .AddIntSlider("gentle_rain_heal", T("settings.gentle_rain.heal", "Gentle Rain Healing"),
                    BindInt(s => s.GentleRainHealPercent, (s, value) => s.GentleRainHealPercent = value), 1, 20,
                    valueFormatter: value => ModLocalization.Format("format.max_hp_percent", "{0}% max HP", value))
                .AddIntSlider("gentle_rain_weight", T("settings.gentle_rain.weight", "Gentle Rain Weight"),
                    BindInt(s => s.GentleRainWeight, (s, value) => s.GentleRainWeight = value), 0, 30))
            .AddSection("acts", section => section
                .WithTitle(T("settings.section.act_incidents", "Act-Specific Events"))
                .AddToggle("vine_snare", T("settings.incident.vine_snare", "Overgrowth: Vine Snare"),
                    BindBool(s => s.EnableVineSnare, (s, value) => s.EnableVineSnare = value))
                .AddIntSlider("vine_weight", T("settings.vine_snare.weight", "Vine Snare Weight"),
                    BindInt(s => s.VineSnareWeight, (s, value) => s.VineSnareWeight = value), 0, 30)
                .AddIntSlider("vine_vulnerable",
                    T("settings.vine_snare.vulnerable", "Vulnerable Applied to Both Sides"),
                    BindInt(s => s.VineSnareVulnerable, (s, value) => s.VineSnareVulnerable = value), 1, 5)
                .AddToggle("damp_wind", T("settings.incident.damp_sea_wind", "Underdocks: Damp Sea Wind"),
                    BindBool(s => s.EnableDampSeaWind, (s, value) => s.EnableDampSeaWind = value))
                .AddIntSlider("damp_wind_weight", T("settings.damp_sea_wind.weight", "Damp Sea Wind Weight"),
                    BindInt(s => s.DampSeaWindWeight, (s, value) => s.DampSeaWindWeight = value), 0, 30)
                .AddIntSlider("damp_wind_weak", T("settings.damp_sea_wind.weak", "Weak Applied to Both Sides"),
                    BindInt(s => s.DampSeaWindWeak, (s, value) => s.DampSeaWindWeak = value), 1, 5)
                .AddIntSlider("damp_wind_frail", T("settings.damp_sea_wind.frail", "Frail Applied to Both Sides"),
                    BindInt(s => s.DampSeaWindFrail, (s, value) => s.DampSeaWindFrail = value), 1, 5)
                .AddToggle("hive_onslaught", T("settings.incident.hive_onslaught", "The Hive: Hive Onslaught"),
                    BindBool(s => s.EnableHiveOnslaught, (s, value) => s.EnableHiveOnslaught = value))
                .AddIntSlider("hive_weight", T("settings.hive_onslaught.weight", "Hive Onslaught Weight"),
                    BindInt(s => s.HiveOnslaughtWeight, (s, value) => s.HiveOnslaughtWeight = value), 0, 30)
                .AddIntSlider("hive_damage", T("settings.hive_onslaught.damage", "Hive Damage per Turn"),
                    BindInt(s => s.HiveOnslaughtDamage, (s, value) => s.HiveOnslaughtDamage = value), 1, 20)
                .AddIntSlider("hive_duration", T("settings.hive_onslaught.duration", "Hive Duration"),
                    BindInt(s => s.HiveOnslaughtDuration, (s, value) => s.HiveOnslaughtDuration = value), 1, 10)),
            pageId: "incidents");
    }

    private static void ApplyPreset(IncidentPreset preset, IModSettingsUiActionHost host)
    {
        var store = RitsuLibFramework.GetDataStore(MainFile.ModId);
        store.Modify<IncidentSettings>(MainFile.SettingsKey, settings => settings.ApplyPreset(preset));
        store.Save(MainFile.SettingsKey);
        host.RequestRefreshAfterDataModelBatchChange();
    }

    private static void Normalize(IncidentSettings settings)
    {
        settings.MinimumCheckpointGap = Math.Clamp(settings.MinimumCheckpointGap, 1, 100);
        settings.MaximumCheckpointGap = Math.Clamp(settings.MaximumCheckpointGap,
            settings.MinimumCheckpointGap, 100);
        settings.IncidentChancePercent = Math.Clamp(settings.IncidentChancePercent, 0, 100);
        settings.MaximumScheduledTurn = Math.Clamp(settings.MaximumScheduledTurn, 1, 100);
        settings.WarningTurns = Math.Clamp(settings.WarningTurns, 0, 10);
        settings.RockfallWeight = Math.Clamp(settings.RockfallWeight, 0, 30);
        settings.RockfallDamage = Math.Clamp(settings.RockfallDamage, 1, 30);
        settings.SwordRainWeight = Math.Clamp(settings.SwordRainWeight, 0, 30);
        settings.SwordRainDamagePerHit = Math.Clamp(settings.SwordRainDamagePerHit, 1, 10);
        settings.SwordRainHitCount = Math.Clamp(settings.SwordRainHitCount, 1, 10);
        settings.ToxicFogWeight = Math.Clamp(settings.ToxicFogWeight, 0, 30);
        settings.ToxicFogPoisonPerHit = Math.Clamp(settings.ToxicFogPoisonPerHit, 1, 10);
        settings.GentleRainWeight = Math.Clamp(settings.GentleRainWeight, 0, 30);
        settings.GentleRainHealPercent = Math.Clamp(settings.GentleRainHealPercent, 1, 20);
        settings.VineSnareWeight = Math.Clamp(settings.VineSnareWeight, 0, 30);
        settings.VineSnareVulnerable = Math.Clamp(settings.VineSnareVulnerable, 1, 5);
        settings.DampSeaWindWeight = Math.Clamp(settings.DampSeaWindWeight, 0, 30);
        settings.DampSeaWindWeak = Math.Clamp(settings.DampSeaWindWeak, 1, 5);
        settings.DampSeaWindFrail = Math.Clamp(settings.DampSeaWindFrail, 1, 5);
        settings.HiveOnslaughtWeight = Math.Clamp(settings.HiveOnslaughtWeight, 0, 30);
        settings.HiveOnslaughtDamage = Math.Clamp(settings.HiveOnslaughtDamage, 1, 20);
        settings.HiveOnslaughtDuration = Math.Clamp(settings.HiveOnslaughtDuration, 1, 10);
    }

    private static void MarkCustom(IncidentSettings settings, Action mutation)
    {
        mutation();
        settings.Preset = IncidentPreset.Custom;
        Normalize(settings);
    }

    private static ModSettingsValueBinding<IncidentSettings, bool> BindBool(
        Func<IncidentSettings, bool> getter,
        Action<IncidentSettings, bool> setter) =>
        new(MainFile.ModId, MainFile.SettingsKey, SaveScope.Global, getter, setter);

    private static ModSettingsValueBinding<IncidentSettings, int> BindInt(
        Func<IncidentSettings, int> getter,
        Action<IncidentSettings, int> setter) =>
        new(MainFile.ModId, MainFile.SettingsKey, SaveScope.Global, getter, setter);

    private static ModSettingsText T(string key, string englishFallback) =>
        ModLocalization.Text(key, englishFallback);
}
