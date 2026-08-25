using BattlefieldIncidents.Scheduling;

namespace BattlefieldIncidents.Settings;

public enum IncidentPreset
{
    Casual,
    Standard,
    Chaos,
    Custom,

    // Appended rather than slotted in beside the other modes: the saved settings file stores this enum
    // as a number, so inserting anything above Custom would silently turn a saved Custom into Hell.
    Hell,
}

public sealed class IncidentSettings
{
    public bool Enabled { get; set; } = true;
    // New installs start with the faster Chaos preset; players can switch to a calmer mode in settings.
    public IncidentPreset Preset { get; set; } = IncidentPreset.Chaos;
    public int MinimumCheckpointGap { get; set; } = 1;
    public int MaximumCheckpointGap { get; set; } = 3;
    public int IncidentChancePercent { get; set; } = 80;
    public int MaximumScheduledTurn { get; set; } = 100;
    public int WarningTurns { get; set; } = 1;
    public bool AllowOverlap { get; set; } = true;

    public bool EnableNormalCombats { get; set; } = true;
    public bool EnableEliteCombats { get; set; } = true;
    public bool EnableBossCombats { get; set; } = true;

    public bool EnableRockfall { get; set; } = true;
    public int RockfallWeight { get; set; } = 10;
    public int RockfallDamage { get; set; } = 5;

    public bool EnableSwordRain { get; set; } = true;
    public int SwordRainWeight { get; set; } = 10;
    public int SwordRainDamagePerHit { get; set; } = 1;
    public int SwordRainHitCount { get; set; } = 3;

    public bool EnableToxicFog { get; set; } = true;
    public int ToxicFogWeight { get; set; } = 8;
    public int ToxicFogPoisonPerHit { get; set; } = 1;

    public bool EnableGentleRain { get; set; } = true;
    public int GentleRainWeight { get; set; } = 14;
    public int GentleRainHealPercent { get; set; } = 3;

    /// <summary>
    ///     The least the rain may heal a player for, whatever the percentage works out to. Without it
    ///     the event pays the enemy side more than it pays you, because they usually have more total
    ///     health between them.
    /// </summary>
    public int GentleRainPlayerMinimumHeal { get; set; } = 10;

    // The laser is filed under the helpful events because it charges every unit the same share of its
    // own health, and the player is the one side that can Block it and pick who it finishes off.
    public bool EnableLaser { get; set; } = true;
    public int LaserWeight { get; set; } = 10;
    public int LaserHpPercent { get; set; } = 5;

    public bool EnableVineSnare { get; set; } = true;
    public int VineSnareWeight { get; set; } = 12;
    public int VineSnareVulnerable { get; set; } = 1;

    public bool EnableDampSeaWind { get; set; } = true;
    public int DampSeaWindWeight { get; set; } = 12;
    public int DampSeaWindWeak { get; set; } = 1;
    public int DampSeaWindFrail { get; set; } = 1;

    public bool EnableHiveOnslaught { get; set; } = true;
    public int HiveOnslaughtWeight { get; set; } = 12;
    public int HiveOnslaughtDamage { get; set; } = 5;
    public int HiveOnslaughtDuration { get; set; } = 3;

    public bool EnableNeowsBlessing { get; set; } = true;
    public int NeowsBlessingWeight { get; set; } = 10;

    public bool EnableArchitectsCurse { get; set; } = true;
    public int ArchitectsCurseWeight { get; set; } = 10;

    // The other seven Ancients. Neow was here first because the opening blessing needed a name; these
    // are the rest of the pantheon, each one an event with the flavour of the shrine it comes from.

    /// <summary>Vakuu plays your next hand for you, then burns it. Asked before it happens.</summary>
    public bool EnableVakuu { get; set; } = true;

    public int VakuuWeight { get; set; } = 8;

    /// <summary>Darv fills your hand next turn and scrambles what everything costs. Asked first.</summary>
    public bool EnableDarv { get; set; } = true;

    public int DarvWeight { get; set; } = 8;

    /// <summary>Nonupeipe hands out one of three gifts, drawn on its own weights.</summary>
    public bool EnableNonupeipe { get; set; } = true;

