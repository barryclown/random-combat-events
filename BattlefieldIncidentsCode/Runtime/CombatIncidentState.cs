using BattlefieldIncidents.Scheduling;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

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

    public bool MiraclesResolved { get; set; }

    /// <summary>
    ///     Units still holding an unspent Last Miracle, with the notice that told the player about it.
    ///     The notice stays up for as long as the charge does, because it describes a condition of the
    ///     battlefield rather than something that happened on one turn.
    /// </summary>
    public HashSet<Creature> MiracleCharges { get; } = new(ReferenceEqualityComparer.Instance);

    public Dictionary<Creature, IncidentWarningNotice> MiracleNotices { get; } =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>Monsters fighting on the player's side. Driven by us; the game will not move them.</summary>
    public List<Creature> Allies { get; } = [];

    /// <summary>Every monster this mod brought in, whichever side it took. Gates the next summon.</summary>
    public List<Creature> Summons { get; } = [];

    /// <summary>The turn each summon arrived, so it is never asked to leave on the turn it showed up.</summary>
    public Dictionary<Creature, int> SummonArrivals { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Contracts agreed on one turn and paid for on the next, keyed by the turn they settle.</summary>
    public Dictionary<int, PendingMercenary> PendingMercenaries { get; } = [];

    /// <summary>How much extra the fight ended up carrying, which is what the spoils are scaled to.</summary>
    public int ExtraNormalMonsters { get; set; }

    public int ExtraEliteMonsters { get; set; }

    /// <summary>
    ///     What the Ancients promised for a later turn, keyed by the turn it comes due. Three of the
    ///     seven pay out next turn rather than now, which is the whole of their cost: you commit before
    ///     you know what you will be holding.
    /// </summary>
    public Dictionary<int, PioneerDeferral> Deferrals { get; } = [];

    /// <summary>Wax relics handed out this fight. They melt when it ends, however it ends.</summary>
    public List<MegaCrit.Sts2.Core.Models.RelicModel> WaxRelics { get; } = [];

    /// <summary>Gold Nonupeipe owes the party once the fight is actually won.</summary>
    public int PromisedGold { get; set; }

    /// <summary>The vines, and who they have hold of. Co-op only; null in a single-player fight.</summary>
    public StranglingVinesState? Vines { get; set; }

    public bool VinesResolved { get; set; }

    public PioneerDeferral DeferralFor(int turn)
    {
        if (Deferrals.TryGetValue(turn, out var existing))
            return existing;

        var created = new PioneerDeferral();
        Deferrals[turn] = created;
        return created;
    }
}

/// <summary>An Ancient's promise, waiting for the turn it was made about.</summary>
internal sealed class PioneerDeferral
{
    /// <summary>Vakuu plays the hand and burns it.</summary>
    public bool Takeover { get; set; }

    /// <summary>Darv fills the hand and scrambles the prices.</summary>
    public bool Gamble { get; set; }

    /// <summary>Pael's energy and cards.</summary>
    public int Energy { get; set; }

    public int Draw { get; set; }

    /// <summary>Drives every roll the payout needs, so a reload cannot shop for a better one.</summary>
    public ulong Seed { get; set; }

    public bool IsEmpty => !Takeover && !Gamble && Energy <= 0 && Draw <= 0;
}

/// <summary>
///     One player wrapped in vines, and the thing holding them. Killing the vines frees the player; so
///     does waiting long enough, which is what keeps a table from stalling when nobody can reach them.
/// </summary>
internal sealed class StranglingVinesState
{
    public required Creature Vines { get; init; }
    public required Player Victim { get; init; }
    public required int ReleaseTurn { get; init; }
    public IncidentWarningNotice? Notice { get; set; }
    public bool Released { get; set; }
}

internal readonly record struct PendingSideDamageKey(int SourceTurn, int Round, IncidentKind Kind);

internal sealed class PendingSideDamage
{
    public required IncidentKind Kind { get; init; }
    public required int SourceTurn { get; init; }
    public required int Round { get; init; }
    public required decimal Damage { get; init; }

    /// <summary>
    ///     When above zero the hit is a share of each unit's own max HP instead of the flat
    ///     <see cref="Damage" />, so one number cannot be a scratch on a Boss and a death sentence on a
    ///     minion. <see cref="PlayerDamage" /> is what that works out to for the local player, snapshot
    ///     when the event is armed so the notice and the hit can never disagree.
    /// </summary>
    public int DamagePercent { get; init; }

    public decimal PlayerDamage { get; init; }

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
