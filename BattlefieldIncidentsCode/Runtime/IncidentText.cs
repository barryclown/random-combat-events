using BattlefieldIncidents.Localization;
using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace BattlefieldIncidents.Runtime;

internal static class IncidentText
{
    public static string Name(IncidentKind kind) => kind switch
    {
        IncidentKind.Rockfall => L("incident.name.rockfall", "Rockfall"),
        IncidentKind.SwordRain => L("incident.name.sword_rain", "Sword Rain"),
        IncidentKind.ToxicFog => L("incident.name.toxic_fog", "Toxic Fog"),
        IncidentKind.VineSnare => L("incident.name.vine_snare", "Vine Snare"),
        IncidentKind.DampSeaWind => L("incident.name.damp_sea_wind", "Damp Sea Wind"),
        IncidentKind.HiveOnslaught => L("incident.name.hive_onslaught", "Hive Onslaught"),
        IncidentKind.GentleRain => L("incident.name.gentle_rain", "Gentle Rain"),
        _ => kind.ToString(),
    };

    public static string Warning(
        ScheduledCheckpoint checkpoint,
        int turnsRemaining,
        IncidentSettings settings) => checkpoint.Incident switch
    {
        IncidentKind.Rockfall =>
            F("incident.warning.rockfall",
                "In {0}, falling rocks will deal {1} damage to all players at the end of the player side's turn. Block can prevent it.",
                ModLocalization.Turns(turnsRemaining), settings.RockfallDamage),
        IncidentKind.SwordRain =>
            F("incident.warning.sword_rain",
                "In {0}, Sword Rain will strike every unit on each side at the end of that side's turn for {1} damage × {2}. Block can prevent it.",
                ModLocalization.Turns(turnsRemaining), settings.SwordRainDamagePerHit,
                settings.SwordRainHitCount),
        IncidentKind.ToxicFog =>
            F("incident.warning.toxic_fog",
                "In {0}, Toxic Fog will cover the battlefield for that turn. After taking unblocked attack damage, a unit gains {1} {2}.",
                ModLocalization.Turns(turnsRemaining), settings.ToxicFogPoisonPerHit,
                PowerName<PoisonPower>()),
        IncidentKind.VineSnare =>
            F("incident.warning.vine_snare",
                "In {0}, vines will apply {1} {2} to all players and enemies.",
                ModLocalization.Turns(turnsRemaining), settings.VineSnareVulnerable,
                PowerName<VulnerablePower>()),
        IncidentKind.DampSeaWind =>
            F("incident.warning.damp_sea_wind",
                "In {0}, Damp Sea Wind will apply {1} {2} and {3} {4} to all players and enemies.",
                ModLocalization.Turns(turnsRemaining), settings.DampSeaWindWeak,
                PowerName<WeakPower>(), settings.DampSeaWindFrail, PowerName<FrailPower>()),
        IncidentKind.HiveOnslaught =>
            F("incident.warning.hive_onslaught",
                "In {0}, Hive Onslaught will begin. For {1}, both sides take {2} damage at the end of their own side's turn. Block can prevent it.",
                ModLocalization.Turns(turnsRemaining), ModLocalization.Turns(checkpoint.Duration),
                settings.HiveOnslaughtDamage),
        IncidentKind.GentleRain =>
            F("incident.warning.gentle_rain",
                "In {0}, Gentle Rain will heal every living unit for {1}% of its own max HP, with a minimum of 1 HP.",
                ModLocalization.Turns(turnsRemaining), settings.GentleRainHealPercent),
        _ => F("incident.warning.default", "A battlefield event will occur in {0}.",
            ModLocalization.Turns(turnsRemaining)),
    };