    public int NonupeipeWeight { get; set; } = 10;
    public int NonupeipeMaxHpWeight { get; set; } = 10;
    public int NonupeipeGoldWeight { get; set; } = 10;
    public int NonupeipeMarkedRoomWeight { get; set; } = 10;
    public int NonupeipeMaxHp { get; set; } = 2;
    public int NonupeipeGold { get; set; } = 30;

    /// <summary>Tanx lends a weapon, good for this turn only.</summary>
    public bool EnableTanx { get; set; } = true;

    public int TanxWeight { get; set; } = 10;

    /// <summary>Tezcatara's wax relic works for one fight and is slag by the end of it.</summary>
    public bool EnableTezcatara { get; set; } = true;

    public int TezcataraWeight { get; set; } = 8;

    /// <summary>Pael's small mercy, paid out at the start of the next turn.</summary>
    public bool EnablePael { get; set; } = true;

    public int PaelWeight { get; set; } = 10;
    public int PaelEnergy { get; set; } = 1;
    public int PaelCards { get; set; } = 1;

    /// <summary>Orobas offers cards from the other disciplines, free for the turn they arrive.</summary>
    public bool EnableOrobas { get; set; } = true;

    public int OrobasWeight { get; set; } = 8;
    public int OrobasChoices { get; set; } = 3;

    // Co-op. None of this does anything in a single-player run.

    /// <summary>
    ///     Opens a co-op fight with one player wrapped in vines: they cannot play cards until someone
    ///     else cuts the vines down. Rolled at combat start, so it never enters the route's weights.
    /// </summary>
    public bool EnableStranglingVines { get; set; } = true;

    public int StranglingVinesChancePercent { get; set; } = 25;

    /// <summary>How long the vines hold if nobody frees the player. Never let a table deadlock.</summary>
    public int StranglingVinesEscapeTurns { get; set; } = 3;

    /// <summary>
    ///     What each player pays for a priced offer in co-op, as a share of the quoted price. Four
    ///     regardless of how many are actually at the table: a full party should feel like it can afford
    ///     to say yes, and a pair splitting a full price never would.
    /// </summary>
    public int MultiplayerPriceDivisor { get; set; } = 4;

    // Summon events bring another monster into the fight. The existing summon prices are halved again
    // at the user's request; the odd 25-gold tier rounds to 13 because gold is an integer.
    public bool EnableFreeSummon { get; set; } = true;
    public int FreeSummonWeight { get; set; } = 5;
    public int FreeSummonAllyPercent { get; set; } = 50;

    public bool EnableMercenary { get; set; } = true;
    public int MercenaryWeight { get; set; } = 5;
    public int MercenaryPrice { get; set; } = 13;
    public int MercenaryRunOffPercent { get; set; } = 5;
    public int MercenaryBetrayalPercent { get; set; } = 5;

    public bool EnableEnemyRecruit { get; set; } = true;
    public int EnemyRecruitWeight { get; set; } = 5;
    public int StandDownPrice { get; set; } = 19;
    public int HirePrice { get; set; } = 25;
    public int RecruitFailurePercent { get; set; } = 5;

    public bool EnableChallenge { get; set; } = true;
    public int ChallengeWeight { get; set; } = 4;
    public int ChallengeUpsetPercent { get; set; } = 5;
    public int ChallengeElitePercent { get; set; } = 50;

    /// <summary>
    ///     Chance each turn that a summoned monster loses interest and walks away. Applies to both sides:
    ///     nothing that wandered into a fight has a reason to see it through.
    /// </summary>
    public int SummonDepartureChancePercent { get; set; } = 15;

    // Spoils for the monsters a summon event added, built to the same recipe the game uses for a room:
    // gold, a card to choose from, a potion on a roll, and a relic for anything Elite-sized.
    /// <summary>Middle of the gold pile an extra ordinary monster is worth.</summary>
    public int ExtraMonsterGold { get; set; } = 30;

    /// <summary>Middle of the gold pile an extra Elite is worth.</summary>
    public int ExtraEliteGold { get; set; } = 60;

