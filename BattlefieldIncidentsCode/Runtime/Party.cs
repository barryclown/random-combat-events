using BattlefieldIncidents.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Everyone sitting at the table, and what a shared decision costs each of them.
///     <para>
///     Every event this mod runs is the same event for the whole party — the route is derived from the
///     run seed, which all peers share, so each machine plans an identical fight without anyone having
///     to send anything. What does need care is the settling: an effect written against "the player"
///     would land on whichever one the game happened to hand our hook first, and the other seats would
///     watch a gift go past them.
///     </para>
/// </summary>
internal static class Party
{
    /// <summary>
    ///     Whether this run has more than one seat. Read from the net service rather than the player
    ///     count so that a co-op run someone has dropped out of still behaves like co-op.
    /// </summary>
    public static bool IsCoop
    {
        get
        {
            try
            {
                var type = RunManager.Instance?.NetService?.Type;
                return type is not null and not NetGameType.Singleplayer;
            }
            catch (Exception exception)
            {
                MainFile.Logger.Warn($"Could not read the net service; assuming single player. {exception}");
                return false;
            }
        }
    }

    /// <summary>
    ///     Every player in the fight, in the run's own order. That order is identical on every peer,
    ///     which is what lets us reserve choice IDs and break vote ties without talking to anyone.
    /// </summary>
    public static IReadOnlyList<Player> Members(CombatState combatState)
    {
        try
        {
            var players = combatState.RunState?.Players;
            if (players is { Count: > 0 })
                return players;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not read the run's players. {exception}");
        }

        // A fight always has at least the local player, even if the run state is not talking to us.
        return combatState.Creatures
            .Where(creature => creature.IsPlayer)
            .Select(creature => creature.Player)
            .OfType<Player>()
            .Distinct()
            .ToList();
    }

    /// <summary>
    ///     What one player pays towards a quoted price. Single-player runs pay the whole thing; a co-op
    ///     table splits it by a fixed share rather than by head count, so that a pair is not priced out
    ///     of everything a full party can afford.
    /// </summary>
    public static int Share(int price, IncidentSettings settings)
    {
        if (price <= 0)
            return 0;
        if (!IsCoop)
            return price;

        var divisor = Math.Clamp(settings.MultiplayerPriceDivisor, 1, 8);
        return Math.Max(1, (int)Math.Ceiling(price / (decimal)divisor));
    }

    /// <summary>
    ///     Whether every seat can cover the amount asked of it. Takes the per-player share, not the
    ///     headline price — a part-paid contract is nobody's idea of a deal.
    /// </summary>
    public static bool CanAllPay(CombatState combatState, int share) =>
        share <= 0 || Members(combatState).All(player => player.Gold >= share);
}
