namespace BattlefieldIncidents.Scheduling;

public enum ActTheme
{
    Unknown,
    Overgrowth,
    Underdocks,
    Hive,
    Glory,
}

public enum IncidentKind
{
    Rockfall,
    SwordRain,
    ToxicFog,
    VineSnare,
    DampSeaWind,
    HiveOnslaught,
    GentleRain,
}

public sealed record WeightedIncident(IncidentKind Kind, int Weight, int Duration);

public sealed record TimelineGenerationOptions(
    int MinimumCheckpointGap,
    int MaximumCheckpointGap,
    int IncidentChancePercent,
    int MaximumTurn,
    ActTheme ActTheme,
    bool AllowOverlap,
    IReadOnlyList<WeightedIncident> Incidents);

public sealed record ScheduledCheckpoint(
    int Turn,
    IncidentKind? Incident,
    int Duration,
    int Variant,
    ulong EffectSeed)
{
    public bool HasIncident => Incident.HasValue;
}

public sealed record IncidentTimeline(ulong Seed, IReadOnlyList<ScheduledCheckpoint> Checkpoints)
{
    public IEnumerable<ScheduledCheckpoint> Incidents => Checkpoints.Where(checkpoint => checkpoint.HasIncident);
}