    /// <summary>Chance an extra monster also leaves a potion behind.</summary>
    public int ExtraPotionPercent { get; set; } = 40;

    /// <summary>
    ///     Chance an extra ordinary monster also leaves a relic. Elites always leave one, the same way
    ///     an Elite room always does.
    /// </summary>
    public int ExtraRelicPercent { get; set; } = 10;

    /// <summary>Whether an extra monster is also worth a card to choose from.</summary>
    public bool ExtraCardReward { get; set; } = true;

    // The Last Miracle is a standing rule for the whole combat, not a stop on the route, so it is
    // rolled per unit at combat start and carries its own chances instead of a pool weight.
    public bool EnableMiracle { get; set; } = true;
    public int PlayerMiracleChancePermille { get; set; } = 10;
    public int EnemyMiracleChancePermille { get; set; } = 5;

    // Combat-start boons roll once per combat, independently of the turn route above.
    public bool EnableCombatStartBoons { get; set; } = true;
    public int CombatStartBlessingPercent { get; set; } = 33;
    // Standard is the default preset, so a fresh Workshop install opens on a gift-or-nothing roll.
    public int CombatStartCursePercent { get; set; } = 0;
    public bool EnableCombatStartNormalCombats { get; set; } = true;
    public bool EnableCombatStartEliteCombats { get; set; } = true;
    public bool EnableCombatStartBossCombats { get; set; } = true;

    public void ApplyPreset(IncidentPreset preset)
    {
        Preset = preset;
        switch (preset)
        {
            case IncidentPreset.Casual:
                MinimumCheckpointGap = 5;
                MaximumCheckpointGap = 7;
                IncidentChancePercent = 35;
                WarningTurns = 2;
                ApplyOrdinaryMix();
                break;
            case IncidentPreset.Standard:
                MinimumCheckpointGap = 3;
                MaximumCheckpointGap = 5;
                IncidentChancePercent = 50;
                WarningTurns = 1;
                ApplyOrdinaryMix();
                break;
            case IncidentPreset.Chaos:
                MinimumCheckpointGap = 1;
                MaximumCheckpointGap = 3;
                IncidentChancePercent = 80;
                WarningTurns = 1;
                ApplyOrdinaryMix();
                break;
            case IncidentPreset.Hell:
                MinimumCheckpointGap = 2;
                MaximumCheckpointGap = 4;
                IncidentChancePercent = 75;
                WarningTurns = 1;
                // Every fight opens on a curse, and the events that were helping you dry up.
                CombatStartBlessingPercent = 0;
                CombatStartCursePercent = 100;
                GentleRainWeight = 3;
                NeowsBlessingWeight = 2;
                LaserWeight = 3;
                // Hell keeps the summon events, but the helpful half of them dries up too.
                FreeSummonAllyPercent = 20;
                // The Ancients are gifts almost to a one, so the shrines go quiet here as well.
                ApplyPioneerMix(2);
                break;
            case IncidentPreset.Custom:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
        }
    }

    /// <summary>
    ///     What the three ordinary modes share. Players were finding runs hard to finish, and an opening
    ///     curse stacked on top of a scheduled route was the sharpest edge of that, so outside Hell the
    ///     opening roll can only ever hand out a gift.
    /// </summary>
    private void ApplyOrdinaryMix()
    {
        CombatStartBlessingPercent = 33;
        CombatStartCursePercent = 0;
        GentleRainWeight = 14;
        NeowsBlessingWeight = 10;
        LaserWeight = 10;
        FreeSummonAllyPercent = 50;
        ApplyPioneerMix(1);
    }

    /// <summary>
    ///     The Ancients' weights, written out in full for every preset. Leaving any of them unset would
    ///     let a value from Hell survive a switch back to Standard, which is how a "helpful" mode ends up
    ///     quietly stingy.
    /// </summary>
    /// <param name="tier">1 for the ordinary modes, 2 for the thin version Hell uses.</param>
    private void ApplyPioneerMix(int tier)
    {
        var ordinary = tier == 1;
        VakuuWeight = ordinary ? 8 : 2;
        DarvWeight = ordinary ? 8 : 2;
        NonupeipeWeight = ordinary ? 10 : 2;
        TanxWeight = ordinary ? 10 : 2;
        TezcataraWeight = ordinary ? 8 : 2;
        PaelWeight = ordinary ? 10 : 2;
        OrobasWeight = ordinary ? 8 : 2;
    }

