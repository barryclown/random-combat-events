using BattlefieldIncidents.Scheduling;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Which monsters a summon event may draw on, built per act from the game's own encounter tables.
///     <para>
///     Scoped to the act the fight is in, so Act 1 can only ever call on Act 1's monsters. A hand-written
///     roster would rot the first time Mega Crit moves a monster between acts, and drawing from the whole
///     game would drop a late-act horror into the opening floor.
///     </para>
/// </summary>
internal static class MonsterPools
{
    private sealed record ActPools(
        IReadOnlyList<MonsterModel> Weak,
        IReadOnlyList<MonsterModel> Normal,
        IReadOnlyList<MonsterModel> Elite);

    private static readonly Dictionary<ActModel, ActPools> Cache = new(ReferenceEqualityComparer.Instance);

    public static IReadOnlyList<MonsterModel> For(ActModel act, SummonTier tier)
    {
        var pools = Build(act);
        return tier switch
        {
            SummonTier.Weak => pools.Weak,
            SummonTier.Elite => pools.Elite,
            _ => pools.Normal,
        };
    }

    /// <summary>Builds every act's pools up front, so a bad exclusion shows up in the log at load.</summary>
    public static void Warm()
    {
        foreach (var act in ModelDb.Acts)
            Build(act);
    }

    public static void Invalidate() => Cache.Clear();

    private static ActPools Build(ActModel act)
    {
        if (Cache.TryGetValue(act, out var cached))
            return cached;

        // Bosses are barred outright: one dropped into an ordinary fight is not a challenge, it is the
        // end of the run.
        var bosses = Names(act.AllBossEncounters);
        var weak = Collect(act.AllWeakEncounters, bosses);
        var regular = Collect(act.AllRegularEncounters, bosses);
        var elite = Collect(act.AllEliteEncounters, bosses);

        // A monster that also turns up as an Elite belongs to the harder tier, never the easy one.
        var eliteNames = elite.Select(monster => monster.GetType().Name).ToHashSet(StringComparer.Ordinal);
        weak = weak.Where(monster => !eliteNames.Contains(monster.GetType().Name)).ToList();
        regular = regular.Where(monster => !eliteNames.Contains(monster.GetType().Name)).ToList();

        // "Normal" means anything an ordinary fight could throw at you, which includes the small fry.
        var normal = weak.Concat(regular)
            .GroupBy(monster => monster.GetType().Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(monster => monster.GetType().Name, StringComparer.Ordinal)
            .ToList();

        // Some acts have no encounters flagged weak. Rather than leave the free summon with nothing to
        // draw, fall back to whatever is small enough to pass for small fry.
        if (weak.Count == 0)
        {
            weak = normal
                .Where(monster => SummonCatalog.TierForHp(monster.MaxInitialHp, false) == SummonTier.Weak)
                .ToList();
        }

        var pools = new ActPools(weak, normal, elite);
        Cache[act] = pools;
        MainFile.Logger.Info(
            $"Summon pools for {act.GetType().Name}: {weak.Count} weak, {normal.Count} normal, {elite.Count} elite.");
        return pools;
    }

    private static HashSet<string> Names(IEnumerable<EncounterModel> encounters) =>
        encounters
            .Where(encounter => !encounter.IsMock)
            .SelectMany(encounter => encounter.AllPossibleMonsters)
            .Select(monster => monster.GetType().Name)
            .ToHashSet(StringComparer.Ordinal);

    private static List<MonsterModel> Collect(
        IEnumerable<EncounterModel> encounters,
        IReadOnlySet<string> barred)
    {
        var found = new Dictionary<string, MonsterModel>(StringComparer.Ordinal);
        foreach (var encounter in encounters)
        {
            if (encounter.IsMock)
                continue;

            foreach (var monster in encounter.AllPossibleMonsters)
            {
                if (monster.IsMock)
                    continue;

                var name = monster.GetType().Name;
                if (barred.Contains(name) || !SummonCatalog.IsUsableAsSummon(name))
                    continue;

                found[name] = monster;
            }
        }

        return found.Values.OrderBy(monster => monster.GetType().Name, StringComparer.Ordinal).ToList();
    }
}
