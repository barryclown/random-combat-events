namespace BattlefieldIncidents.Scheduling;

public static class IncidentTimelineGenerator
{
    public static IncidentTimeline Generate(ulong seed, TimelineGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var random = new DeterministicRandom(seed);
        var checkpoints = new List<ScheduledCheckpoint>();
        var turn = 0;
        var occupiedThroughTurn = 0;

        while (true)
        {
            turn += random.NextInt(options.MinimumCheckpointGap, options.MaximumCheckpointGap + 1);
            if (turn > options.MaximumTurn)
                break;

            IncidentKind? kind = null;
            var duration = 0;
            var variant = 0;
            var effectSeed = random.NextUInt64();
            var canSchedule = options.AllowOverlap || turn > occupiedThroughTurn;

            // Keep the checkpoint rhythm untouched but leave the turn empty when there is no room to
            // announce an incident before it lands.
            canSchedule &= turn >= options.FirstTelegraphableTurn;

            if (canSchedule && options.Incidents.Count > 0 &&
                random.NextInt(0, 100) < options.IncidentChancePercent)
            {
                var incident = PickWeighted(ref random, options.Incidents);
                kind = incident.Kind;
                duration = Math.Max(1, incident.Duration);
                variant = random.NextInt(0, 3);
                occupiedThroughTurn = Math.Max(occupiedThroughTurn, turn + duration - 1);
            }

            checkpoints.Add(new ScheduledCheckpoint(turn, kind, duration, variant, effectSeed));
        }

        return new IncidentTimeline(seed, checkpoints);
    }

    private static WeightedIncident PickWeighted(ref DeterministicRandom random,
        IReadOnlyList<WeightedIncident> incidents)
    {
        var totalWeight = incidents.Sum(incident => Math.Max(0, incident.Weight));
        if (totalWeight <= 0)
            throw new InvalidOperationException("At least one incident must have a positive weight.");

        var roll = random.NextInt(0, totalWeight);
        foreach (var incident in incidents)
        {
            roll -= Math.Max(0, incident.Weight);
            if (roll < 0)
                return incident;
        }

        return incidents[^1];
    }

    private static void Validate(TimelineGenerationOptions options)
    {
        if (options.MinimumCheckpointGap < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MinimumCheckpointGap));
        if (options.MaximumCheckpointGap < options.MinimumCheckpointGap)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumCheckpointGap));
        if (options.IncidentChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(options.IncidentChancePercent));
        if (options.MaximumTurn < 1)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumTurn));
    }
}
