using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace BattlefieldIncidents.Runtime;

public sealed class IncidentDirector : SingletonModel, ICustomModel
{
    private static readonly Dictionary<CombatState, CombatIncidentState> CombatStates =
        new(ReferenceEqualityComparer.Instance);

    private static IncidentDirector? _instance;
    private static bool _registered;

    public override bool ShouldReceiveCombatHooks => true;

    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        ModHelper.SubscribeForCombatStateHooks(MainFile.ModId, combatState =>
        {
            EnsureState(combatState);
            _instance ??= ModelDb.GetById<IncidentDirector>(ModelDb.GetId<IncidentDirector>());
            return [_instance];
        });
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
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
        CloseExpiredToxicFogs(runtime, round);

        var settings = SettingsBootstrap.Read();
        ShowWarnings(runtime, round, settings);
        await ResolveRound(choiceContext, combatState, runtime, settings, round);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
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

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
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

            IncidentToast.ShowInfo(
                IncidentText.RockfallImpact(rockfallDamage),
                IncidentText.IncidentTitle(IncidentKind.Rockfall),
                IncidentText.Icon(IncidentKind.Rockfall));
            await DamagePlayers(choiceContext, combatState, rockfallDamage);
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

            IncidentToast.ShowInfo(
                IncidentText.SideDamageImpact(pending, side),
                IncidentText.IncidentTitle(pending.Kind),
                IncidentText.Icon(pending.Kind));
            await DamageSide(choiceContext, combatState, side, pending.Damage, pending.Hits);

            if (pending.IsComplete && runtime.PendingSideDamages.Remove(key))
                pending.Notice.Close();
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
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
        }

        CombatStates.Remove(room.CombatState);
        return Task.CompletedTask;
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

    private static void ShowWarnings(CombatIncidentState runtime, int round, IncidentSettings settings)
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
                IncidentText.Warning(checkpoint, warningTurns, settings),
                IncidentText.OmenTitle(checkpoint.Turn),
                IncidentText.Icon(kind));
        }
    }

    private static async Task ResolveRound(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        int round)
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
                    IncidentText.Trigger(checkpoint, settings),
                    IncidentText.IncidentTitle(IncidentKind.Rockfall),
                    IncidentText.Icon(IncidentKind.Rockfall));
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
                    IncidentText.Trigger(checkpoint, settings));
                continue;
            }

            if (checkpoint.Incident == IncidentKind.ToxicFog)
            {
                var poisonPerHit = Math.Clamp(settings.ToxicFogPoisonPerHit, 1, 10);
                runtime.ActiveToxicFogs[round] = new ActiveToxicFog(
                    poisonPerHit,
                    IncidentToast.ShowWarning(
                        IncidentText.Trigger(checkpoint, settings),
                        IncidentText.IncidentTitle(IncidentKind.ToxicFog),
                        IncidentText.Icon(IncidentKind.ToxicFog)));
                continue;
            }

            ShowTrigger(checkpoint, settings);
            await Execute(checkpoint.Incident!.Value, choiceContext, combatState, settings);
        }
    }

    private static void ShowTrigger(ScheduledCheckpoint checkpoint, IncidentSettings settings)
    {
        var kind = checkpoint.Incident!.Value;
        IncidentToast.ShowInfo(
            IncidentText.Trigger(checkpoint, settings),
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
                await HealAllLiving(combatState, settings.GentleRainHealPercent);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(incident), incident, null);
        }
    }

    private static Task<IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.DamageResult>> DamagePlayers(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        decimal damage) => CreatureCmd.Damage(
        choiceContext,
        combatState.PlayerCreatures.Where(creature => creature.IsHittable).ToList(),
        damage,
        ValueProp.Unpowered,
        null,
        null,
        null);

    private static async Task DamageSide(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        CombatSide side,
        decimal damage,
        int hits)
    {
        for (var hit = 0; hit < hits; hit++)
        {
            var targets = combatState.Creatures
                .Where(creature => creature.Side == side && creature.IsHittable)
                .ToList();
            if (targets.Count == 0)
                return;

            await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Unpowered,
                null, null, null);
        }
    }

    private static async Task HealAllLiving(CombatState combatState, int configuredPercent)
    {
        var percent = Math.Clamp(configuredPercent, 1, 100);
        var livingCreatures = combatState.Creatures.Where(creature => creature.IsAlive).ToList();
        foreach (var creature in livingCreatures)
        {
            if (!creature.IsAlive || creature.CurrentHp >= creature.MaxHp)
                continue;

            var amount = Math.Max(1m, Math.Floor((decimal)creature.MaxHp * percent / 100m));
            await CreatureCmd.Heal(creature, amount);
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
        string warningText)
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
}
