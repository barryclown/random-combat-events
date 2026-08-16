namespace BattlefieldIncidents.Scheduling;

/// <summary>
///     How a table of players settles one of this mod's questions. Majority carries it; a tie is broken
///     by the combat seed rather than by whoever clicked first, so every client reaches the same answer
///     without anyone having to be the authority on it.
/// </summary>
public static class VoteTally
{
    private const ulong TieBreakSalt = 0x9E37_79B9_7F4A_7C15UL;

    /// <summary>
    ///     Resolves a set of ballots. Ballots are option indexes; a negative entry means that player did
    ///     not answer and is simply not counted. Returns -1 when nobody voted at all.
    /// </summary>
    /// <param name="seed">
    ///     Derived from the combat route, so a tie resolves the same way on every machine and the same
    ///     way again after a reload.
    /// </param>
    public static int Resolve(IReadOnlyList<int> ballots, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(ballots);

        var counts = new Dictionary<int, int>();
        foreach (var ballot in ballots.Where(ballot => ballot >= 0))
            counts[ballot] = counts.GetValueOrDefault(ballot) + 1;

        if (counts.Count == 0)
            return -1;

        var best = counts.Values.Max();

        // Ordered before the draw so the list of contenders is identical on every machine; a dictionary's
        // own order is not something to bet a synchronised game on.
        var tied = counts
            .Where(entry => entry.Value == best)
            .Select(entry => entry.Key)
            .OrderBy(option => option)
            .ToList();

        return tied.Count == 1
            ? tied[0]
            : tied[new DeterministicRandom(seed ^ TieBreakSalt).NextInt(0, tied.Count)];
    }
}
