using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace BattlefieldIncidents.Runtime;

public sealed class IncidentDirector : SingletonModel, ICustomModel
{
    /// <summary>
    ///     What a spent Last Miracle leaves behind. The unit is sitting at 0 HP when the game hands the
    ///     death back to us, so this is the whole of the save: enough to still be standing, not enough
    ///     to survive the next hit of the same attack.
    /// </summary>
    private const int MiracleSurvivalHp = 1;

    private static readonly Dictionary<CombatState, CombatIncidentState> CombatStates =
        new(ReferenceEqualityComparer.Instance);

    private static IncidentDirector? _instance;
    private static bool _registered;

    // The spoils are asked for at room end, which may land either side of combat teardown, so the counts
    // are mirrored out of the combat state before it is dropped.
    private static int _carriedNormalSpoils;
    private static int _carriedEliteSpoils;
    private static ulong _carriedSpoilSeed;

    public override bool ShouldReceiveCombatHooks => true;

    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;

        // Deliberately does not build the combat state here. The game calls this delegate on every
        // single hook dispatch, including the ones that keep coming after the fight is over — victory
        // hooks, reward hooks — so creating state from here rebuilt it after teardown had already
        // removed it. That resurrected entry was never cleaned up again, it logged the whole 100-turn
        // route a second and third time per fight, and the later CombatEnded signal then found it and
        // ran the "abandoned fight" cleanup, which throws away the extra rewards a summon event earned.
        // The state is built where it is actually needed: the first player turn of a live fight.
        ModHelper.SubscribeForCombatStateHooks(MainFile.ModId, _ =>
        {
            _instance ??= ModelDb.GetById<IncidentDirector>(ModelDb.GetId<IncidentDirector>());
            return [_instance];
        });
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        try
        {
            if (player.Creature.CombatState is not CombatState combatState)
                return;

            var runtime = EnsureState(combatState);
            if (!runtime.EnabledForCombat)
                return;

            var round = combatState.RoundNumber;
            if (round <= runtime.LastProcessedRound)
                return;

            // Consume before the first await. Extra turns and additional co-op players share RoundNumber.
            runtime.LastProcessedRound = round;
            CloseStaleNotices(runtime, round);
            CloseExpiredToxicFogs(runtime, round);

            var settings = SettingsBootstrap.Read();
            DropMiraclesForMissingUnits(combatState, runtime);
            ResolveMiracles(combatState, runtime, settings);
            await ResolveCombatStart(choiceContext, combatState, runtime, settings, player);

            // The laser charges a share of health rather than a flat number, so the only figure worth
            // putting in a notice is what it works out to for the player reading it.
            var laserDamage = PercentOfMaxHp(player.Creature, settings.LaserHpPercent);
            ShowWarnings(runtime, round, settings, laserDamage);
            ReleaseVines(combatState, runtime, round);

            // Settled before the round's own event, so an Ancient's energy is in hand before anything
            // asks the player to spend it.
            foreach (var (kind, report) in await PioneerEvents.SettleDeferralsAsync(
                         choiceContext, combatState, runtime, round))
            {
                TrackRoundNotice(runtime, round, report,
                    IncidentText.IncidentTitle(kind), IncidentText.Icon(kind));
            }

            await ReleaseDepartingSummons(combatState, runtime, settings, round);
            await SettleContracts(combatState, runtime, settings, player, round);
            await ResolveRound(choiceContext, combatState, runtime, settings, round, player, laserDamage);
            await RunAllyTurns(combatState, runtime);
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(AfterPlayerTurnStart), exception);
        }
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        try
        {
            if (result.UnblockedDamage <= 0 ||
                !props.IsPoweredAttack() ||
                target.CombatState is not CombatState combatState ||
                !CombatStates.TryGetValue(combatState, out var runtime) ||
                !runtime.EnabledForCombat ||
                !runtime.ActiveToxicFogs.TryGetValue(combatState.RoundNumber, out var toxicFog))
            {
                return;
            }

            await PowerCmd.Apply<PoisonPower>(
                choiceContext,
                target,
                toxicFog.PoisonPerHit,
                applier: null,
                cardSource: null);
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(AfterDamageGiven), exception);
        }
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        try
        {
            var combatState = participants
                .Select(creature => creature.CombatState)
                .OfType<CombatState>()
                .FirstOrDefault();
            if (combatState == null || !CombatStates.TryGetValue(combatState, out var runtime))
                return;

            var round = combatState.RoundNumber;
            if (side == CombatSide.Player && runtime.PendingRockfalls.Remove(round, out var rockfallDamage))
            {
                // Remove before awaiting so an extra player-side turn cannot resolve the same rockfall twice.
                if (runtime.WarningNotices.Remove(round, out var warningNotice))
                    warningNotice.Close();

                var rockfallTally = await DamageEveryone(choiceContext, combatState, rockfallDamage);
                TrackResultNotice(combatState, runtime, round,
                    IncidentText.DamageResult(
                        IncidentText.Name(IncidentKind.Rockfall), DamageScope.Everyone, rockfallTally),
                    IncidentKind.Rockfall);
            }

            var pendingForSide = runtime.PendingSideDamages
                .Where(pair => pair.Key.Round == round)
                .OrderBy(pair => pair.Key.SourceTurn)
                .ThenBy(pair => pair.Key.Kind)
                .ToList();
            foreach (var (key, pending) in pendingForSide)
            {
                // Consume before the first await so extra turns cannot repeat this side's damage.
                if (!pending.TryConsume(side))
                    continue;

                // Reported after the fact, with the numbers the damage actually produced. These events
                // resolve once per side, and the enemy half lands in the middle of the enemy turn where
                // nothing else on screen accounts for it — a prediction there could not tell the player
                // whether it connected, which is the one thing they cannot check by eye.
                var tally = await DamageSide(choiceContext, combatState, side, pending.Damage,
                    pending.Hits, pending.DamagePercent, pending.Kind);
                TrackResultNotice(combatState, runtime, round,
                    IncidentText.DamageResult(SideDamageLabel(pending), Scope(side), tally),
                    pending.Kind);

                if (pending.IsComplete && runtime.PendingSideDamages.Remove(key))
                    pending.Notice.Close();
            }
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(BeforeSideTurnEnd), exception);
        }
    }

    /// <summary>
    ///     The game asks this whenever a unit is about to die, and some callers are only previewing a
    ///     death rather than resolving one, so this has to stay a plain question. The dice were thrown
    ///     at combat start; all that happens here is a lookup.
    /// </summary>
    public override bool ShouldDie(Creature creature)
    {
        try
        {
            return creature.CombatState is not CombatState combatState ||
                   !CombatStates.TryGetValue(combatState, out var runtime) ||
                   !runtime.MiracleCharges.Contains(creature);
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(ShouldDie), exception);
            return true;
        }
    }

    /// <summary>
    ///     Runs only when this model was the one that stopped the death. The unit is at 0 HP by now, so
    ///     it has to be healed back on the spot: leave it there and the game will just kill it again on
    ///     its next pass, ten times over, and then give up.
    /// </summary>
    public override async Task AfterPreventingDeath(Creature creature)
    {
        try
        {
            if (creature.CombatState is not CombatState combatState ||
                !CombatStates.TryGetValue(combatState, out var runtime) ||
                !runtime.MiracleCharges.Remove(creature))
            {
                return;
            }

            if (runtime.MiracleNotices.Remove(creature, out var grantedNotice))
                grantedNotice.Close();

            await CreatureCmd.Heal(creature, MiracleSurvivalHp);
            if (!creature.IsAlive)
            {
                // Healing is refused once combat is winding down. Say nothing and let the death stand,
                // rather than announcing a save the player would never see happen.
                MainFile.Logger.Info($"Last Miracle could not revive {creature.Name}; the death stands.");
                return;
            }

            TrackRoundNotice(runtime, combatState.RoundNumber,
                IncidentText.MiracleTrigger(creature.Name),
                IncidentText.IncidentTitle(IncidentKind.LastMiracle),
                IncidentText.Icon(IncidentKind.LastMiracle));
            MainFile.Logger.Info($"Last Miracle spent by {creature.Name}.");
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(AfterPreventingDeath), exception);
        }
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        try
        {
            if (CombatStates.TryGetValue(room.CombatState, out var runtime))
            {
                // Both of these are promises made during the fight, and both come due now: the wax goes
                // hard, and Nonupeipe pays out on a fight that was actually won.
                await PioneerEvents.MeltWaxAsync(runtime);
                await PayPromisedGold(room.CombatState, runtime);
            }

            CleanUpCombat(room);
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(AfterCombatEnd), exception);
        }
    }

    /// <summary>
    ///     Combat hooks cover the victory path, but the manager also raises this signal when a player
    ///     dies or the fight is abandoned.  Close the UI state there too; a lost fight must never carry
    ///     its pending notices or summon spoils into the next room.
    /// </summary>
    internal static void OnCombatEnded(CombatRoom room)
    {
        try
        {
            if (!CombatStates.ContainsKey(room.CombatState))
                return;

            CleanUpCombat(room, carrySpoils: false);
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(OnCombatEnded), exception);
        }
    }

    /// <summary>
    ///     The vines hold one player's cards down. Routed through the game's own play gate so the cards
    ///     read as unplayable on screen instead of simply refusing to work when clicked.
    /// </summary>
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        try
        {
            if (card.Owner?.Creature.CombatState is not CombatState combatState ||
                !CombatStates.TryGetValue(combatState, out var runtime))
            {
                return true;
            }

            return !StranglingVines.Holds(runtime, card);
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(ShouldPlay), exception);
            return true;
        }
    }

    private static async Task PayPromisedGold(CombatState combatState, CombatIncidentState runtime)
    {
        if (runtime.PromisedGold <= 0)
            return;

        var owed = runtime.PromisedGold;
        runtime.PromisedGold = 0;
        foreach (var player in Party.Members(combatState))
            await PlayerCmd.GainGold(owed, player);

        MainFile.Logger.Info($"Nonupeipe paid out {owed} gold to each player.");
    }

    private static void ReleaseVines(CombatState combatState, CombatIncidentState runtime, int round)
    {
        var report = StranglingVines.Release(combatState, runtime, round);
        if (report == null)
            return;

        TrackRoundNotice(runtime, round, report,
            IncidentText.IncidentTitle(IncidentKind.StranglingVines),
            IncidentText.Icon(IncidentKind.StranglingVines));
    }

    /// <summary>
    ///     Closes every notice that belongs to a round already gone. Without this the player accumulates
    ///     messages about turns that have passed, and has to work out which one is current.
    /// </summary>
    private static void CloseStaleNotices(CombatIncidentState runtime, int round)
    {
        foreach (var stale in runtime.RoundNotices.Keys.Where(key => key < round).ToList())
        {
            if (!runtime.RoundNotices.Remove(stale, out var notices))
                continue;

            foreach (var notice in notices)
                notice.Close();
        }

        foreach (var stale in runtime.WarningNotices.Keys.Where(key => key < round).ToList())
        {
            if (runtime.WarningNotices.Remove(stale, out var notice))
                notice.Close();
        }

        foreach (var (key, pending) in runtime.PendingSideDamages.Where(pair => pair.Key.Round < round).ToList())
        {
            if (runtime.PendingSideDamages.Remove(key))
                pending.Notice.Close();
        }
    }

    /// <summary>
    ///     Posts a result notice only if the fight is still going.
    ///     <para>
    ///     These lines are written after the damage lands, and that damage can be the blow that ends the
    ///     combat — at which point teardown has already closed every notice this fight owned. Adding one
    ///     here anyway would leave a toast about a finished fight sitting over the reward screen with
    ///     nothing left to close it.
    ///     </para>
    /// </summary>
    private static void TrackResultNotice(
        CombatState combatState,
        CombatIncidentState runtime,
        int round,
        string body,
        IncidentKind kind)
    {
        if (!CombatStates.TryGetValue(combatState, out var current) || !ReferenceEquals(current, runtime))
        {
            MainFile.Logger.Info($"{kind} resolved as the fight ended; skipping its result notice.");
            return;
        }

        TrackRoundNotice(runtime, round, body,
            IncidentText.IncidentTitle(kind), IncidentText.Icon(kind));
    }

    private static void TrackRoundNotice(
        CombatIncidentState runtime,
        int round,
        string body,
        string title,
        Godot.Texture2D? icon)
    {
        var notice = IncidentToast.ShowRoundNotice(body, title, icon);
        if (!runtime.RoundNotices.TryGetValue(round, out var notices))
        {
            notices = [];
            runtime.RoundNotices[round] = notices;
        }

        notices.Add(notice);
    }

    // A mod-side failure must never break the game's combat pipeline; drop the event and keep playing.
    private static void ReportHookFailure(string hook, Exception exception) =>
        MainFile.Logger.Error($"{hook} failed; skipping this combat event. {exception}");

    private static void CleanUpCombat(CombatRoom room, bool carrySpoils = true)
    {
        if (CombatStates.TryGetValue(room.CombatState, out var runtime))
        {
            foreach (var notice in runtime.WarningNotices.Values)
                notice.Close(immediate: true);
            runtime.WarningNotices.Clear();

            foreach (var pending in runtime.PendingSideDamages.Values)
                pending.Notice.Close(immediate: true);
            runtime.PendingSideDamages.Clear();

            foreach (var toxicFog in runtime.ActiveToxicFogs.Values)
                toxicFog.Notice.Close(immediate: true);
            runtime.ActiveToxicFogs.Clear();

            foreach (var notice in runtime.RoundNotices.Values.SelectMany(notices => notices))
                notice.Close(immediate: true);
            runtime.RoundNotices.Clear();

            foreach (var notice in runtime.MiracleNotices.Values)
                notice.Close(immediate: true);
            runtime.MiracleNotices.Clear();
            runtime.MiracleCharges.Clear();
            runtime.Allies.Clear();
            runtime.Summons.Clear();
            runtime.SummonArrivals.Clear();
            runtime.SummonOrigins.Clear();
            runtime.PendingMercenaries.Clear();
            runtime.Deferrals.Clear();
            runtime.Vines?.Notice?.Close(immediate: true);
            runtime.Vines = null;
            if (carrySpoils)
            {
                _carriedNormalSpoils = runtime.ExtraNormalMonsters;
                _carriedEliteSpoils = runtime.ExtraEliteMonsters;
                _carriedSpoilSeed = runtime.Timeline.Seed;
            }
            else
            {
                _carriedNormalSpoils = 0;
                _carriedEliteSpoils = 0;
                _carriedSpoilSeed = 0;
            }
        }

        CombatStates.Remove(room.CombatState);
    }

    private static CombatIncidentState EnsureState(CombatState combatState)
    {
        if (CombatStates.TryGetValue(combatState, out var existing))
            return existing;

        var settings = SettingsBootstrap.Read();
        var actTheme = GetActTheme(combatState);
        var enabled = settings.Enabled && IsCombatTypeEnabled(combatState, settings);
        var seed = BuildCombatSeed(combatState);
        var timeline = IncidentTimelineGenerator.Generate(seed, settings.ToTimelineOptions(actTheme));

        var state = new CombatIncidentState
        {
            Timeline = timeline,
            ActTheme = actTheme,
            EnabledForCombat = enabled,
            LastProcessedRound = Math.Max(0, combatState.RoundNumber - 1),
        };
        CombatStates.Add(combatState, state);

        var incidentList = string.Join(", ", timeline.Incidents.Select(incident =>
            $"R{incident.Turn}:{IncidentText.Name(incident.Incident!.Value)}"));
        MainFile.Logger.Info($"Incident route seed={seed}, act={actTheme}, enabled={enabled}: " +
                             (string.IsNullOrEmpty(incidentList) ? "no incidents" : incidentList));
        return state;
    }

    /// <summary>
    ///     Rolls each unit's Last Miracle from the combat seed and announces the ones that landed.
    ///     Both sides are rolled: an enemy that shrugs off a killing blow with no explanation reads as
    ///     the game cheating, and knowing about it in advance is what turns it into something the
    ///     player can plan a second hit around.
    /// </summary>
    private static void ResolveMiracles(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings)
    {
        if (runtime.MiraclesResolved)
            return;

        runtime.MiraclesResolved = true;
        if (!settings.EnableMiracle)
            return;

        // Reloading rebuilds this state from the seed, which would hand a spent charge straight back.
        // The route's promise is that a reload rerolls nothing, so a fight already under way keeps
        // whatever it has left, which is nothing. Losing an unused charge is the harmless direction to
        // fail in; handing back a used one is not.
        if (combatState.RoundNumber > 1)
        {
            MainFile.Logger.Info("Skipping Last Miracle grants: this combat was resumed mid-fight.");
            return;
        }

        var nextIndex = 0;
        foreach (var creature in combatState.Creatures)
        {
            // Advance the index for every unit, alive or not, so the same combat seed keeps handing
            // the same result to the same slot.
            var unitIndex = nextIndex++;
            var chancePermille = creature.IsPlayer
                ? settings.PlayerMiracleChancePermille
                : settings.EnemyMiracleChancePermille;
            if (!creature.IsAlive ||
                !MiracleRoll.IsGranted(runtime.Timeline.Seed, unitIndex, chancePermille))
            {
                continue;
            }

            runtime.MiracleCharges.Add(creature);
            runtime.MiracleNotices[creature] = IncidentToast.ShowPersistentInfo(
                IncidentText.MiracleGranted(creature.Name),
                IncidentText.CombatStartTitle(IncidentKind.LastMiracle),
                IncidentText.Icon(IncidentKind.LastMiracle));
            MainFile.Logger.Info(
                $"Last Miracle granted to {creature.Name} (slot {unitIndex}, {chancePermille}/1000).");
        }
    }

    /// <summary>
    ///     A unit can leave the fight without the interception ever running: a forced kill skips the
    ///     check entirely, and an escape removes the unit outright. Take those notices down rather than
    ///     leave the screen promising a save for something that is no longer standing there.
    /// </summary>
    private static void DropMiraclesForMissingUnits(CombatState combatState, CombatIncidentState runtime)
    {
        foreach (var creature in runtime.MiracleCharges.ToList())
        {
            if (creature.IsAlive && combatState.Creatures.Contains(creature))
                continue;

            runtime.MiracleCharges.Remove(creature);
            if (runtime.MiracleNotices.Remove(creature, out var notice))
                notice.Close();
        }
    }

    /// <summary>
    ///     Rolls the once-per-combat blessing or curse. This is deliberately independent of the turn route:
    ///     it has no warning to give and nothing to counter, so folding it into the timeline's weights would
    ///     mix two different kinds of decision.
    /// </summary>
    private static async Task ResolveCombatStart(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        Player player)
    {
        if (runtime.CombatStartResolved)
            return;

        // Consume before the first await so a second player's turn start cannot roll it again.
        runtime.CombatStartResolved = true;

        await ClaimMarkedRoom(combatState, runtime, player);
        await RollVines(combatState, runtime, settings);

        if (!BoonExecutor.IsCombatStartEnabledFor(combatState, settings))
            return;

        // A separate seed stream keeps the combat-start roll stable when route settings change.
        var kind = BoonExecutor.RollCombatStart(runtime.Timeline.Seed ^ 0x9E3779B97F4A7C15UL, settings);
        if (kind == null)
        {
            MainFile.Logger.Info("Combat-start boon: none rolled.");
            return;
        }

        var option = await BoonExecutor.Apply(
            kind.Value, runtime.Timeline.Seed ^ 0xC2B2AE3D27D4EB4FUL, choiceContext, combatState, player);
        if (option == null)
        {
            MainFile.Logger.Warn($"Combat-start {kind} rolled but no usable option was available.");
            return;
        }

        MainFile.Logger.Info($"Combat-start boon: {kind} -> {option.Id}.");
        var boonKind = kind.Value == BoonKind.Blessing
            ? IncidentKind.NeowsBlessing
            : IncidentKind.ArchitectsCurse;
        TrackRoundNotice(runtime, combatState.RoundNumber,
            IncidentText.BoonTrigger(kind.Value, option),
            IncidentText.CombatStartTitle(boonKind),
            IncidentText.Icon(boonKind));
    }

    /// <summary>
    ///     Cashes in a room Nonupeipe marked earlier in the act: one of the monsters waiting here has
    ///     been sitting on its last point of health since you saw the pin go up on the map.
    /// </summary>
    private static async Task ClaimMarkedRoom(
        CombatState combatState,
        CombatIncidentState runtime,
        Player player)
    {
        if (!MarkedRooms.TryClaim(player))
            return;

        var target = combatState.Creatures
            .Where(creature => creature.Side == CombatSide.Enemy && creature.IsHittable)
            .OrderByDescending(creature => creature.MaxHp)
            .FirstOrDefault();
        if (target == null)
            return;

        // The biggest thing in the room, because a mark that lands on the weakest monster present is a
        // gift the player would never notice they had been given.
        await CreatureCmd.SetCurrentHp(target, 1m);
        TrackRoundNotice(runtime, combatState.RoundNumber,
            IncidentText.MarkedRoomTrigger(target.Name),
            IncidentText.CombatStartTitle(IncidentKind.NonupeipesGift),
            IncidentText.Icon(IncidentKind.NonupeipesGift));
        MainFile.Logger.Info($"Nonupeipe's mark spent: {target.Name} starts at 1 HP.");
    }

    private static async Task RollVines(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings)
    {
        var report = await StranglingVines.ResolveAsync(
            combatState, runtime, settings, runtime.Timeline.Seed ^ 0x8C15_1A2B_3D4E_5F60UL);
        if (report == null)
            return;

        // A standing notice rather than a one-round one: it describes a condition of the fight that
        // lasts until somebody does something about it.
        var notice = IncidentToast.ShowPersistentInfo(
            report,
            IncidentText.CombatStartTitle(IncidentKind.StranglingVines),
            IncidentText.Icon(IncidentKind.StranglingVines));
        if (runtime.Vines != null)
            runtime.Vines.Notice = notice;
    }

    private static void ShowWarnings(
        CombatIncidentState runtime,
        int round,
        IncidentSettings settings,
        decimal laserDamage)
    {
        var warningTurns = settings.WarningTurns;
        if (warningTurns <= 0)
            return;

        foreach (var checkpoint in runtime.Timeline.Incidents.Where(incident =>
                     incident.Turn - round == warningTurns))
        {
            if (!runtime.WarnedCheckpointTurns.Add(checkpoint.Turn))
                continue;

            var kind = checkpoint.Incident!.Value;
            runtime.WarningNotices[checkpoint.Turn] = IncidentToast.ShowWarning(
                IncidentText.Warning(checkpoint, warningTurns, settings, laserDamage),
                IncidentText.OmenTitle(checkpoint.Turn),
                IncidentText.Icon(kind));
        }
    }

    /// <summary>
    ///     A share of a unit's own health, floored, and never less than a single point so the weakest
    ///     unit on the field is still touched by it.
    /// </summary>
    private static decimal PercentOfMaxHp(Creature creature, int percent) =>
        Math.Max(1m, Math.Floor((decimal)creature.MaxHp * Math.Clamp(percent, 1, 100) / 100m));

    private static async Task ResolveRound(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        int round,
        Player player,
        decimal laserDamage)
    {
        if (runtime.WarningNotices.Remove(round, out var warningNotice))
            warningNotice.Close();

        // Ongoing incidents are derived from the original route, so a mid-combat reload cannot erase them.
        foreach (var source in runtime.Timeline.Incidents.Where(incident =>
                     incident.Incident == IncidentKind.HiveOnslaught &&
                     round >= incident.Turn && round < incident.Turn + incident.Duration))
        {
            var wave = round - source.Turn + 1;
            ArmSideDamage(
                runtime,
                source,
                round,
                IncidentKind.HiveOnslaught,
                settings.HiveOnslaughtDamage,
                hits: 1,
                wave,
                source.Duration,
                IncidentText.HiveWave(source, wave, settings));
        }

        foreach (var checkpoint in runtime.Timeline.Incidents.Where(incident =>
                     incident.Turn == round && incident.Incident != IncidentKind.HiveOnslaught))
        {
            if (checkpoint.Incident == IncidentKind.Rockfall)
            {
                // Snapshot the configured damage so the warning and later impact cannot diverge.
                runtime.PendingRockfalls[round] = settings.RockfallDamage;
                runtime.WarningNotices[round] = IncidentToast.ShowWarning(
                    IncidentText.Trigger(checkpoint, settings, laserDamage),
                    IncidentText.IncidentTitle(IncidentKind.Rockfall),
                    IncidentText.Icon(IncidentKind.Rockfall));
                continue;
            }

            if (checkpoint.Incident == IncidentKind.Laser)
            {
                ArmSideDamage(
                    runtime,
                    checkpoint,
                    round,
                    IncidentKind.Laser,
                    damage: 0,
                    hits: 1,
                    wave: 1,
                    duration: 1,
                    IncidentText.Trigger(checkpoint, settings, laserDamage),
                    Math.Clamp(settings.LaserHpPercent, 1, 100));
                continue;
            }


            if (checkpoint.Incident == IncidentKind.SwordRain)
            {
                ArmSideDamage(
                    runtime,
                    checkpoint,
                    round,
                    IncidentKind.SwordRain,
                    settings.SwordRainDamagePerHit,
                    Math.Clamp(settings.SwordRainHitCount, 1, 10),
                    wave: 1,
                    duration: 1,
                    IncidentText.Trigger(checkpoint, settings, laserDamage));
                continue;
            }

            if (checkpoint.Incident == IncidentKind.ToxicFog)
            {
                var poisonPerHit = Math.Clamp(settings.ToxicFogPoisonPerHit, 1, 10);
                runtime.ActiveToxicFogs[round] = new ActiveToxicFog(
                    poisonPerHit,
                    IncidentToast.ShowWarning(
                        IncidentText.Trigger(checkpoint, settings, laserDamage),
                        IncidentText.IncidentTitle(IncidentKind.ToxicFog),
                        IncidentText.Icon(IncidentKind.ToxicFog)));
                continue;
            }

            if (checkpoint.Incident is IncidentKind.FreeSummon or IncidentKind.Mercenary
                or IncidentKind.EnemyRecruit or IncidentKind.Challenge)
            {
                // One newcomer at a time. Stacking summons turns a fight into a crowd nobody asked for,
                // so the next one waits until the last has left the field.
                if (SummonEvents.HasLiveSummon(combatState, runtime))
                {
                    MainFile.Logger.Info(
                        $"Skipping {checkpoint.Incident} on turn {round}: a summoned monster is still standing.");
                    continue;
                }

                // Resolved inline: every one of these has to name the monster it is talking about, and
                // three of them wait on an answer before anything happens.
                var summonKind = checkpoint.Incident.Value;
                var report = summonKind switch
                {
                    IncidentKind.FreeSummon => await SummonEvents.ResolveFreeAsync(
                        combatState, runtime, settings, checkpoint.EffectSeed),
                    IncidentKind.Mercenary => await SummonEvents.ResolveMercenaryAsync(
                        combatState, runtime, settings, player, round, checkpoint.EffectSeed),
                    IncidentKind.EnemyRecruit => await SummonEvents.ResolveRecruitAsync(
                        combatState, runtime, settings, player, checkpoint.EffectSeed),
                    _ => await SummonEvents.ResolveChallengeAsync(
                        combatState, runtime, settings, checkpoint.EffectSeed),
                };

                if (report != null)
                {
                    TrackRoundNotice(runtime, round, report,
                        IncidentText.IncidentTitle(summonKind), IncidentText.Icon(summonKind));
                }

                continue;
            }

            if (checkpoint.Incident is IncidentKind.VakuusTakeover or IncidentKind.DarvsGamble
                or IncidentKind.NonupeipesGift or IncidentKind.TanxsArmory
                or IncidentKind.TezcatarasEmber or IncidentKind.PaelsBlessing
                or IncidentKind.OrobassOffer)
            {
                // Resolved inline like the summon events: two of them wait on an answer, and every one
                // of them can only name what it handed over once it has handed it over.
                var ancient = checkpoint.Incident.Value;
                var report = await PioneerEvents.ResolveAsync(
                    ancient, choiceContext, combatState, runtime, settings, round, checkpoint.EffectSeed);
                if (report != null)
                {
                    TrackRoundNotice(runtime, round, report,
                        IncidentText.IncidentTitle(ancient), IncidentText.Icon(ancient));
                }

                continue;
            }

            if (checkpoint.Incident is IncidentKind.NeowsBlessing or IncidentKind.ArchitectsCurse)
            {
                // The toast has to name what was actually handed out, which is only known after it fires.
                var boonKind = checkpoint.Incident == IncidentKind.NeowsBlessing
                    ? BoonKind.Blessing
                    : BoonKind.Curse;
                var option = await BoonExecutor.Apply(
                    boonKind, checkpoint.EffectSeed, choiceContext, combatState, player);
                if (option != null)
                {
                    TrackRoundNotice(runtime, round,
                        IncidentText.BoonTrigger(boonKind, option),
                        IncidentText.IncidentTitle(checkpoint.Incident.Value),
                        IncidentText.Icon(checkpoint.Incident.Value));
                }

                continue;
            }

            ShowTrigger(runtime, round, checkpoint, settings, laserDamage);
            await Execute(checkpoint.Incident!.Value, choiceContext, combatState, settings);
        }
    }

    private static void ShowTrigger(
        CombatIncidentState runtime,
        int round,
        ScheduledCheckpoint checkpoint,
        IncidentSettings settings,
        decimal laserDamage)
    {
        var kind = checkpoint.Incident!.Value;
        TrackRoundNotice(runtime, round,
            IncidentText.Trigger(checkpoint, settings, laserDamage),
            IncidentText.IncidentTitle(kind),
            IncidentText.Icon(kind));
    }

    private static async Task Execute(
        IncidentKind incident,
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        IncidentSettings settings)
    {
        switch (incident)
        {
            case IncidentKind.Rockfall:
                // Rockfall is armed at turn start and resolves from BeforeSideTurnEnd.
                break;
            case IncidentKind.SwordRain:
            case IncidentKind.ToxicFog:
                // These are armed at turn start and resolved by their combat hooks.
                break;
            case IncidentKind.VineSnare:
                await PowerCmd.Apply<VulnerablePower>(choiceContext, HittableCreatures(combatState),
                    Math.Clamp(settings.VineSnareVulnerable, 1, 9), null, null);
                break;
            case IncidentKind.DampSeaWind:
                var targets = HittableCreatures(combatState);
                await PowerCmd.Apply<WeakPower>(choiceContext, targets,
                    Math.Clamp(settings.DampSeaWindWeak, 1, 9), null, null);
                await PowerCmd.Apply<FrailPower>(choiceContext, targets,
                    Math.Clamp(settings.DampSeaWindFrail, 1, 9), null, null);
                break;
            case IncidentKind.HiveOnslaught:
                break;
            case IncidentKind.GentleRain:
                await HealAllLiving(combatState, settings.GentleRainHealPercent,
                    settings.GentleRainPlayerMinimumHeal);
                break;
            case IncidentKind.NeowsBlessing:
            case IncidentKind.ArchitectsCurse:
                // Resolved inline in ResolveRound so the toast can name what was handed out.
                break;
            case IncidentKind.LastMiracle:
                // Never scheduled: it is granted at combat start and waits for a lethal blow.
                break;
            case IncidentKind.Laser:
                // Armed at turn start; each side is charged its share at the end of its own turn.
                break;
            case IncidentKind.FreeSummon:
            case IncidentKind.Mercenary:
            case IncidentKind.EnemyRecruit:
            case IncidentKind.Challenge:
                // Resolved inline in ResolveRound so the notice can name the monster involved.
                break;
            case IncidentKind.VakuusTakeover:
            case IncidentKind.DarvsGamble:
            case IncidentKind.NonupeipesGift:
            case IncidentKind.TanxsArmory:
            case IncidentKind.TezcatarasEmber:
            case IncidentKind.PaelsBlessing:
            case IncidentKind.OrobassOffer:
                // Resolved inline in ResolveRound: each one only knows what it gave after giving it.
                break;
            case IncidentKind.StranglingVines:
                // Rolled at combat start, never scheduled on the route.
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(incident), incident, null);
        }
    }

    /// <summary>Which half of the room a side's damage counts as, for the notice that reports it.</summary>
    private static DamageScope Scope(CombatSide side) =>
        side == CombatSide.Player ? DamageScope.Players : DamageScope.Enemies;

    /// <summary>
    ///     What to call this blast in its result line. The Hive runs for several turns, so its waves are
    ///     numbered; everything else is simply itself.
    /// </summary>
    private static string SideDamageLabel(PendingSideDamage pending) =>
        pending.Kind == IncidentKind.HiveOnslaught
            ? IncidentText.HiveWaveLabel(pending.Wave, pending.Duration)
            : IncidentText.Name(pending.Kind);

    /// <summary>
    ///     Rocks fall on the whole room, not on one side of it.
    /// </summary>
    private static async Task<DamageTally> DamageEveryone(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        decimal damage)
    {
        var results = await CreatureCmd.Damage(
            choiceContext,
            HittableCreatures(combatState),
            damage,
            ValueProp.Unpowered,
            null,
            null,
            null);
        return Tally(results);
    }

    private static async Task<DamageTally> DamageSide(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        CombatSide side,
        decimal damage,
        int hits,
        int damagePercent,
        IncidentKind kind)
    {
        var tally = new DamageTally();
        for (var hit = 0; hit < hits; hit++)
        {
            var targets = combatState.Creatures
                .Where(creature => creature.Side == side && creature.IsHittable)
                .ToList();
            if (targets.Count == 0)
                return tally;

            if (damagePercent <= 0)
            {
                tally = Tally(await CreatureCmd.Damage(choiceContext, targets, damage,
                    ValueProp.Unpowered, null, null, null), tally);
                continue;
            }

            // Each unit is charged its own share, so this cannot go out as one group call. Hittability
            // is re-checked as we go, because an earlier unit dying can take the rest of the row with it.
            foreach (var target in targets.Where(target => target.IsHittable))
            {
                var share = PercentOfMaxHp(target, damagePercent);
                // Logged per unit because a share of max HP is the one figure a player cannot verify by
                // eye, and "is it really charging the enemies their own percentage" is a fair question.
                MainFile.Logger.Info(
                    $"{kind} charges {target.Name} ({side}) {damagePercent}% of {target.MaxHp} max HP = {share}.");
                tally = Tally(
                    await CreatureCmd.Damage(choiceContext, [target], share, ValueProp.Unpowered,
                        null, null, null),
                    tally);
            }
        }

        return tally;
    }

    private static DamageTally Tally(
        IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.DamageResult>? results,
        DamageTally running = default)
    {
        if (results == null)
            return running;

        return results.Aggregate(running, (current, result) => current.Add(result));
    }

    /// <summary>
    ///     The rain falls on everyone, but not evenly.
    ///     <para>
    ///     A flat share of max HP quietly favours the enemy side: a room of monsters usually has more
    ///     total health than the player does, so "3% to every living unit" hands the opposition more
    ///     than it hands you, and the one event filed under Aid was slightly working against you. The
    ///     player's share therefore has a floor. Enemies keep the old minimum of a single point.
    ///     </para>
    /// </summary>
    private static async Task HealAllLiving(
        CombatState combatState,
        int configuredPercent,
        int playerMinimum)
    {
        var percent = Math.Clamp(configuredPercent, 1, 100);
        var floorForPlayers = Math.Max(1, playerMinimum);
        var livingCreatures = combatState.Creatures.Where(creature => creature.IsAlive).ToList();
        foreach (var creature in livingCreatures)
        {
            if (!creature.IsAlive || creature.CurrentHp >= creature.MaxHp)
                continue;

            var share = Math.Floor((decimal)creature.MaxHp * percent / 100m);
            var minimum = creature.IsPlayer ? floorForPlayers : 1;
            await CreatureCmd.Heal(creature, Math.Max(minimum, share));
        }
    }

    private static void ArmSideDamage(
        CombatIncidentState runtime,
        ScheduledCheckpoint source,
        int round,
        IncidentKind kind,
        decimal damage,
        int hits,
        int wave,
        int duration,
        string warningText,
        int damagePercent = 0)
    {
        var key = new PendingSideDamageKey(source.Turn, round, kind);
        if (runtime.PendingSideDamages.ContainsKey(key))
            return;

        runtime.PendingSideDamages.Add(key, new PendingSideDamage
        {
            Kind = kind,
            SourceTurn = source.Turn,
            Round = round,
            Damage = damage,
            DamagePercent = damagePercent,
            Hits = Math.Clamp(hits, 1, 10),
            Wave = wave,
            Duration = duration,
            Notice = IncidentToast.ShowWarning(
                warningText,
                IncidentText.IncidentTitle(kind),
                IncidentText.Icon(kind)),
        });
    }

    private static void CloseExpiredToxicFogs(CombatIncidentState runtime, int currentRound)
    {
        foreach (var round in runtime.ActiveToxicFogs.Keys.Where(round => round < currentRound).ToList())
        {
            var toxicFog = runtime.ActiveToxicFogs[round];
            runtime.ActiveToxicFogs.Remove(round);
            toxicFog.Notice.Close();
        }
    }

    private static List<Creature> HittableCreatures(CombatState combatState) =>
        combatState.Creatures.Where(creature => creature.IsHittable).ToList();

    private static bool IsCombatTypeEnabled(CombatState combatState, IncidentSettings settings) =>
        combatState.Encounter?.RoomType switch
        {
            RoomType.Monster => settings.EnableNormalCombats,
            RoomType.Elite => settings.EnableEliteCombats,
            RoomType.Boss => settings.EnableBossCombats,
            _ => false,
        };

    private static ActTheme GetActTheme(CombatState combatState) => combatState.RunState.Act switch
    {
        Overgrowth => ActTheme.Overgrowth,
        Underdocks => ActTheme.Underdocks,
        Hive => ActTheme.Hive,
        Glory => ActTheme.Glory,
        _ => ActTheme.Unknown,
    };

    private static ulong BuildCombatSeed(CombatState combatState)
    {
        var runState = combatState.RunState;
        var coordinate = runState.CurrentMapCoord;
        return CombatSeedBuilder.Build(
            runState.Rng.Seed,
            runState.CurrentActIndex,
            runState.ActFloor,
            runState.TotalFloor,
            coordinate?.col ?? -1,
            coordinate?.row ?? -1,
            combatState.Encounter?.Id.Entry ?? "UNKNOWN_ENCOUNTER");
    }

    private static async Task ReleaseDepartingSummons(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        int round)
    {
        foreach (var (kind, report) in
                 await SummonEvents.ReleaseDepartingAsync(combatState, runtime, settings, round))
        {
            // Filed under the event that brought the monster in. A mercenary walking out on a contract
            // announced as a "Wandering Monster" reads as an unrelated third thing happening.
            TrackRoundNotice(runtime, round, report,
                IncidentText.IncidentTitle(kind), IncidentText.Icon(kind));
        }
    }

    /// <summary>
    ///     Pays out any contract agreed on the previous turn. Deliberately before the round's own event,
    ///     so the help you bought is standing there before the next thing happens.
    /// </summary>
    private static async Task SettleContracts(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        Player player,
        int round)
    {
        if (!runtime.PendingMercenaries.Remove(round, out var pending))
            return;

        var report = await SummonEvents.SettleMercenaryAsync(
            combatState, runtime, settings, player, pending);
        if (report != null)
        {
            TrackRoundNotice(runtime, round, report,
                IncidentText.IncidentTitle(IncidentKind.Mercenary),
                IncidentText.Icon(IncidentKind.Mercenary));
        }
    }

    /// <summary>
    ///     Runs the player's summoned monsters. The game's turn loop only walks the enemy list, so an
    ///     ally that is not driven from here simply stands and watches.
    /// </summary>
    private static async Task RunAllyTurns(CombatState combatState, CombatIncidentState runtime)
    {
        runtime.Allies.RemoveAll(ally => !ally.IsAlive || !combatState.Creatures.Contains(ally));
        foreach (var ally in runtime.Allies.ToList())
            await AllyController.PerformTurnAsync(ally, combatState);
    }

    /// <summary>
    ///     Puts a summoned ally in front of a blow meant for the player. Whatever overkills past the ally
    ///     spills back onto the player on its own; the damage command handles that once the target has
    ///     been swapped, which is the same path Osty takes.
    /// </summary>
    public override Creature ModifyUnblockedDamageTarget(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer)
    {
        try
        {
            if (target.CombatState is not CombatState combatState ||
                !CombatStates.TryGetValue(combatState, out var runtime))
            {
                return target;
            }

            return AllyController.FindBodyguard(runtime.Allies, target, props, dealer) ?? target;
        }
        catch (Exception exception)
        {
            ReportHookFailure(nameof(ModifyUnblockedDamageTarget), exception);
            return target;
        }
    }

    /// <summary>
    ///     Reads the extra-monster counts and clears them. They may still live on the combat state, or
    ///     may already have been mirrored out of it, depending on whether rewards are asked for before or
    ///     after the combat is torn down.
    /// </summary>
    internal static (int Normals, int Elites, ulong Seed) TakeCarriedSpoils(AbstractRoom? room)
    {
        if (room is CombatRoom combatRoom &&
            CombatStates.TryGetValue(combatRoom.CombatState, out var runtime) &&
            (runtime.ExtraNormalMonsters > 0 || runtime.ExtraEliteMonsters > 0))
        {
            var live = (runtime.ExtraNormalMonsters, runtime.ExtraEliteMonsters, runtime.Timeline.Seed);
            runtime.ExtraNormalMonsters = 0;
            runtime.ExtraEliteMonsters = 0;
            return live;
        }

        var carried = (_carriedNormalSpoils, _carriedEliteSpoils, _carriedSpoilSeed);
        _carriedNormalSpoils = 0;
        _carriedEliteSpoils = 0;
        return carried;
    }
}
