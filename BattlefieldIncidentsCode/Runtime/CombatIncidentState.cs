using BattlefieldIncidents.Scheduling;
using MegaCrit.Sts2.Core.Combat;

namespace BattlefieldIncidents.Runtime;

internal sealed class CombatIncidentState
{
    public required IncidentTimeline Timeline { get; init; }
    public required ActTheme ActTheme { get; init; }
    public required bool EnabledForCombat { get; init; }
    public int LastProcessedRound { get; set; }
    public bool CombatStartResolved { get; set; }
    public HashSet<int> WarnedCheckpointTurns { get; } = [];
    public Dictionary<int, decimal> PendingRockfalls { get; } = [];
    public Dictionary<PendingSideDamageKey, PendingSideDamage> PendingSideDamages { get; } = [];
    public Dictionary<int, ActiveToxicFog> ActiveToxicFogs { get; } = [];
    public Dictionary<int, IncidentWarningNotice> WarningNotices { get; } = [];

    /// <summary>
    ///     Notices that describe what happened on a given round. They are closed when the next round
    ///     begins rather than on a timer, so the screen always shows the current round and nothing older.
    /// </summary>
    public Dictionary<int, List<IncidentWarningNotice>> RoundNotices { get; } = [];
}

internal readonly record struct PendingSideDamageKey(int SourceTurn, int Round, IncidentKind Kind);

internal sealed class PendingSideDamage
{
    public required IncidentKind Kind { get; init; }
    public required int SourceTurn { get; init; }
    public required int Round { get; init; }
    public required decimal Damage { get; init; }
    public required int Hits { get; init; }
    public required int Wave { get; init; }
    public required int Duration { get; init; }
    public required IncidentWarningNotice Notice { get; init; }
    public bool PlayerResolved { get; private set; }
    public bool EnemyResolved { get; private set; }
    public bool IsComplete => PlayerResolved && EnemyResolved;

    public bool TryConsume(CombatSide side)
    {
        switch (side)
        {
            case CombatSide.Player when !PlayerResolved:
                PlayerResolved = true;
                return true;
            case CombatSide.Enemy when !EnemyResolved:
                EnemyResolved = true;
                return true;
            default:
                return false;
        }
    }
}

internal sealed record ActiveToxicFog(int PoisonPerHit, IncidentWarningNotice Notice);
