using BattlefieldIncidents.Scheduling;

namespace BattlefieldIncidents.Settings;

public enum IncidentPreset
{
    Casual,
    Standard,
    Chaos,
    Custom,
}

public sealed class IncidentSettings
{
    public bool Enabled { get; set; } = true;
    public IncidentPreset Preset { get; set; } = IncidentPreset.Standard;
    public int MinimumCheckpointGap { get; set; } = 3;
    public int MaximumCheckpointGap { get; set; } = 5;
    public int IncidentChancePercent { get; set; } = 50;
    public int MaximumScheduledTurn { get; set; } = 100;
    public int WarningTurns { get; set; } = 1;
    public bool AllowOverlap { get; set; } = true;

    public bool EnableNormalCombats { get; set; } = true;
    public bool EnableEliteCombats { get; set; } = true;
    public bool EnableBossCombats { get; set; } = true;

    public bool EnableRockfall { get; set; } = true;
    public int RockfallWeight { get; set; } = 10;
    public int RockfallDamage { get; set; } = 5;

    public bool EnableSwordRain { get; set; } = true;
    public int SwordRainWeight { get; set; } = 10;
    public int SwordRainDamagePerHit { get; set; } = 1;
    public int SwordRainHitCount { get; set; } = 3;

    public bool EnableToxicFog { get; set; } = true;
    public int ToxicFogWeight { get; set; } = 8;
    public int ToxicFogPoisonPerHit { get; set; } = 1;

    public bool EnableGentleRain { get; set; } = true;
    public int GentleRainWeight { get; set; } = 14;
    public int GentleRainHealPercent { get; set; } = 3;

    public bool EnableVineSnare { get; set; } = true;
    public int VineSnareWeight { get; set; } = 12;
    public int VineSnareVulnerable { get; set; } = 1;

    public bool EnableDampSeaWind { get; set; } = true;
    public int DampSeaWindWeight { get; set; } = 12;
    public int DampSeaWindWeak { get; set; } = 1;
    public int DampSeaWindFrail { get; set; } = 1;

    public bool EnableHiveOnslaught { get; set; } = true;
    public int HiveOnslaughtWeight { get; set; } = 12;
    public int HiveOnslaughtDamage { get; set; } = 5;
    public int HiveOnslaughtDuration { get; set; } = 3;

    public void ApplyPreset(IncidentPreset preset)
    {
        Preset = preset;
        switch (preset)
        {
            case IncidentPreset.Casual:
                MinimumCheckpointGap = 5;
                MaximumCheckpointGap = 7;
                IncidentChancePercent = 35;
                WarningTurns = 2;
                break;
            case IncidentPreset.Standard:
                MinimumCheckpointGap = 3;
                MaximumCheckpointGap = 5;
                IncidentChancePercent = 50;
                WarningTurns = 1;
                break;
            case IncidentPreset.Chaos:
                MinimumCheckpointGap = 1;
                MaximumCheckpointGap = 3;
                IncidentChancePercent = 80;
                WarningTurns = 1;
                break;
            case IncidentPreset.Custom:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
        }
    }

    public TimelineGenerationOptions ToTimelineOptions(ActTheme actTheme)
    {
        var minimumGap = Math.Clamp(MinimumCheckpointGap, 1, 100);
        var maximumGap = Math.Clamp(MaximumCheckpointGap, minimumGap, 100);
        var maximumTurn = Math.Clamp(MaximumScheduledTurn, 1, 100);

        return new TimelineGenerationOptions(
            minimumGap,
            maximumGap,
            Math.Clamp(IncidentChancePercent, 0, 100),
            maximumTurn,
            actTheme,
            AllowOverlap,
            BuildIncidentWeights(actTheme));
    }

    private IReadOnlyList<WeightedIncident> BuildIncidentWeights(ActTheme actTheme)
    {
        var incidents = new List<WeightedIncident>();
        Add(EnableRockfall, IncidentKind.Rockfall, RockfallWeight, duration: 1);
        Add(EnableSwordRain, IncidentKind.SwordRain, SwordRainWeight, duration: 1);
        Add(EnableToxicFog, IncidentKind.ToxicFog, ToxicFogWeight, duration: 1);
        Add(EnableGentleRain, IncidentKind.GentleRain, GentleRainWeight, duration: 1);

        switch (actTheme)
        {
            case ActTheme.Overgrowth:
                Add(EnableVineSnare, IncidentKind.VineSnare, VineSnareWeight, duration: 1);
                break;
            case ActTheme.Underdocks:
                Add(EnableDampSeaWind, IncidentKind.DampSeaWind, DampSeaWindWeight, duration: 1);
                break;
            case ActTheme.Hive:
                Add(EnableHiveOnslaught, IncidentKind.HiveOnslaught, HiveOnslaughtWeight,
                    Math.Clamp(HiveOnslaughtDuration, 1, 10));
                break;
        }

        return incidents;

        void Add(bool enabled, IncidentKind kind, int weight, int duration)
        {
            if (enabled && weight > 0)
                incidents.Add(new WeightedIncident(kind, Math.Clamp(weight, 1, 100), duration));
        }
    }
}
