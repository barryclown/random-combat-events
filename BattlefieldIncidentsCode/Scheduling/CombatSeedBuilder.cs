using System.Text;

namespace BattlefieldIncidents.Scheduling;

public static class CombatSeedBuilder
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Build(
        ulong runSeed,
        int actIndex,
        int actFloor,
        int totalFloor,
        int mapColumn,
        int mapRow,
        string encounterId)
    {
        var hash = OffsetBasis;
        Mix(ref hash, runSeed);
        Mix(ref hash, unchecked((ulong)actIndex));
        Mix(ref hash, unchecked((ulong)actFloor));
        Mix(ref hash, unchecked((ulong)totalFloor));
        Mix(ref hash, unchecked((ulong)mapColumn));
        Mix(ref hash, unchecked((ulong)mapRow));
        foreach (var value in Encoding.UTF8.GetBytes(encounterId ?? string.Empty))
        {
            hash ^= value;
            hash *= Prime;
        }

        Mix(ref hash, 0x4246495F56315F31UL); // "BFI_V1_1"
        return hash;
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= Prime;
        }
    }
}
