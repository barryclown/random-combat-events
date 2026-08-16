using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     The four events that bring another monster to the fight. Each one names the monster before the
///     player commits, because the whole decision is "is that thing worth my gold" and a nameless
///     silhouette gives them nothing to decide with.
/// </summary>
internal static class SummonEvents
{
    /// <summary>Someone wanders in and picks a side. No choice, no cost, and always small fry.</summary>
    public static async Task<string?> ResolveFreeAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        ulong seed)
    {
        var monster = Pick(combatState.RunState.Act, SummonTier.Weak, seed);
        if (monster == null)
            return null;

        var joinsPlayer = SummonRoll.PickIndex(100, seed ^ 0x51) < settings.FreeSummonAllyPercent;
        var side = joinsPlayer ? CombatSide.Player : CombatSide.Enemy;
        var creature = await AllyController.SpawnAsync(combatState, monster, side);
        if (creature == null)
            return null;

        Register(runtime, creature, side, SummonTier.Weak);
        return IncidentText.FreeSummon(creature.Name, joinsPlayer);
    }

    /// <summary>
    ///     A monster offers its services. Paying is deferred a turn so the offer costs a decision now and
    ///     the gold later, which is also when it turns out whether it meant it.
    /// </summary>
    public static async Task<string?> ResolveMercenaryAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        Player player,
        int round,
        ulong seed)
    {
        var monster = Pick(player.RunState.Act, SummonTier.Normal, seed);
        if (monster == null)
            return null;

        var name = monster.Title.GetFormattedText();
        // Every figure the player sees, and is later charged, is the share they personally owe. A total
        // split four ways whatever the party size is a number nobody at the table actually pays.
        var price = Party.Share(SummonRoll.Price(settings.MercenaryPrice, seed), settings);
        if (!Party.CanAllPay(combatState, price))
            return IncidentText.MercenaryTooExpensive(name, price);

        var accepted = await IncidentDialog.AskPartyAsync(
            combatState, seed,
            IncidentText.IncidentTitle(IncidentKind.Mercenary),
            IncidentText.MercenaryOffer(name, price),
            IncidentText.DialogHire(price),
            IncidentText.DialogDecline());
        if (!accepted)
            return IncidentText.MercenaryDeclined(name);

        // Settled next turn: the contract is agreed now, the purse opens later.
        runtime.PendingMercenaries[round + 1] = new PendingMercenary(monster, name, price, seed);
        return IncidentText.MercenaryAccepted(name, price);
    }

    /// <summary>Pays out the contract agreed last turn and finds out what it bought.</summary>
    public static async Task<string?> SettleMercenaryAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        Player player,
        PendingMercenary pending)
    {
        // The gold goes either way. A deal that only costs you when it works out is not a decision.
        var charged = await ChargeEveryone(combatState, pending.Price);

        var outcome = SummonRoll.RollMercenary(
            pending.Seed ^ 0xA3, settings.MercenaryBetrayalPercent, settings.MercenaryRunOffPercent);
        if (outcome == MercenaryOutcome.RunsOff)
            return IncidentText.MercenaryRanOff(pending.Name, charged);

        var side = outcome == MercenaryOutcome.TurnsHostile ? CombatSide.Enemy : CombatSide.Player;
        var creature = await AllyController.SpawnAsync(combatState, pending.Monster, side);
        if (creature == null)
            return IncidentText.MercenaryRanOff(pending.Name, charged);

        Register(runtime, creature, side, SummonTier.Normal);
        return outcome == MercenaryOutcome.TurnsHostile
            ? IncidentText.MercenaryBetrayed(creature.Name, charged)
            : IncidentText.MercenaryHelps(creature.Name, charged);
    }

    /// <summary>
    ///     A monster is about to join the other side. Gold can buy it off, or buy it over — the second
    ///     costs more, because turning an enemy into an ally is worth more than making one walk away.
    /// </summary>
    public static async Task<string?> ResolveRecruitAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        Player player,
        ulong seed)
    {
        var monster = Pick(combatState.RunState.Act, SummonTier.Normal, seed);
        if (monster == null)
            return null;

        var name = monster.Title.GetFormattedText();
        var standDownPrice = Party.Share(SummonRoll.Price(settings.StandDownPrice, seed ^ 0x11), settings);
        var hirePrice = Party.Share(SummonRoll.Price(settings.HirePrice, seed ^ 0x22), settings);

        var choice = await IncidentDialog.AskThreeWayAsync(
            combatState, seed,
            IncidentText.IncidentTitle(IncidentKind.EnemyRecruit),
            IncidentText.RecruitOffer(name, standDownPrice, hirePrice),
            IncidentText.DialogNegotiate(),
            IncidentText.DialogIgnore(),
            IncidentText.RecruitFollowUp(name, standDownPrice, hirePrice),
            IncidentText.DialogStandDown(standDownPrice),
            IncidentText.DialogRecruit(hirePrice));

        var price = choice switch
        {
            1 => standDownPrice,
            2 => hirePrice,
            _ => 0,
        };
        if (!Party.CanAllPay(combatState, price))
        {
            choice = 0;
            price = 0;
        }

        if (price > 0)
            await ChargeEveryone(combatState, price);

        var outcome = choice switch
        {
            1 => SummonRoll.RollStandDown(seed ^ 0xB1, settings.RecruitFailurePercent),
            2 => SummonRoll.RollHire(seed ^ 0xB2, settings.RecruitFailurePercent),
            _ => RecruitOutcome.JoinsEnemies,
        };

        switch (outcome)
        {
            case RecruitOutcome.StandsDown:
                return IncidentText.RecruitStoodDown(name, price);
            case RecruitOutcome.RunsOff:
                return IncidentText.RecruitRanOff(name, price);
            default:
                var side = outcome == RecruitOutcome.Helps ? CombatSide.Player : CombatSide.Enemy;
                var creature = await AllyController.SpawnAsync(combatState, monster, side);
                if (creature == null)
                    return IncidentText.RecruitRanOff(name, price);

                Register(runtime, creature, side, SummonTier.Normal);
                return outcome == RecruitOutcome.Helps
                    ? IncidentText.RecruitHelps(creature.Name, price)
                    : IncidentText.RecruitJoinedEnemies(creature.Name, price);
        }
    }

    /// <summary>
    ///     Something wants in on the fight. Free to refuse, and worth spoils if you take it on — the one
    ///     event here where the player is buying risk rather than selling it.
    /// </summary>
    public static async Task<string?> ResolveChallengeAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        ulong seed)
    {
        var wantsElite = SummonRoll.PickIndex(100, seed ^ 0x71) < settings.ChallengeElitePercent;
        var tier = wantsElite ? SummonTier.Elite : SummonTier.Normal;
        var act = combatState.RunState.Act;
        var monster = Pick(act, tier, seed) ?? Pick(act, SummonTier.Normal, seed);
        if (monster == null)
            return null;

        var name = monster.Title.GetFormattedText();
        var accepted = await IncidentDialog.AskPartyAsync(
            combatState, seed,
            IncidentText.IncidentTitle(IncidentKind.Challenge),
            IncidentText.ChallengeOffer(name, tier == SummonTier.Elite),
            IncidentText.DialogAccept(),
            IncidentText.DialogRefuse());

        var outcome = accepted
            ? SummonRoll.RollChallengeAccepted(seed ^ 0xC1, settings.ChallengeUpsetPercent)
            : SummonRoll.RollChallengeDeclined(seed ^ 0xC2, settings.ChallengeUpsetPercent);

        switch (outcome)
        {
            case ChallengeOutcome.FleesLeavingSpoils:
                // It backed down, but it came carrying the prize, and the prize stays.
                CountForSpoils(runtime, tier);
                return IncidentText.ChallengeFled(name);
            case ChallengeOutcome.Leaves:
                return IncidentText.ChallengeLeft(name);
            default:
                var creature = await AllyController.SpawnAsync(combatState, monster, CombatSide.Enemy);
                if (creature == null)
                    return IncidentText.ChallengeLeft(name);

                Register(runtime, creature, CombatSide.Enemy, tier);
                return accepted
                    ? IncidentText.ChallengeAccepted(creature.Name)
                    : IncidentText.ChallengeForcedItself(creature.Name);
        }
    }

    /// <summary>
    ///     Whether a monster this mod brought in is still on the field, on either side. Used to hold the
    ///     next summon event back rather than piling arrivals on top of each other.
    /// </summary>
    public static bool HasLiveSummon(CombatState combatState, CombatIncidentState runtime)
    {
        runtime.Summons.RemoveAll(summon => !summon.IsAlive || !combatState.Creatures.Contains(summon));
        return runtime.Summons.Count > 0;
    }

    /// <summary>
    ///     Takes one player's share out of every purse at the table and reports what a single seat paid.
    ///     A shared decision is charged to everyone who got a vote in it; billing whoever the game
    ///     happened to hand our hook first would make the whole thing a lottery about turn order.
    /// </summary>
    private static async Task<int> ChargeEveryone(CombatState combatState, int share)
    {
        if (share <= 0)
            return 0;

        var paid = 0;
        foreach (var player in Party.Members(combatState))
        {
            var charged = Math.Min(share, (int)player.Gold);
            if (charged <= 0)
                continue;

            await PlayerCmd.LoseGold(charged, player, GoldLossType.Spent);
            paid = Math.Max(paid, charged);
        }

        return paid;
    }

    /// <summary>Only ever draws from the act the fight is in, so an early floor stays an early floor.</summary>
    private static MonsterModel? Pick(ActModel act, SummonTier tier, ulong seed)
    {
        var pool = MonsterPools.For(act, tier);
        var index = SummonRoll.PickIndex(pool.Count, seed);
        return index < 0 ? null : pool[index];
    }

    /// <summary>
    ///     Rolls each standing summon for whether it loses interest and leaves. Nothing that wandered into
    ///     someone else's fight has a reason to see it through, so dying is not the only way out.
    /// </summary>
    public static async Task<List<string>> ReleaseDepartingAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        int round)
    {
        var reports = new List<string>();
        foreach (var summon in runtime.Summons.ToList())
        {
            if (!summon.IsAlive || !combatState.Creatures.Contains(summon))
            {
                Forget(runtime, summon);
                continue;
            }

            // Never on the turn it arrived: showing up and leaving in the same breath reads as a bug.
            if (!runtime.SummonArrivals.TryGetValue(summon, out var arrived) || arrived >= round)
                continue;

            var unitId = (int)(summon.CombatId ?? 0u);
            if (!SummonRoll.LeavesThisTurn(runtime.Timeline.Seed, unitId, round,
                    settings.SummonDepartureChancePercent))
            {
                continue;
            }

            var wasAlly = summon.Side == CombatSide.Player;
            var name = summon.Name;
            await CreatureCmd.Escape(summon);
            Forget(runtime, summon);
            reports.Add(IncidentText.SummonLeft(name, wasAlly));
            MainFile.Logger.Info($"{name} lost interest and left the fight on turn {round}.");
        }

        return reports;
    }

    private static void Forget(CombatIncidentState runtime, Creature summon)
    {
        runtime.Summons.Remove(summon);
        runtime.Allies.Remove(summon);
        runtime.SummonArrivals.Remove(summon);
    }

    private static void Register(
        CombatIncidentState runtime,
        Creature creature,
        CombatSide side,
        SummonTier tier)
    {
        runtime.Summons.Add(creature);
        runtime.SummonArrivals[creature] = creature.CombatState?.RoundNumber ?? 0;
        if (side == CombatSide.Player)
        {
            runtime.Allies.Add(creature);
            return;
        }

        CountForSpoils(runtime, tier);
    }

    /// <summary>A fight that got harder pays better, and an Elite is worth a relic on top.</summary>
    private static void CountForSpoils(CombatIncidentState runtime, SummonTier tier)
    {
        if (tier == SummonTier.Elite)
            runtime.ExtraEliteMonsters++;
        else
            runtime.ExtraNormalMonsters++;
    }
}

internal sealed record PendingMercenary(MonsterModel Monster, string Name, int Price, ulong Seed);
