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
    NeowsBlessing,
    ArchitectsCurse,
    Laser,
    FreeSummon,
    Mercenary,
    EnemyRecruit,
    Challenge,

    /// <summary>
    ///     A standing rule for the whole combat rather than a stop on the turn route. It is granted at
    ///     combat start and never enters the weighted pool, but it still needs a name, an icon and a
    ///     toast title, which is what this enum supplies.
    /// </summary>
    LastMiracle,

    // The seven remaining Ancients, each one an event of its own. Appended rather than grouped beside
    // Neow's Blessing so the order the pool is built in does not shift for anything already in it.

    /// <summary>Vakuu takes the next turn off your hands: the whole hand is played, then burned.</summary>
    VakuusTakeover,

    /// <summary>Darv trades order for volume — a full hand next turn, and no idea what it will cost.</summary>
    DarvsGamble,

    /// <summary>Nonupeipe gives one of three gifts: max HP, gold after the fight, or a marked room.</summary>
    NonupeipesGift,

    /// <summary>Tanx hands over a weapon for this turn only.</summary>
    TanxsArmory,

    /// <summary>Tezcatara's wax relic: yours for the fight, and melted by the end of it.</summary>
    TezcatarasEmber,

    /// <summary>Pael's small mercy — an extra point of energy and a card, next turn.</summary>
    PaelsBlessing,

    /// <summary>Orobas offers three cards from outside your own discipline, free for this turn.</summary>
    OrobassOffer,

    /// <summary>
    ///     Co-op only. One player opens the fight wrapped in vines and cannot play cards until an ally
    ///     cuts them loose. Rolled at combat start rather than scheduled on the route, so it never
    ///     enters the weighted pool.
    /// </summary>
    StranglingVines,
}

/// <summary>Which of Nonupeipe's three gifts a single roll landed on.</summary>
public enum NonupeipeGift
{
    MaxHp,
    GoldAfterCombat,
    MarkedRoom,
}

public sealed record WeightedIncident(IncidentKind Kind, int Weight, int Duration);

public sealed record TimelineGenerationOptions(
    int MinimumCheckpointGap,
    int MaximumCheckpointGap,
    int IncidentChancePercent,
    int MaximumTurn,
    ActTheme ActTheme,
    bool AllowOverlap,
    IReadOnlyList<WeightedIncident> Incidents,
    /// <summary>
    ///     The earliest turn an incident may occupy. An incident on turn 1 cannot be announced a turn
    ///     ahead, so scheduling one there would break the mod's whole premise that every incident is
    ///     telegraphed before it lands.
    /// </summary>
    int FirstTelegraphableTurn = 1);

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
