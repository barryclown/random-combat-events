namespace BattlefieldIncidents.Scheduling;

/// <summary>
///     The rolls the Ancients' events need, all derived from the combat seed rather than taken live, so
///     that reloading a save cannot shop for a better gift.
/// </summary>
public static class PioneerRoll
{
    private const ulong GiftSalt = 0x4F6C_DD1D_2545_F491UL;
    private const ulong RoomSalt = 0xDA1C_E2A9_B5AD_4ECEUL;

    /// <summary>
    ///     Which of Nonupeipe's three gifts this roll landed on. The three weights are configurable and
    ///     may be zeroed individually; if every one is off the caller is told there is nothing to give
    ///     rather than being handed a silent default.
    /// </summary>
    public static NonupeipeGift? Gift(ulong seed, int maxHpWeight, int goldWeight, int markedRoomWeight)
    {
        var maxHp = Math.Max(0, maxHpWeight);
        var gold = Math.Max(0, goldWeight);
        var marked = Math.Max(0, markedRoomWeight);
        var total = maxHp + gold + marked;
        if (total <= 0)
            return null;

        var roll = new DeterministicRandom(seed ^ GiftSalt).NextInt(0, total);
        if (roll < maxHp)
            return NonupeipeGift.MaxHp;

        return roll < maxHp + gold ? NonupeipeGift.GoldAfterCombat : NonupeipeGift.MarkedRoom;
    }

    /// <summary>
    ///     Picks which of the rooms still ahead of the party gets marked. Kept here rather than in the
    ///     runtime so the choice is reproducible in a test without a map.
    /// </summary>
    public static int MarkedRoomIndex(ulong seed, int candidateCount) =>
        candidateCount <= 0 ? -1 : new DeterministicRandom(seed ^ RoomSalt).NextInt(0, candidateCount);
}
