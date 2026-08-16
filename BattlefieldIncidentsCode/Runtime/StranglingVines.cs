using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     The co-op event: one player opens the fight held fast and cannot play a card until somebody else
///     cuts them loose. It exists to make a table look at each other — everything else in this mod lands
///     on the whole party at once, and none of it asks anyone to spend a turn on somebody else's problem.
///     <para>
///     Three rules keep it from being a punishment. The vines have a single point of health, so freeing
///     someone costs the smallest attack in the game rather than a turn of setup. Potions still work, so
///     the trapped player is not reduced to watching. And the vines let go on their own after a few
///     turns, so a party that cannot reach them — or has nobody left standing who could — is never stuck.
///     </para>
/// </summary>
internal static class StranglingVines
{
    /// <summary>Monsters that can pass for a tangle of vines, best first.</summary>
    private static readonly string[] PreferredMonsters = ["VineShambler", "SlitheringStrangler"];

    private const ulong VictimSalt = 0x27D4_EB4F_C2B2_AE3DUL;
    private const ulong ChanceSalt = 0xE2A9_B5AD_4ECE_DA1CUL;

    /// <summary>
    ///     Rolls the vines at combat start and, if they land, puts them on the field. Returns the line to
    ///     show, or null when nothing happened.
    /// </summary>
    public static async Task<string?> ResolveAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        ulong seed)
    {
        if (runtime.VinesResolved)
            return null;

        runtime.VinesResolved = true;

        // Single-player has nobody to do the cutting, so this simply is not that mode's event.
        if (!settings.EnableStranglingVines || !Party.IsCoop)
            return null;

        var members = Party.Members(combatState).Where(player => player.Creature.IsAlive).ToList();
        if (members.Count < 2)
            return null;

        if (new DeterministicRandom(seed ^ ChanceSalt).NextInt(0, 100) >= settings.StranglingVinesChancePercent)
            return null;

        var monster = FindMonster(combatState);
        if (monster == null)
        {
            MainFile.Logger.Warn("Strangling Vines rolled, but no monster could stand in for the vines.");
            return null;
        }

        var victim = members[SummonRoll.PickIndex(members.Count, seed ^ VictimSalt)];
        var creature = await AllyController.SpawnAsync(combatState, monster, CombatSide.Enemy);
        if (creature == null)
            return null;

        // One point of health is the whole design: any attack at all frees them, so the cost of helping
        // is a card, not a turn.
        await CreatureCmd.SetCurrentHp(creature, 1m);

        runtime.Vines = new StranglingVinesState
        {
            Vines = creature,
            Victim = victim,
            ReleaseTurn = combatState.RoundNumber + Math.Max(1, settings.StranglingVinesEscapeTurns),
        };

        MainFile.Logger.Info(
            $"Strangling Vines took hold of {victim.NetId}; they let go on turn {runtime.Vines.ReleaseTurn}.");
        return IncidentText.VinesCaught(victim.Creature.Name, creature.Name,
            settings.StranglingVinesEscapeTurns);
    }

    /// <summary>
    ///     Whether this card is one the vines are holding down. Consulted through the game's own play
    ///     gate, so a held player sees their cards greyed out with everything else that cannot be played
    ///     rather than clicking one and watching nothing happen.
    /// </summary>
    public static bool Holds(CombatIncidentState runtime, CardModel card)
    {
        var vines = runtime.Vines;
        if (vines is null or { Released: true })
            return false;

        return card.Owner == vines.Victim;
    }

    /// <summary>
    ///     Checks whether the vines have been cut or have simply given up, and reports it once. Called at
    ///     the top of every turn.
    /// </summary>
    public static string? Release(CombatState combatState, CombatIncidentState runtime, int round)
    {
        var vines = runtime.Vines;
        if (vines == null || vines.Released)
            return null;

        var cut = !vines.Vines.IsAlive || !combatState.Creatures.Contains(vines.Vines);
        var timedOut = round >= vines.ReleaseTurn;
        if (!cut && !timedOut)
            return null;

        vines.Released = true;
        vines.Notice?.Close();
        vines.Notice = null;

        MainFile.Logger.Info($"Strangling Vines released {vines.Victim.NetId} ({(cut ? "cut" : "timed out")}).");
        return cut
            ? IncidentText.VinesCut(vines.Victim.Creature.Name)
            : IncidentText.VinesWithered(vines.Victim.Creature.Name);
    }

    private static MonsterModel? FindMonster(CombatState combatState)
    {
        try
        {
            foreach (var name in PreferredMonsters)
            {
                var match = ModelDb.Monsters.FirstOrDefault(monster =>
                    string.Equals(monster.GetType().Name, name, StringComparison.Ordinal));
                if (match is { IsMock: false })
                    return match;
            }
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not look up a vine monster by name. {exception}");
        }

        // Whatever the act has that is small. Not vines, but held is held.
        var pool = MonsterPools.For(combatState.RunState.Act, SummonTier.Weak);
        return pool.Count > 0 ? pool[0] : null;
    }
}
