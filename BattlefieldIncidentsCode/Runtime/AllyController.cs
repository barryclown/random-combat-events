using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Everything that makes a summoned monster behave like an ally instead of scenery.
///     <para>
///     The game will happily put a monster on the player's side, but nothing there drives it: the turn
///     loop only walks the enemy list, and a monster's move is handed <c>PlayerCreatures</c> to aim at.
///     Both are fixed from the outside — the move itself takes whatever target list it is given, so
///     driving it ourselves against the enemy list is enough. No patching involved.
///     </para>
/// </summary>
internal static class AllyController
{
    /// <summary>
    ///     Brings a monster into the fight on the given side. The command does the whole job: combat
    ///     state, turn bookkeeping, and the on-screen node.
    /// </summary>
    public static async Task<Creature?> SpawnAsync(
        CombatState combatState,
        MonsterModel canonicalMonster,
        CombatSide side)
    {
        try
        {
            var monster = canonicalMonster.ToMutable();
            var creature = await CreatureCmd.Add(monster, combatState, side, slotName: null);
            if (creature == null)
                return null;

            if (side == CombatSide.Player)
            {
                AimAtEnemies(creature, combatState);
                FaceEnemies(creature);
            }

            MainFile.Logger.Info($"Summoned {creature.Name} onto the {side} side.");
            return creature;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Summon failed for {canonicalMonster.GetType().Name}: {exception}");
            return null;
        }
    }

    /// <summary>
    ///     Rolls the ally's next move against the enemy list and refreshes its intent, so the player can
    ///     see what their hired help is about to do the same way they read an enemy.
    /// </summary>
    public static void AimAtEnemies(Creature ally, CombatState combatState)
    {
        var monster = ally.Monster;
        if (monster?.MoveStateMachine == null)
            return;

        var enemies = LivingEnemies(combatState);
        if (enemies.Count == 0)
            return;

        ally.PrepareForNextTurn(enemies, rollNewMove: true);
    }

    /// <summary>
    ///     Runs one ally's turn. This is the move the game would have run itself, handed the enemy list
    ///     instead of the player list.
    /// </summary>
    public static async Task PerformTurnAsync(Creature ally, CombatState combatState)
    {
        try
        {
            var monster = ally.Monster;
            if (!ally.IsAlive || monster == null || monster.SpawnedThisTurn)
                return;

            var enemies = LivingEnemies(combatState);
            if (enemies.Count == 0)
                return;

            var move = monster.NextMove;
            await move.PerformMove(enemies);
            monster.MoveStateMachine?.OnMovePerformed(move);
            CombatManager.Instance.History.MonsterPerformedMove(combatState, monster, move, enemies);

            if (ally.IsAlive)
            {
                AimAtEnemies(ally, combatState);
                // The game's own animations reset the visual scale, so the flip is reapplied each turn.
                FaceEnemies(ally);
            }
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Ally turn failed for {ally.Name}: {exception}");
        }
    }

    /// <summary>
    ///     Picks the ally that steps in front of a blow aimed at the player, following the game's own
    ///     <c>DieForYouPower</c>: only real attacks are intercepted, and only while the ally is standing.
    ///     Whatever overkills past the ally spills back onto the player, which the damage command already
    ///     handles once the target has been swapped.
    /// </summary>
    public static Creature? FindBodyguard(
        IReadOnlyCollection<Creature> allies,
        Creature target,
        ValueProp props,
        Creature? dealer)
    {
        if (allies.Count == 0 || !target.IsPlayer || !props.IsPoweredAttack())
            return null;

        // Never step in front of a blow one of our own threw. An ally's attack is aimed at the player
        // side by default, and catching it here turned the ally into the only thing it ever hit.
        if (dealer != null && dealer.Side == CombatSide.Player)
            return null;

        return allies.FirstOrDefault(ally => ally.IsAlive && ally != dealer &&
                                             ally.CombatState == target.CombatState);
    }

    /// <summary>
    ///     Turns a summoned ally around. Monster art is drawn facing left, towards where the player
    ///     stands, so an ally left alone looks like it is squaring up against the person who hired it.
    /// </summary>
    public static void FaceEnemies(Creature ally)
    {
        try
        {
            var visuals = ally.GetCreatureNode()?.Visuals;
            if (visuals == null)
                return;

            var scale = visuals.Scale;
            visuals.Scale = new Godot.Vector2(-Math.Abs(scale.X), scale.Y);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not turn {ally.Name} around: {exception.Message}");
        }
    }

    private static List<Creature> LivingEnemies(CombatState combatState) =>
        combatState.Enemies.Where(enemy => enemy.IsAlive && enemy.IsHittable).ToList();
}
