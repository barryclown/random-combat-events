namespace BattlefieldIncidents.Scheduling;

/// <summary>What a mercenary does once you have agreed to pay it.</summary>
public enum MercenaryOutcome
{
    Helps,

    /// <summary>Takes the gold and is never seen again. The gold is still spent.</summary>
    RunsOff,

    /// <summary>Decides anyone with that much gold is worth robbing, and joins the other side.</summary>
    TurnsHostile,
}

/// <summary>What a monster being courted away from the enemy side does.</summary>
public enum RecruitOutcome
{
    /// <summary>Stays out of the fight entirely.</summary>
    StandsDown,

    /// <summary>Fights for you.</summary>
    Helps,

    /// <summary>Takes the gold and leaves without helping.</summary>
    RunsOff,

    /// <summary>Joins the enemy side after all.</summary>
    JoinsEnemies,
}

public enum ChallengeOutcome
{
    /// <summary>Joins the enemy side. Beating it pays out.</summary>
    Joins,

    /// <summary>Backs down and leaves its things behind.</summary>
    FleesLeavingSpoils,

    /// <summary>Says its piece and goes.</summary>
    Leaves,
}

/// <summary>
///     Every roll a summon event needs, derived from the combat seed rather than taken live. The route
///     already promises that reloading rerolls nothing, and a priced offer has to promise the same: a
///     player who could reload past a bad outcome, or reroll a quoted price, is playing a different game.
/// </summary>
public static class SummonRoll
{
    private const ulong OutcomeSalt = 0x7F4A_7C15_9E37_79B9UL;
    private const ulong PriceSalt = 0x2545_F491_4F6C_DD1DUL;
    private const ulong TargetSalt = 0xB5AD_4ECE_DA1C_E2A9UL;
    private const ulong DepartureSalt = 0x1D8E_4B27_F0C5_A63FUL;

    /// <summary>How far a quoted price may drift, matching the shop's own ±5% on potions.</summary>
    public const int PriceVariancePercent = 5;

    public static MercenaryOutcome RollMercenary(ulong seed, int betrayalPercent, int runOffPercent)
    {
        var roll = Percent(seed, OutcomeSalt);
        if (roll < runOffPercent)
            return MercenaryOutcome.RunsOff;
        if (roll < runOffPercent + betrayalPercent)
            return MercenaryOutcome.TurnsHostile;

        return MercenaryOutcome.Helps;
    }

    public static RecruitOutcome RollStandDown(ulong seed, int failurePercent) =>
        Percent(seed, OutcomeSalt) < failurePercent
            ? RecruitOutcome.JoinsEnemies
            : RecruitOutcome.StandsDown;

    public static RecruitOutcome RollHire(ulong seed, int failurePercent) =>
        Percent(seed, OutcomeSalt) < failurePercent
            ? RecruitOutcome.RunsOff
            : RecruitOutcome.Helps;

    public static ChallengeOutcome RollChallengeAccepted(ulong seed, int upsetPercent) =>
        Percent(seed, OutcomeSalt) < upsetPercent
            ? ChallengeOutcome.FleesLeavingSpoils
            : ChallengeOutcome.Joins;

    public static ChallengeOutcome RollChallengeDeclined(ulong seed, int upsetPercent) =>
        Percent(seed, OutcomeSalt) < upsetPercent
            ? ChallengeOutcome.Joins
            : ChallengeOutcome.Leaves;

    /// <summary>
    ///     A quoted price, drifted by up to ±5% the way the shop drifts a potion. Rolled from the combat
    ///     seed so the number the player is shown is the number they are charged, however many times the
    ///     save is reloaded.
    /// </summary>
    public static int Price(int basePrice, ulong seed)
    {
        if (basePrice <= 0)
            return 0;

        var span = PriceVariancePercent * 2 + 1;
        var offset = new DeterministicRandom(seed ^ PriceSalt).NextInt(0, span) - PriceVariancePercent;
        var drifted = (int)Math.Round(basePrice * (100m + offset) / 100m, MidpointRounding.AwayFromZero);
        return Math.Max(1, drifted);
    }

    /// <summary>
    ///     Whether a summoned monster wanders off this turn. Keyed by the unit and the turn, so the same
    ///     turn always gives the same answer however often the save is reloaded.
    /// </summary>
    public static bool LeavesThisTurn(ulong combatSeed, int unitId, int round, int chancePercent)
    {
        if (chancePercent <= 0)
            return false;
        if (chancePercent >= 100)
            return true;

        var seed = combatSeed ^ DepartureSalt ^ ((ulong)(uint)unitId * 0x9E3779B97F4A7C15UL)
                   ^ ((ulong)(uint)round * 0xC2B2AE3D27D4EB4FUL);
        return new DeterministicRandom(seed).NextInt(0, 100) < chancePercent;
    }

    /// <summary>Picks one entry from a pool without touching the game's own RNG streams.</summary>
    public static int PickIndex(int count, ulong seed)
    {
        if (count <= 0)
            return -1;

        return new DeterministicRandom(seed ^ TargetSalt).NextInt(0, count);
    }

    private static int Percent(ulong seed, ulong salt) =>
        new DeterministicRandom(seed ^ salt).NextInt(0, 100);
}
