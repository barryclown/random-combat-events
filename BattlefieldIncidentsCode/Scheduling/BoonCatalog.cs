namespace BattlefieldIncidents.Scheduling;

public enum BoonKind
{
    Blessing,
    Curse,
}

public enum BoonPayload
{
    Card,
    Power,
}

public enum BoonPile
{
    Hand,
    Draw,
    Discard,
}

public enum BoonPlacement
{
    Top,
    Bottom,
    Random,
}

public enum BoonPower
{
    Strength,
    Dexterity,
    Artifact,
    StrengthDown,
    Vulnerable,
    Weak,
    Frail,
}

/// <summary>
///     One thing a blessing or a curse can hand out. Cards name a model type instead of referencing one,
///     so the table stays free of game types and can be checked without the game running.
/// </summary>
public sealed record BoonOption(
    BoonKind Kind,
    BoonPayload Payload,
    string Id,
    int Weight,
    string? CardTypeName = null,
    BoonPile Pile = BoonPile.Hand,
    BoonPlacement Placement = BoonPlacement.Top,
    BoonPower Power = BoonPower.Strength,
    int Amount = 0);

public static class BoonCatalog
{
    /// <summary>
    ///     Neow's Blessing. Cards land in hand so the player can use them the turn they arrive.
    /// </summary>
    public static IReadOnlyList<BoonOption> Blessings { get; } =
    [
        Card(BoonKind.Blessing, "brightest_flame", 8, "BrightestFlame"),
        Card(BoonKind.Blessing, "soul", 8, "Soul"),
        Card(BoonKind.Blessing, "apparition", 6, "Apparition"),
        Card(BoonKind.Blessing, "apotheosis", 3, "Apotheosis"),
        Card(BoonKind.Blessing, "abundance", 7, "Abundance"),
        Card(BoonKind.Blessing, "whistle", 7, "Whistle"),
        Card(BoonKind.Blessing, "dark_shackles", 7, "DarkShackles"),
        Card(BoonKind.Blessing, "production", 7, "Production"),
        Card(BoonKind.Blessing, "panic_button", 6, "PanicButton"),
        Card(BoonKind.Blessing, "the_gambit", 4, "TheGambit"),
        Card(BoonKind.Blessing, "master_of_strategy", 8, "MasterOfStrategy"),
        Card(BoonKind.Blessing, "wish", 8, "Wish"),
        Power(BoonKind.Blessing, "strength", 8, BoonPower.Strength, 2),
        Power(BoonKind.Blessing, "dexterity", 8, BoonPower.Dexterity, 2),
        Power(BoonKind.Blessing, "artifact", 5, BoonPower.Artifact, 1),
    ];

    /// <summary>
    ///     The Architect's Curse. Placement follows one rule: only cards that can be played or discarded
    ///     freely may enter the hand; anything unplayable goes to the draw or discard pile, so a curse can
    ///     never lock the opening hand.
    /// </summary>
    public static IReadOnlyList<BoonOption> Curses { get; } =
    [
        Card(BoonKind.Curse, "burn", 8, "Burn", BoonPile.Hand),
        Card(BoonKind.Curse, "slimed", 8, "Slimed", BoonPile.Hand),
        Card(BoonKind.Curse, "void", 5, "Void", BoonPile.Hand),
        Card(BoonKind.Curse, "dazed", 7, "Dazed", BoonPile.Draw, BoonPlacement.Random),
        Card(BoonKind.Curse, "wound", 7, "Wound", BoonPile.Draw, BoonPlacement.Random),
        Card(BoonKind.Curse, "injury", 6, "Injury", BoonPile.Draw, BoonPlacement.Random),
        Card(BoonKind.Curse, "clumsy", 6, "Clumsy", BoonPile.Draw, BoonPlacement.Random),
        Card(BoonKind.Curse, "decay", 6, "Decay", BoonPile.Discard),
        Card(BoonKind.Curse, "shame", 6, "Shame", BoonPile.Discard),
        Card(BoonKind.Curse, "writhe", 5, "Writhe", BoonPile.Draw, BoonPlacement.Bottom),
        Card(BoonKind.Curse, "regret", 5, "Regret", BoonPile.Discard),
        Card(BoonKind.Curse, "doubt", 5, "Doubt", BoonPile.Discard),
        Power(BoonKind.Curse, "strength_down", 8, BoonPower.StrengthDown, 1),
        Power(BoonKind.Curse, "vulnerable", 8, BoonPower.Vulnerable, 1),
        Power(BoonKind.Curse, "weak", 8, BoonPower.Weak, 1),
        Power(BoonKind.Curse, "frail", 6, BoonPower.Frail, 1),
    ];

    /// <summary>
    ///     Curse cards that cannot be played. These are only ever placed in the draw or discard pile.
    /// </summary>
    public static IReadOnlySet<string> UnplayableCurses { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Dazed", "Wound", "Injury", "Clumsy", "Decay", "Shame", "Writhe", "Regret", "Doubt",
    };

    public static IReadOnlyList<BoonOption> For(BoonKind kind) =>
        kind == BoonKind.Blessing ? Blessings : Curses;

    /// <summary>
    ///     Picks one option using the same weighted draw the incident timeline uses, so a roll replays
    ///     identically from the same seed.
    /// </summary>
    public static BoonOption? Pick(IReadOnlyList<BoonOption> options, ulong seed)
    {
        if (options.Count == 0)
            return null;

        var total = options.Sum(option => Math.Max(0, option.Weight));
        if (total <= 0)
            return null;

        var random = new DeterministicRandom(seed);
        var roll = random.NextInt(0, total);
        foreach (var option in options)
        {
            roll -= Math.Max(0, option.Weight);
            if (roll < 0)
                return option;
        }

        return options[^1];
    }

    /// <summary>
    ///     Rolls the once-per-combat blessing or curse. Both chances share a single roll, so they can never
    ///     both fire and whatever is left over is simply "nothing happens".
    /// </summary>
    public static BoonKind? RollCombatStart(ulong seed, int blessingPercent, int cursePercent)
    {
        var blessing = Math.Clamp(blessingPercent, 0, 100);
        var curse = Math.Clamp(cursePercent, 0, 100 - blessing);

        var random = new DeterministicRandom(seed);
        var roll = random.NextInt(0, 100);
        if (roll < blessing)
            return BoonKind.Blessing;

        return roll < blessing + curse ? BoonKind.Curse : null;
    }

    private static BoonOption Card(
        BoonKind kind,
        string id,
        int weight,
        string cardTypeName,
        BoonPile pile = BoonPile.Hand,
        BoonPlacement placement = BoonPlacement.Top) =>
        new(kind, BoonPayload.Card, id, weight, cardTypeName, pile, placement);

    private static BoonOption Power(BoonKind kind, string id, int weight, BoonPower power, int amount) =>
        new(kind, BoonPayload.Power, id, weight, Power: power, Amount: amount);
}
