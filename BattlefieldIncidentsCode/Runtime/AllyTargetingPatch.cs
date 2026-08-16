using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Points a summoned ally's attacks at the enemy instead of at the player who hired it.
///     <para>
///     A monster's move takes a target list, but almost none of them use it — they build the attack with
///     <c>DamageCmd.Attack(...).FromMonster(this)</c>, and that builder hard-codes
///     <c>PlayerCreatures</c> for anything coming from a monster. Handing the move a different list
///     achieves nothing, so the correction has to happen where the targets are actually chosen.
///     </para>
///     <para>
///     Only monsters standing on the player's side are touched, and only when the attack was already
///     aimed squarely at the player side, so an ordinary enemy attack and any single-target effect are
///     left exactly as the game built them.
///     </para>
/// </summary>
[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class AllyTargetingPatch
{
    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        try
        {
            var attacker = __instance.Attacker;
            if (attacker == null ||
                !attacker.IsMonster ||
                attacker.Side != CombatSide.Player ||
                attacker.CombatState is not CombatState combatState)
            {
                return;
            }

            if (__result.Count == 0 || !__result.All(target => target.IsPlayer))
                return;

            var opponents = combatState.GetOpponentsOf(attacker);
            if (opponents.Count > 0)
                __result = opponents;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Ally targeting correction failed; leaving the attack as-is. {exception}");
        }
    }
}