    public TimelineGenerationOptions ToTimelineOptions(ActTheme actTheme)
    {
        var minimumGap = Math.Clamp(MinimumCheckpointGap, 1, 100);
        var maximumGap = Math.Clamp(MaximumCheckpointGap, minimumGap, 100);
        var maximumTurn = Math.Clamp(MaximumScheduledTurn, 1, 100);

        return new TimelineGenerationOptions(
            minimumGap,
            maximumGap,
            Math.Clamp(IncidentChancePercent, 0, 100),
            maximumTurn,
            actTheme,
            AllowOverlap,
            BuildIncidentWeights(actTheme),
            Math.Max(0, WarningTurns) + 1);
    }

    private IReadOnlyList<WeightedIncident> BuildIncidentWeights(ActTheme actTheme)
    {
        var incidents = new List<WeightedIncident>();
        Add(EnableRockfall, IncidentKind.Rockfall, RockfallWeight, duration: 1);
        Add(EnableSwordRain, IncidentKind.SwordRain, SwordRainWeight, duration: 1);
        Add(EnableToxicFog, IncidentKind.ToxicFog, ToxicFogWeight, duration: 1);
        Add(EnableGentleRain, IncidentKind.GentleRain, GentleRainWeight, duration: 1);
        Add(EnableLaser, IncidentKind.Laser, LaserWeight, duration: 1);
        Add(EnableFreeSummon, IncidentKind.FreeSummon, FreeSummonWeight, duration: 1);
        Add(EnableMercenary, IncidentKind.Mercenary, MercenaryWeight, duration: 1);
        Add(EnableEnemyRecruit, IncidentKind.EnemyRecruit, EnemyRecruitWeight, duration: 1);
        Add(EnableChallenge, IncidentKind.Challenge, ChallengeWeight, duration: 1);
        Add(EnableNeowsBlessing, IncidentKind.NeowsBlessing, NeowsBlessingWeight, duration: 1);
        Add(EnableArchitectsCurse, IncidentKind.ArchitectsCurse, ArchitectsCurseWeight, duration: 1);
        Add(EnableVakuu, IncidentKind.VakuusTakeover, VakuuWeight, duration: 1);
        Add(EnableDarv, IncidentKind.DarvsGamble, DarvWeight, duration: 1);
        Add(EnableNonupeipe, IncidentKind.NonupeipesGift, NonupeipeWeight, duration: 1);
        Add(EnableTanx, IncidentKind.TanxsArmory, TanxWeight, duration: 1);
        Add(EnableTezcatara, IncidentKind.TezcatarasEmber, TezcataraWeight, duration: 1);
        Add(EnablePael, IncidentKind.PaelsBlessing, PaelWeight, duration: 1);
        Add(EnableOrobas, IncidentKind.OrobassOffer, OrobasWeight, duration: 1);

        switch (actTheme)
        {
            case ActTheme.Overgrowth:
                Add(EnableVineSnare, IncidentKind.VineSnare, VineSnareWeight, duration: 1);
                break;
            case ActTheme.Underdocks:
                Add(EnableDampSeaWind, IncidentKind.DampSeaWind, DampSeaWindWeight, duration: 1);
                break;
            case ActTheme.Hive:
                Add(EnableHiveOnslaught, IncidentKind.HiveOnslaught, HiveOnslaughtWeight,
                    Math.Clamp(HiveOnslaughtDuration, 1, 10));
                break;
        }

        return incidents;

        void Add(bool enabled, IncidentKind kind, int weight, int duration)
        {
            if (enabled && weight > 0)
                incidents.Add(new WeightedIncident(kind, Math.Clamp(weight, 1, 100), duration));
        }
    }
}
