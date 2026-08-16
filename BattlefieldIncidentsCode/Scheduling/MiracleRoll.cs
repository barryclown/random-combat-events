namespace BattlefieldIncidents.Scheduling;

/// <summary>
///     Decides which units carry a Last Miracle, once per combat and up front.
///     Rolling here instead of at the moment of death buys two things. Reloading a save cannot reroll
///     it, which is the same promise the turn route makes. And the game asks <c>ShouldDie</c> in places
///     that are only previewing a death rather than resolving one, so the answer has to be a lookup
///     with no dice in it.
/// </summary>
public static class MiracleRoll
{
    private const ulong Salt = 0x5D1F_9C3B_A70E_4821UL;
    private const ulong Stride = 0x9E3779B97F4A7C15UL;

    /// <param name="combatSeed">The combat's route seed, so the miracle shares its stability.</param>
    /// <param name="unitIndex">Position of the unit in the combat's creature list.</param>
    /// <param name="chancePermille">Chance in tenths of a percent, so 5 means 0.5%.</param>
    public static bool IsGranted(ulong combatSeed, int unitIndex, int chancePermille)
    {
        if (chancePermille <= 0)
            return false;
        if (chancePermille >= 1000)
            return true;

        var random = new DeterministicRandom(
            combatSeed ^ Salt ^ (((ulong)(uint)unitIndex + 1) * Stride));
        return random.NextInt(0, 1000) < chancePermille;
    }
}
