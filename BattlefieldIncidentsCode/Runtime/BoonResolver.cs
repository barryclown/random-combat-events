using BattlefieldIncidents.Scheduling;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Looks up the base-game cards a blessing or curse hands out. Cards are matched by model type name
///     at runtime rather than referenced at compile time, so a renamed or removed card degrades into a
///     logged skip instead of a load failure, and cards added by other mods can never be picked by
///     accident: a name that matches more than one model, or one that comes from outside the game's own
///     card namespace, is rejected.
/// </summary>
internal static class BoonResolver
{
    private const string BaseGameCardNamespace = "MegaCrit.Sts2.Core.Models.Cards";

    private static readonly Dictionary<string, CardModel?> Cache = new(StringComparer.Ordinal);

    private static bool _audited;

    /// <summary>
    ///     Runs the audit once, the first time the model database is usable. The card database is not
    ///     built yet while mods initialize, so an audit at that point only measures its own bad timing.
    ///     A failed attempt leaves the flag clear so the next trigger tries again.
    /// </summary>
    internal static void EnsureAudited()
    {
        if (_audited)
            return;

        try
        {
            if (!ModelDb.AllCards.Any())
                return;
        }
        catch (Exception)
        {
            // The database is not ready. A later trigger will run the audit.
            return;
        }

        _audited = true;
        LogCatalogAudit();
    }

    /// <summary>
    ///     Resolves every catalog entry and writes the outcome to the log.
    /// </summary>
    internal static void LogCatalogAudit()
    {
        var missing = new List<string>();
        var resolved = 0;
        var cards = 0;

        foreach (var option in BoonCatalog.Blessings.Concat(BoonCatalog.Curses))
        {
            if (option.Payload != BoonPayload.Card || option.CardTypeName == null)
                continue;

            cards++;
            if (!CanCreate(option))
                missing.Add($"{option.Kind}/{option.Id} ({option.CardTypeName})");
            else
                resolved++;
        }

        MainFile.Logger.Info($"Boon catalog audit: {resolved}/{cards} cards resolved against this game build.");
        if (missing.Count > 0)
            MainFile.Logger.Warn($"Boon catalog entries skipped because their card is unavailable: {string.Join(", ", missing)}");
    }

    /// <summary>
    ///     Whether this entry's card exists on this game build. Deliberately does not build an instance:
    ///     the pool is filtered on every roll, and instantiating a dozen throwaway cards to answer a
    ///     yes/no question is both wasteful and a good way to touch models that should be left alone.
    /// </summary>
    internal static bool CanCreate(BoonOption option) =>
        option.CardTypeName != null && Find(option.CardTypeName) != null;

    /// <summary>
    ///     The canonical model for this entry, or null when the card does not exist on this game build.
    ///     Callers must not clone or mutate it themselves: a combat card is built by handing this to
    ///     <c>CombatState.CreateCard</c>, which is what registers the copy with the combat. Cloning it by
    ///     hand produces a card the game's own pile commands cannot work with.
    /// </summary>
    internal static CardModel? FindCanonical(BoonOption option) =>
        option.CardTypeName == null ? null : Find(option.CardTypeName);

    /// <summary>
    ///     The card's own localized title, so a notice never invents its own name for a base-game card.
    /// </summary>
    internal static string? TitleFor(BoonOption option)
    {
        if (option.CardTypeName == null)
            return null;

        try
        {
            return Find(option.CardTypeName)?.Title;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static CardModel? Find(string typeName)
    {
        if (Cache.TryGetValue(typeName, out var cached))
            return cached;

        CardModel? match = null;
        try
        {
            var candidates = ModelDb.AllCards
                .Where(card => string.Equals(card.GetType().Name, typeName, StringComparison.Ordinal))
                .ToList();

            var fromBaseGame = candidates
                .Where(card => card.GetType().Namespace == BaseGameCardNamespace)
                .ToList();

            match = fromBaseGame.Count switch
            {
                1 => fromBaseGame[0],
                0 when candidates.Count == 0 => null,
                0 => Reject(typeName, "only non-base-game models matched"),
                _ => Reject(typeName, $"{fromBaseGame.Count} base-game models share this name"),
            };
        }
        catch (Exception exception)
        {
            // Do not cache this: the database may simply not be ready yet.
            MainFile.Logger.Warn($"Could not look up card '{typeName}': {exception.Message}");
            return null;
        }

        Cache[typeName] = match;
        return match;
    }

    private static CardModel? Reject(string typeName, string reason)
    {
        MainFile.Logger.Warn($"Ignoring card '{typeName}' for combat events: {reason}.");
        return null;
    }
}