    public static string Trigger(ScheduledCheckpoint checkpoint, IncidentSettings settings) => checkpoint.Incident switch
    {
        IncidentKind.Rockfall =>
            F("incident.trigger.rockfall",
                "The boulders are coming loose! At the end of this turn, all players will take {0} damage. Block can prevent it.",
                settings.RockfallDamage),
        IncidentKind.SwordRain =>
            F("incident.trigger.sword_rain",
                "Blades fill the sky! Each side will take {0} damage × {1} at the end of its own turn. Block can prevent it.",
                settings.SwordRainDamagePerHit, settings.SwordRainHitCount),
        IncidentKind.ToxicFog =>
            F("incident.trigger.toxic_fog",
                "Toxic Fog covers the battlefield! This turn, after taking unblocked attack damage, a unit gains {0} {1}.",
                settings.ToxicFogPoisonPerHit, PowerName<PoisonPower>()),
        IncidentKind.VineSnare =>
            F("incident.trigger.vine_snare",
                "Vines bind both sides! All players and enemies gain {0} {1}.",
                settings.VineSnareVulnerable, PowerName<VulnerablePower>()),
        IncidentKind.DampSeaWind =>
            F("incident.trigger.damp_sea_wind",
                "Damp Sea Wind erodes the battlefield! All players and enemies gain {0} {1} and {2} {3}.",
                settings.DampSeaWindWeak, PowerName<WeakPower>(), settings.DampSeaWindFrail,
                PowerName<FrailPower>()),
        IncidentKind.HiveOnslaught => HiveWave(checkpoint, 1, settings),
        IncidentKind.GentleRain =>
            F("incident.trigger.gentle_rain",
                "Gentle Rain soaks the battlefield! Every living unit heals {0}% of its own max HP, with a minimum of 1 HP.",
                settings.GentleRainHealPercent),
        _ => L("incident.trigger.default", "The battlefield shifts!"),
    };

    public static string HiveWave(ScheduledCheckpoint checkpoint, int wave, IncidentSettings settings) =>
        F("incident.hive.wave",
            "Hive wave {0}/{1} approaches! Both sides will take {2} damage at the end of their own side's turn. Block can prevent it.",
            wave, checkpoint.Duration, settings.HiveOnslaughtDamage);

    public static string RockfallImpact(decimal damage) =>
        F("incident.impact.rockfall",
            "The boulders fall! All players are about to take {0} damage. Block can prevent it.", damage);

    public static string SideDamageImpact(PendingSideDamage pending, CombatSide side)
    {
        var targets = side == CombatSide.Player
            ? L("incident.target.players", "all players")
            : L("incident.target.enemies", "all enemies");
        return pending.Kind switch
        {
            IncidentKind.SwordRain =>
                F("incident.impact.sword_rain",
                    "Sword Rain falls! {0} are about to take {1} damage × {2}. Block can prevent it.",
                    targets, pending.Damage, pending.Hits),
            IncidentKind.HiveOnslaught =>
                F("incident.impact.hive_onslaught",
                    "Hive wave {1}/{2} strikes! {0} are about to take {3} damage. Block can prevent it.",
                    targets, pending.Wave, pending.Duration, pending.Damage),
            _ => F("incident.impact.default",
                "{0} are about to take {1} damage × {2}. Block can prevent it.",
                targets, pending.Damage, pending.Hits),
        };
    }

    public static string IncidentTitle(IncidentKind kind) =>
        F("toast.title.incident", "Battlefield Event · {0}", Name(kind));

    public static string OmenTitle(int turn) =>
        F("toast.title.omen", "Battle Omen · Turn {0}", turn);

    public static Texture2D Icon(IncidentKind kind) => kind switch
    {
        IncidentKind.Rockfall => ModelDb.Power<RollingBoulderPower>().Icon,
        IncidentKind.SwordRain => ModelDb.Power<FanOfKnivesPower>().Icon,
        IncidentKind.ToxicFog => ModelDb.Power<PoisonPower>().Icon,
        IncidentKind.VineSnare => ModelDb.Power<VulnerablePower>().Icon,
        IncidentKind.DampSeaWind => ModelDb.Power<WeakPower>().Icon,
        IncidentKind.HiveOnslaught => ModelDb.Power<PersonalHivePower>().Icon,
        IncidentKind.GentleRain => ModelDb.Power<RegenPower>().Icon,
        _ => ModelDb.Power<VulnerablePower>().Icon,
    };

    private static string PowerName<T>() where T : PowerModel =>
        ModelDb.Power<T>().Title.GetFormattedText();

    private static string L(string key, string englishFallback) =>
        ModLocalization.Get(key, englishFallback);

    private static string F(string key, string englishFallback, params object?[] arguments) =>
        ModLocalization.Format(key, englishFallback, arguments);
}
