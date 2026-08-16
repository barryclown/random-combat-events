using BattlefieldIncidents.Scheduling;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Nonupeipe's third gift: a fight somewhere ahead of you where something will be waiting on its
///     last point of health. The room is marked on the map with the game's own quest pin, the same one
///     the Fur Coat uses, so the gift is a route to plan around rather than a surprise.
///     <para>
///     🔴 The mark lives for this session only. The game saves quest pins through the relic that placed
///     them, and this mod has no relic to hang them on; quitting to the menu and loading the run back in
///     drops the mark. Losing an unclaimed gift is the harmless direction to fail in, and it is said out
///     loud in the settings rather than left for someone to discover.
///     </para>
/// </summary>
internal static class MarkedRooms
{
    private static readonly HashSet<(int Col, int Row)> Marks = [];
    private static string _ownerKey = string.Empty;

    /// <summary>
    ///     Marks one unvisited fight ahead of the party. Returns false when the act has nothing left to
    ///     mark, which is a real outcome near the end of a map rather than a failure.
    /// </summary>
    public static bool Mark(Player? player, ulong seed)
    {
        try
        {
            if (player == null)
                return false;

            var runState = player.RunState;
            var map = runState?.Map;
            if (runState == null || map == null)
                return false;

            SyncOwner(player);

            var currentRow = runState.CurrentMapCoord?.row ?? -1;
            var candidates = map.GetAllMapPoints()
                .Where(point => IsFight(point.PointType))
                .Where(point => point.coord.row > currentRow)
                .Where(point => !Marks.Contains((point.coord.col, point.coord.row)))
                .OrderBy(point => point.coord.row)
                .ThenBy(point => point.coord.col)
                .ToList();
            if (candidates.Count == 0)
                return false;

            var chosen = candidates[PioneerRoll.MarkedRoomIndex(seed, candidates.Count)];
            Marks.Add((chosen.coord.col, chosen.coord.row));
            AddPin(chosen);
            MainFile.Logger.Info(
                $"Nonupeipe marked the fight at ({chosen.coord.col},{chosen.coord.row}).");
            return true;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not mark a room for Nonupeipe. {exception}");
            return false;
        }
    }

    /// <summary>
    ///     Whether the fight about to start is a marked one, spending the mark if so. Asked once, when
    ///     combat opens.
    /// </summary>
    public static bool TryClaim(Player? player)
    {
        try
        {
            if (player?.RunState?.CurrentMapCoord is not { } coord)
                return false;

            SyncOwner(player);
            return Marks.Remove((coord.col, coord.row));
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not check this room's mark. {exception}");
            return false;
        }
    }

    /// <summary>
    ///     Marks belong to one act of one run. Anything else — a new run, the next act, a different save
    ///     loaded over the top — starts with a clean map rather than inheriting somebody else's pins.
    /// </summary>
    private static void SyncOwner(Player player)
    {
        var runState = player.RunState;
        var key = $"{runState?.Rng.Seed}:{runState?.CurrentActIndex}";
        if (key == _ownerKey)
            return;

        _ownerKey = key;
        Marks.Clear();
    }

    private static void AddPin(MapPoint point)
    {
        try
        {
            var courier = ModelDb.GetById<SpoilsCourier>(ModelDb.GetId<SpoilsCourier>());
            if (courier != null)
                point.AddQuest(courier);
        }
        catch (Exception exception)
        {
            // The pin is decoration; the mark itself is what matters, so a missing pin is worth a line
            // in the log and nothing more.
            MainFile.Logger.Warn($"Could not pin the marked room on the map. {exception}");
        }
    }

    private static bool IsFight(MapPointType type) =>
        type is MapPointType.Monster or MapPointType.Elite;
}
