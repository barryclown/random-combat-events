namespace BattlefieldIncidents.Scheduling;

public enum SummonTier
{
    /// <summary>Small fry. The only thing a free summon is ever allowed to bring in.</summary>
    Weak,

    /// <summary>Anything that shows up in an ordinary combat encounter.</summary>
    Normal,

    /// <summary>Pulled from Elite encounters. Worth a relic when you beat it.</summary>
    Elite,
}

/// <summary>
///     Which monsters may be summoned, and on what terms. Kept free of game types so the rules can be
///     tested on their own; <see cref="Runtime.MonsterPools" /> is what turns them into real models.
/// </summary>
public static class SummonCatalog
{
    /// <summary>
    ///     A monster's moves are handed a target list, and these ones reach past the creature into the
    ///     player behind it — shuffling cards into a deck, taking gold, handing out potions. Point one of
    ///     them at an enemy and the move either throws or quietly does nothing, so they are barred from
    ///     both sides of a summon: useless as an ally, and as an enemy they would be summoned by us
    ///     without the encounter that gives them their context.
    /// </summary>
    public static readonly IReadOnlySet<string> PlayerDependentMonsters = new HashSet<string>(StringComparer.Ordinal)
    {
        "Aeonglass",
        "Chomper",
        "EyeWithTeeth",
        "GasBomb",
        "HauntedShip",
        "KnowledgeDemon",
        "LeafSlimeM",
        "LeafSlimeS",
        "LivingFog",
        "MechaKnight",
        "Myte",
        "Noisebot",
        "PhrogParasite",
        "SlimedBerserker",
        "SoulFysh",
        "TestSubject",
        "TheInsatiable",
        "ThievingHopper",
        "TwigSlimeM",
        "Vantom",
        "Wriggler",
    };

    /// <summary>
    ///     Highest max HP a monster may have and still count as small fry. Free summons draw from here,
    ///     so the ceiling doubles as the free event's power budget.
    /// </summary>
    public const int WeakHpCeiling = 25;

    public static bool IsUsableAsSummon(string monsterTypeName) =>
        !string.IsNullOrWhiteSpace(monsterTypeName) &&
        !PlayerDependentMonsters.Contains(monsterTypeName);

    public static SummonTier TierForHp(int maxInitialHp, bool fromEliteEncounter)
    {
        if (fromEliteEncounter)
            return SummonTier.Elite;

        return maxInitialHp <= WeakHpCeiling ? SummonTier.Weak : SummonTier.Normal;
    }
}
