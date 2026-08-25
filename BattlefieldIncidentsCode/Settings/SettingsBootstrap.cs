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
                        "Checks every 1–3 turns and schedules an event 80% of the time."))
                .AddButton("preset_hell", T("settings.preset.hell", "Hell"),
                    T("settings.action.apply", "Apply"), host =>
                    ApplyPreset(IncidentPreset.Hell, host),
                    description: T("settings.preset.hell.description",
                        "Checks every 2–4 turns, schedules an event 75% of the time, opens every fight on the Architect's Curse, and cuts the helpful events down to almost nothing. The other three modes can only ever open on a gift.")))
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
            .AddSection("combat_start", section => section
                .WithTitle(T("settings.section.combat_start", "Combat Start"))
                .AddToggle("combat_start_enabled",
                    T("settings.combat_start.enabled", "Enable Combat-Start Blessing and Curse"),
                    BindBool(s => s.EnableCombatStartBoons, (s, value) => s.EnableCombatStartBoons = value),
                    T("settings.combat_start.description",
                        "Rolled once when combat begins, separately from the turn route. The remainder of the two chances is no effect."))
                .AddIntSlider("combat_start_blessing",
                    T("settings.combat_start.blessing", "Neow's Blessing Chance"),
                    BindInt(s => s.CombatStartBlessingPercent, (s, value) => s.CombatStartBlessingPercent = value),
                    0, 100, 1, value => $"{value}%")
                .AddIntSlider("combat_start_curse",
                    T("settings.combat_start.curse", "Architect's Curse Chance"),
                    BindInt(s => s.CombatStartCursePercent, (s, value) => s.CombatStartCursePercent = value),
                    0, 100, 1, value => $"{value}%")
                .AddToggle("combat_start_normal",
                    T("settings.combat_start.normal", "Combat Start in Normal Combat"),
                    BindBool(s => s.EnableCombatStartNormalCombats,
                        (s, value) => s.EnableCombatStartNormalCombats = value))
                .AddToggle("combat_start_elite",
                    T("settings.combat_start.elite", "Combat Start in Elite Combat"),
                    BindBool(s => s.EnableCombatStartEliteCombats,
                        (s, value) => s.EnableCombatStartEliteCombats = value))
                .AddToggle("combat_start_boss",
                    T("settings.combat_start.boss", "Combat Start in Boss Combat"),
                    BindBool(s => s.EnableCombatStartBossCombats,
                        (s, value) => s.EnableCombatStartBossCombats = value)))
            .AddSection("miracle", section => section
                .WithTitle(T("settings.section.miracle", "Last Miracle"))
                .AddToggle("miracle_enabled", T("settings.miracle.enabled", "Enable Last Miracle"),
                    BindBool(s => s.EnableMiracle, (s, value) => s.EnableMiracle = value),
                    T("settings.miracle.description",
                        "Rolled per unit when combat begins and announced on screen while it lasts. A unit holding one survives its first lethal blow at 1 HP; later hits of the same attack still land."))
                .AddIntSlider("miracle_player", T("settings.miracle.player", "Player Chance"),
                    BindInt(s => s.PlayerMiracleChancePermille,
                        (s, value) => s.PlayerMiracleChancePermille = value),
                    0, 1000, 5, ModLocalization.Permille)
                .AddIntSlider("miracle_enemy", T("settings.miracle.enemy", "Enemy Chance"),
                    BindInt(s => s.EnemyMiracleChancePermille,
                        (s, value) => s.EnemyMiracleChancePermille = value),
                    0, 1000, 5, ModLocalization.Permille))
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
                .AddIntSlider("gentle_rain_player_minimum",
                    T("settings.gentle_rain.player_minimum", "Gentle Rain Minimum For Players"),
                    BindInt(s => s.GentleRainPlayerMinimumHeal,
                        (s, value) => s.GentleRainPlayerMinimumHeal = value), 1, 50,
                    description: T("settings.gentle_rain.player_minimum.description",
                        "The least a player heals for, whatever the percentage comes to. Enemies keep a minimum of 1, so without this the rain pays the fuller enemy side more than it pays you."))
                .AddIntSlider("gentle_rain_weight", T("settings.gentle_rain.weight", "Gentle Rain Weight"),
                    BindInt(s => s.GentleRainWeight, (s, value) => s.GentleRainWeight = value), 0, 30)
                .AddToggle("laser", T("settings.incident.laser", "Laser"),
                    BindBool(s => s.EnableLaser, (s, value) => s.EnableLaser = value))
                .AddIntSlider("laser_percent", T("settings.laser.percent", "Laser Damage"),
                    BindInt(s => s.LaserHpPercent, (s, value) => s.LaserHpPercent = value), 1, 25,
                    valueFormatter: value => ModLocalization.Format("format.max_hp_percent", "{0}% max HP", value),
                    description: T("settings.laser.description",
                        "Charged against each unit's own max HP, so it costs a Boss far more than it costs you, and it resolves at the end of each side's own turn where Block still works."))
                .AddIntSlider("laser_weight", T("settings.laser.weight", "Laser Weight"),
                    BindInt(s => s.LaserWeight, (s, value) => s.LaserWeight = value), 0, 30)
                .AddToggle("neows_blessing", T("settings.incident.neows_blessing", "Neow's Blessing"),
                    BindBool(s => s.EnableNeowsBlessing, (s, value) => s.EnableNeowsBlessing = value))
                .AddIntSlider("neows_blessing_weight",
                    T("settings.neows_blessing.weight", "Neow's Blessing Weight"),
                    BindInt(s => s.NeowsBlessingWeight, (s, value) => s.NeowsBlessingWeight = value), 0, 30)
                .AddToggle("architects_curse", T("settings.incident.architects_curse", "Architect's Curse"),
                    BindBool(s => s.EnableArchitectsCurse, (s, value) => s.EnableArchitectsCurse = value))
                .AddIntSlider("architects_curse_weight",
                    T("settings.architects_curse.weight", "Architect's Curse Weight"),
                    BindInt(s => s.ArchitectsCurseWeight, (s, value) => s.ArchitectsCurseWeight = value), 0, 30))
            .AddSection("summons", section => section
                .WithTitle(T("settings.section.summons", "Summon Events"))
                .AddToggle("free_summon", T("settings.incident.free_summon", "Wandering Monster"),
                    BindBool(s => s.EnableFreeSummon, (s, value) => s.EnableFreeSummon = value),
                    T("settings.free_summon.description",
                        "A small monster wanders in and picks a side. No choice and no cost, so it is always drawn from the weakest tier."))
                .AddIntSlider("free_summon_ally", T("settings.free_summon.ally", "Chance It Takes Your Side"),
                    BindInt(s => s.FreeSummonAllyPercent, (s, value) => s.FreeSummonAllyPercent = value),
                    0, 100, 5, value => $"{value}%")
                .AddIntSlider("free_summon_weight", T("settings.free_summon.weight", "Wandering Monster Weight"),
                    BindInt(s => s.FreeSummonWeight, (s, value) => s.FreeSummonWeight = value), 0, 30)
                .AddToggle("mercenary", T("settings.incident.mercenary", "Mercenary"),
                    BindBool(s => s.EnableMercenary, (s, value) => s.EnableMercenary = value),
                    T("settings.mercenary.description",
                        "A monster offers to fight for you. The gold is taken on the following turn, and it is taken whether or not the monster keeps its word."))
                .AddIntSlider("mercenary_price", T("settings.mercenary.price", "Mercenary Fee"),
                    BindInt(s => s.MercenaryPrice, (s, value) => s.MercenaryPrice = value), 0, 300, 5,
                    ModLocalization.Gold)
                .AddIntSlider("mercenary_run_off", T("settings.mercenary.run_off", "Chance It Takes the Gold and Runs"),
                    BindInt(s => s.MercenaryRunOffPercent, (s, value) => s.MercenaryRunOffPercent = value),
                    0, 100, 1, value => $"{value}%")
                .AddIntSlider("mercenary_betrayal", T("settings.mercenary.betrayal", "Chance It Turns on You"),
                    BindInt(s => s.MercenaryBetrayalPercent, (s, value) => s.MercenaryBetrayalPercent = value),
                    0, 100, 1, value => $"{value}%")
                .AddIntSlider("mercenary_weight", T("settings.mercenary.weight", "Mercenary Weight"),
                    BindInt(s => s.MercenaryWeight, (s, value) => s.MercenaryWeight = value), 0, 30)
                .AddToggle("enemy_recruit", T("settings.incident.enemy_recruit", "Enemy Recruit"),
                    BindBool(s => s.EnableEnemyRecruit, (s, value) => s.EnableEnemyRecruit = value),
                    T("settings.enemy_recruit.description",
                        "A monster is about to join the enemy. Gold can talk it down, or cost more and bring it over to you."))
                .AddIntSlider("stand_down_price", T("settings.enemy_recruit.stand_down", "Stand-Down Fee"),
                    BindInt(s => s.StandDownPrice, (s, value) => s.StandDownPrice = value), 0, 300, 5,
                    ModLocalization.Gold)
                .AddIntSlider("hire_price", T("settings.enemy_recruit.hire", "Change-Sides Fee"),
                    BindInt(s => s.HirePrice, (s, value) => s.HirePrice = value), 0, 300, 5,
                    ModLocalization.Gold)
                .AddIntSlider("recruit_failure", T("settings.enemy_recruit.failure", "Chance the Gold Is Wasted"),
                    BindInt(s => s.RecruitFailurePercent, (s, value) => s.RecruitFailurePercent = value),
                    0, 100, 1, value => $"{value}%")
                .AddIntSlider("enemy_recruit_weight", T("settings.enemy_recruit.weight", "Enemy Recruit Weight"),
                    BindInt(s => s.EnemyRecruitWeight, (s, value) => s.EnemyRecruitWeight = value), 0, 30)
                .AddToggle("challenge", T("settings.incident.challenge", "Challenger"),
                    BindBool(s => s.EnableChallenge, (s, value) => s.EnableChallenge = value),
                    T("settings.challenge.description",
                        "Something asks to join the fight against you. Free to refuse, and worth extra spoils if you take it on."))
                .AddIntSlider("challenge_elite", T("settings.challenge.elite", "Chance It Is an Elite"),
                    BindInt(s => s.ChallengeElitePercent, (s, value) => s.ChallengeElitePercent = value),
                    0, 100, 5, value => $"{value}%")
                .AddIntSlider("challenge_upset", T("settings.challenge.upset", "Chance It Does the Opposite"),
                    BindInt(s => s.ChallengeUpsetPercent, (s, value) => s.ChallengeUpsetPercent = value),
                    0, 100, 1, value => $"{value}%")
                .AddIntSlider("challenge_weight", T("settings.challenge.weight", "Challenger Weight"),
                    BindInt(s => s.ChallengeWeight, (s, value) => s.ChallengeWeight = value), 0, 30)
                .AddIntSlider("summon_departure", T("settings.summons.departure", "Chance a Summon Leaves Each Turn"),
                    BindInt(s => s.SummonDepartureChancePercent,
                        (s, value) => s.SummonDepartureChancePercent = value),
                    0, 100, 5, value => $"{value}%",
                    T("settings.summons.departure.description",
                        "Rolled every turn for each summoned monster, on either side. Nothing that wandered into a fight has a reason to see it through, so they do not have to be killed to be rid of."))
                .AddIntSlider("extra_monster_gold", T("settings.summons.extra_gold", "Extra Gold per Added Monster"),
                    BindInt(s => s.ExtraMonsterGold, (s, value) => s.ExtraMonsterGold = value), 0, 200, 5,
                    ModLocalization.Gold,
                    T("settings.summons.extra_gold.description",
                        "Built to the same recipe the game uses for a room: gold, a card to choose from, a potion on a roll, and a relic for anything Elite-sized."))
                .AddIntSlider("extra_elite_gold", T("settings.summons.elite_gold", "Extra Gold per Added Elite"),
                    BindInt(s => s.ExtraEliteGold, (s, value) => s.ExtraEliteGold = value), 0, 400, 10,
                    ModLocalization.Gold)
                .AddIntSlider("extra_potion", T("settings.summons.potion", "Chance of a Potion"),
                    BindInt(s => s.ExtraPotionPercent, (s, value) => s.ExtraPotionPercent = value),
                    0, 100, 5, value => $"{value}%")
                .AddIntSlider("extra_relic", T("settings.summons.relic", "Chance of a Relic from an Ordinary Monster"),
                    BindInt(s => s.ExtraRelicPercent, (s, value) => s.ExtraRelicPercent = value),
                    0, 100, 5, value => $"{value}%",
                    T("settings.summons.relic.description",
                        "An added Elite always leaves a relic, the same way an Elite room always does. This is the chance an ordinary monster does too."))
                .AddToggle("extra_card", T("settings.summons.card", "Card Reward per Added Monster"),
                    BindBool(s => s.ExtraCardReward, (s, value) => s.ExtraCardReward = value)))
            .AddSection("ancients", section => section
                .WithTitle(T("settings.section.ancients", "The Ancients"))
                .AddToggle("vakuu", T("settings.incident.vakuu", "Vakuu's Takeover"),
                    BindBool(s => s.EnableVakuu, (s, value) => s.EnableVakuu = value),
                    T("settings.vakuu.description",
                        "Asks first. Accept and Vakuu plays your whole hand next turn, in the order it sits, at whatever it happens to be aimed at — then burns every card it touched, playable or not."))
                .AddIntSlider("vakuu_weight", T("settings.vakuu.weight", "Vakuu Weight"),
                    BindInt(s => s.VakuuWeight, (s, value) => s.VakuuWeight = value), 0, 30)
                .AddToggle("darv", T("settings.incident.darv", "Darv's Gamble"),
                    BindBool(s => s.EnableDarv, (s, value) => s.EnableDarv = value),
                    T("settings.darv.description",
                        "Asks first. Accept and next turn opens on a full hand, with Confused for the rest of the fight: every card you draw from then on costs whatever it feels like."))
                .AddIntSlider("darv_weight", T("settings.darv.weight", "Darv Weight"),
                    BindInt(s => s.DarvWeight, (s, value) => s.DarvWeight = value), 0, 30)
                .AddToggle("nonupeipe", T("settings.incident.nonupeipe", "Nonupeipe's Gift"),
                    BindBool(s => s.EnableNonupeipe, (s, value) => s.EnableNonupeipe = value),
                    T("settings.nonupeipe.description",
                        "One of three gifts, drawn on the weights below: max HP now, gold once the fight is won, or a room marked on the map where one enemy will be waiting at 1 HP."))
                .AddIntSlider("nonupeipe_weight", T("settings.nonupeipe.weight", "Nonupeipe Weight"),
                    BindInt(s => s.NonupeipeWeight, (s, value) => s.NonupeipeWeight = value), 0, 30)
                .AddIntSlider("nonupeipe_max_hp_weight",
                    T("settings.nonupeipe.max_hp_weight", "Gift Weight · Max HP"),
                    BindInt(s => s.NonupeipeMaxHpWeight, (s, value) => s.NonupeipeMaxHpWeight = value), 0, 30)
                .AddIntSlider("nonupeipe_max_hp", T("settings.nonupeipe.max_hp", "Max HP Granted"),
                    BindInt(s => s.NonupeipeMaxHp, (s, value) => s.NonupeipeMaxHp = value), 1, 20)
                .AddIntSlider("nonupeipe_gold_weight",
                    T("settings.nonupeipe.gold_weight", "Gift Weight · Gold"),
                    BindInt(s => s.NonupeipeGoldWeight, (s, value) => s.NonupeipeGoldWeight = value), 0, 30)
                .AddIntSlider("nonupeipe_gold", T("settings.nonupeipe.gold", "Gold Granted"),
                    BindInt(s => s.NonupeipeGold, (s, value) => s.NonupeipeGold = value), 0, 300, 5,
                    ModLocalization.Gold)
                .AddIntSlider("nonupeipe_marked_weight",
                    T("settings.nonupeipe.marked_weight", "Gift Weight · Marked Room"),
                    BindInt(s => s.NonupeipeMarkedRoomWeight,
                        (s, value) => s.NonupeipeMarkedRoomWeight = value), 0, 30,
                    description: T("settings.nonupeipe.marked.description",
                        "The mark lives on the map for this session only. Quitting to the menu and loading the run back in clears it."))
                .AddToggle("tanx", T("settings.incident.tanx", "Tanx's Armory"),
                    BindBool(s => s.EnableTanx, (s, value) => s.EnableTanx = value),
                    T("settings.tanx.description",
                        "A random attack from your own discipline, free this turn and gone at the end of it."))
                .AddIntSlider("tanx_weight", T("settings.tanx.weight", "Tanx Weight"),
                    BindInt(s => s.TanxWeight, (s, value) => s.TanxWeight = value), 0, 30)
                .AddToggle("tezcatara", T("settings.incident.tezcatara", "Tezcatara's Ember"),
                    BindBool(s => s.EnableTezcatara, (s, value) => s.EnableTezcatara = value),
                    T("settings.tezcatara.description",
                        "A relic cast in wax. It works for the rest of this fight and melts when the fight ends, the same way the Toy Box's do — it stays in your collection, spent."))
                .AddIntSlider("tezcatara_weight", T("settings.tezcatara.weight", "Tezcatara Weight"),
                    BindInt(s => s.TezcataraWeight, (s, value) => s.TezcataraWeight = value), 0, 30)
                .AddToggle("pael", T("settings.incident.pael", "Pael's Blessing"),
                    BindBool(s => s.EnablePael, (s, value) => s.EnablePael = value),
                    T("settings.pael.description", "Extra energy and an extra card, paid out next turn."))
                .AddIntSlider("pael_weight", T("settings.pael.weight", "Pael Weight"),
                    BindInt(s => s.PaelWeight, (s, value) => s.PaelWeight = value), 0, 30)
                .AddIntSlider("pael_energy", T("settings.pael.energy", "Energy Granted"),
                    BindInt(s => s.PaelEnergy, (s, value) => s.PaelEnergy = value), 0, 5)
                .AddIntSlider("pael_cards", T("settings.pael.cards", "Cards Drawn"),
                    BindInt(s => s.PaelCards, (s, value) => s.PaelCards = value), 0, 5)
                .AddToggle("orobas", T("settings.incident.orobas", "Orobas's Offer"),
                    BindBool(s => s.EnableOrobas, (s, value) => s.EnableOrobas = value),
                    T("settings.orobas.description",
                        "Cards from every discipline but your own. Pick one; it is free this turn and gone at the end of it."))
                .AddIntSlider("orobas_weight", T("settings.orobas.weight", "Orobas Weight"),
                    BindInt(s => s.OrobasWeight, (s, value) => s.OrobasWeight = value), 0, 30)
                .AddIntSlider("orobas_choices", T("settings.orobas.choices", "Cards Offered"),
                    BindInt(s => s.OrobasChoices, (s, value) => s.OrobasChoices = value), 1, 5))
            .AddSection("multiplayer", section => section
                .WithTitle(T("settings.section.multiplayer", "Co-op"))
                .AddToggle("strangling_vines", T("settings.incident.strangling_vines", "Strangling Vines"),
                    BindBool(s => s.EnableStranglingVines, (s, value) => s.EnableStranglingVines = value),
                    T("settings.strangling_vines.description",
                        "Co-op only. One player opens the fight held by vines and cannot play cards; anyone else can cut them down with a single attack. Potions still work, and the vines let go on their own eventually so a table can never be stuck."))
                .AddIntSlider("strangling_vines_chance",
                    T("settings.strangling_vines.chance", "Strangling Vines Chance"),
                    BindInt(s => s.StranglingVinesChancePercent,
                        (s, value) => s.StranglingVinesChancePercent = value), 0, 100, 5, value => $"{value}%")
                .AddIntSlider("strangling_vines_escape",
                    T("settings.strangling_vines.escape", "Vines Let Go After"),
                    BindInt(s => s.StranglingVinesEscapeTurns,
                        (s, value) => s.StranglingVinesEscapeTurns = value), 1, 10,
                    valueFormatter: ModLocalization.Turns)
                .AddIntSlider("multiplayer_price_divisor",
                    T("settings.multiplayer.price_divisor", "Co-op Share of a Price"),
                    BindInt(s => s.MultiplayerPriceDivisor, (s, value) => s.MultiplayerPriceDivisor = value),
                    1, 8, 1, value => ModLocalization.Format("format.price_share", "1/{0} each", value),
                    T("settings.multiplayer.price_divisor.description",
                        "What each player pays when the table buys something. Applied whatever the party size, so a full table is not the only one that can afford to say yes. Single-player runs always pay the full price.")))
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
        settings.GentleRainPlayerMinimumHeal = Math.Clamp(settings.GentleRainPlayerMinimumHeal, 1, 50);
        settings.VineSnareWeight = Math.Clamp(settings.VineSnareWeight, 0, 30);
        settings.VineSnareVulnerable = Math.Clamp(settings.VineSnareVulnerable, 1, 5);
        settings.DampSeaWindWeight = Math.Clamp(settings.DampSeaWindWeight, 0, 30);
        settings.DampSeaWindWeak = Math.Clamp(settings.DampSeaWindWeak, 1, 5);
        settings.DampSeaWindFrail = Math.Clamp(settings.DampSeaWindFrail, 1, 5);
        settings.HiveOnslaughtWeight = Math.Clamp(settings.HiveOnslaughtWeight, 0, 30);
        settings.HiveOnslaughtDamage = Math.Clamp(settings.HiveOnslaughtDamage, 1, 20);
        settings.HiveOnslaughtDuration = Math.Clamp(settings.HiveOnslaughtDuration, 1, 10);
        settings.LaserWeight = Math.Clamp(settings.LaserWeight, 0, 30);
        settings.LaserHpPercent = Math.Clamp(settings.LaserHpPercent, 1, 25);
        settings.CombatStartBlessingPercent = Math.Clamp(settings.CombatStartBlessingPercent, 0, 100);
        settings.CombatStartCursePercent = Math.Clamp(settings.CombatStartCursePercent, 0, 100);
        settings.FreeSummonWeight = Math.Clamp(settings.FreeSummonWeight, 0, 30);
        settings.FreeSummonAllyPercent = Math.Clamp(settings.FreeSummonAllyPercent, 0, 100);
        settings.MercenaryWeight = Math.Clamp(settings.MercenaryWeight, 0, 30);
        settings.MercenaryPrice = Math.Clamp(settings.MercenaryPrice, 0, 300);
        settings.MercenaryRunOffPercent = Math.Clamp(settings.MercenaryRunOffPercent, 0, 100);
        settings.MercenaryBetrayalPercent = Math.Clamp(settings.MercenaryBetrayalPercent, 0, 100);
        settings.EnemyRecruitWeight = Math.Clamp(settings.EnemyRecruitWeight, 0, 30);
        settings.StandDownPrice = Math.Clamp(settings.StandDownPrice, 0, 300);
        settings.HirePrice = Math.Clamp(settings.HirePrice, 0, 300);
        settings.RecruitFailurePercent = Math.Clamp(settings.RecruitFailurePercent, 0, 100);
        settings.ChallengeWeight = Math.Clamp(settings.ChallengeWeight, 0, 30);
        settings.ChallengeUpsetPercent = Math.Clamp(settings.ChallengeUpsetPercent, 0, 100);
        settings.ChallengeElitePercent = Math.Clamp(settings.ChallengeElitePercent, 0, 100);
        settings.ExtraMonsterGold = Math.Clamp(settings.ExtraMonsterGold, 0, 200);
        settings.SummonDepartureChancePercent = Math.Clamp(settings.SummonDepartureChancePercent, 0, 100);
        settings.ExtraEliteGold = Math.Clamp(settings.ExtraEliteGold, 0, 400);
        settings.ExtraPotionPercent = Math.Clamp(settings.ExtraPotionPercent, 0, 100);
        settings.ExtraRelicPercent = Math.Clamp(settings.ExtraRelicPercent, 0, 100);
        settings.PlayerMiracleChancePermille = Math.Clamp(settings.PlayerMiracleChancePermille, 0, 1000);
        settings.EnemyMiracleChancePermille = Math.Clamp(settings.EnemyMiracleChancePermille, 0, 1000);
        settings.VakuuWeight = Math.Clamp(settings.VakuuWeight, 0, 30);
        settings.DarvWeight = Math.Clamp(settings.DarvWeight, 0, 30);
        settings.NonupeipeWeight = Math.Clamp(settings.NonupeipeWeight, 0, 30);
        settings.NonupeipeMaxHpWeight = Math.Clamp(settings.NonupeipeMaxHpWeight, 0, 30);
        settings.NonupeipeGoldWeight = Math.Clamp(settings.NonupeipeGoldWeight, 0, 30);
        settings.NonupeipeMarkedRoomWeight = Math.Clamp(settings.NonupeipeMarkedRoomWeight, 0, 30);
        settings.NonupeipeMaxHp = Math.Clamp(settings.NonupeipeMaxHp, 1, 20);
        settings.NonupeipeGold = Math.Clamp(settings.NonupeipeGold, 0, 300);
        settings.TanxWeight = Math.Clamp(settings.TanxWeight, 0, 30);
        settings.TezcataraWeight = Math.Clamp(settings.TezcataraWeight, 0, 30);
        settings.PaelWeight = Math.Clamp(settings.PaelWeight, 0, 30);
        settings.PaelEnergy = Math.Clamp(settings.PaelEnergy, 0, 5);
        settings.PaelCards = Math.Clamp(settings.PaelCards, 0, 5);
        settings.OrobasWeight = Math.Clamp(settings.OrobasWeight, 0, 30);
        settings.OrobasChoices = Math.Clamp(settings.OrobasChoices, 1, 5);
        settings.StranglingVinesChancePercent = Math.Clamp(settings.StranglingVinesChancePercent, 0, 100);
        settings.StranglingVinesEscapeTurns = Math.Clamp(settings.StranglingVinesEscapeTurns, 1, 10);
        settings.MultiplayerPriceDivisor = Math.Clamp(settings.MultiplayerPriceDivisor, 1, 8);
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
