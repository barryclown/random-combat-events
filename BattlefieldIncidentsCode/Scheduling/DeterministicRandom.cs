namespace BattlefieldIncidents.Scheduling;

/// <summary>
/// Small cross-runtime deterministic generator. It never consumes the game's RNG streams.
/// </summary>
public struct DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
    {
        _state = seed;
    }

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        if (minimumInclusive >= maximumExclusive)
            throw new ArgumentOutOfRangeException(nameof(minimumInclusive),
                "Minimum must be lower than maximum.");

        var range = (ulong)(maximumExclusive - minimumInclusive);
        var rejectionThreshold = unchecked(0UL - range) % range;
        ulong value;
        do
        {
            value = NextUInt64();
        } while (value < rejectionThreshold);

        return minimumInclusive + (int)(value % range);
    }
}
