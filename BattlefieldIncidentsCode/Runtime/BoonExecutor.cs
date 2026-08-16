using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Hands out one blessing or curse. Both the combat-start roll and the scheduled route event use this,
///     so the two entry points can never drift apart in what they actually do.
/// </summary>
internal static class BoonExecutor
{
    /// <summary>
    ///     Picks and applies one option. Returns the option that fired so the caller can describe it, or
    ///     null when nothing usable was available on this game build.
    /// </summary>
    internal static async Task<BoonOption?> Apply(
        BoonKind kind,
        ulong seed,
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        Player player)
    {
        var pool = BoonCatalog.For(kind);

        // A card whose model is missing on this build would silently do nothing, so drop those first and
        // let the weighted draw pick among what can actually be handed out.
        var usable = pool
            .Where(option => option.Payload != BoonPayload.Card || BoonResolver.CanCreate(option))
            .ToList();

        var chosen = BoonCatalog.Pick(usable, seed);
        if (chosen == null)
            return null;

        if (chosen.Payload == BoonPayload.Card)
            return await GiveCardToEveryone(chosen, combatState, player);

        await ApplyPower(chosen, choiceContext, combatState);
        return chosen;
    }

    /// <summary>
    ///     Deals the same card to every seat. The powers already land on the whole player side, and a
    ///     blessing that only reached whichever player the game happened to ask first would turn a shared
    ///     route into a lottery about turn order.
    /// </summary>
    private static async Task<BoonOption?> GiveCardToEveryone(
        BoonOption option,
        CombatState combatState,
        Player asked)
    {
        BoonOption? given = null;
        foreach (var player in Party.Members(combatState))
            given = await GiveCard(option, combatState, player) ?? given;

        // Falling back to the one player we were handed keeps single-player behaviour identical even if
        // the roster came back empty for a reason we have not thought of.
        return given ?? await GiveCard(option, combatState, asked);
    }

    /// <summary>
    ///     Builds the card through the combat's own factory and lets the game place it. The factory step
    ///     is what registers the card with this combat; a hand-made clone reaches the pile command in a
    ///     state it cannot use.
    /// </summary>
    private static async Task<BoonOption?> GiveCard(BoonOption option, CombatState combatState, Player player)
    {
        var canonical = BoonResolver.FindCanonical(option);
        if (canonical == null)
            return null;

        var card = combatState.CreateCard(canonical, player);
        var pile = MapPile(option.Pile);
        var result = await CardPileCmd.AddGeneratedCardToCombat(card, pile, player, MapPlacement(option.Placement));
        if (!result.success)
        {
            MainFile.Logger.Warn($"The game refused to place {option.CardTypeName} into {pile}.");
            return null;
        }

        // Face-down piles only refresh their count once told the addition finished.
        var actualPile = card.Pile?.Type ?? pile;
        if (actualPile is MegaCrit.Sts2.Core.Entities.Cards.PileType.Draw
            or MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard
            or MegaCrit.Sts2.Core.Entities.Cards.PileType.Exhaust)
        {
            card.Pile?.InvokeCardAddFinished();
        }

        return option;
    }

    internal static MegaCrit.Sts2.Core.Entities.Cards.PileType MapPile(BoonPile pile) => pile switch
    {
        BoonPile.Draw => MegaCrit.Sts2.Core.Entities.Cards.PileType.Draw,
        BoonPile.Discard => MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard,
        _ => MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand,
    };

    internal static MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition MapPlacement(BoonPlacement placement) => placement switch
    {
        BoonPlacement.Bottom => MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Bottom,
        BoonPlacement.Random => MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Random,
        _ => MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition.Top,
    };

    private static async Task ApplyPower(
        BoonOption option,
        PlayerChoiceContext choiceContext,
        CombatState combatState)
    {
        var targets = combatState.PlayerCreatures.Where(creature => creature.IsHittable).ToList();
        if (targets.Count == 0)
            return;

        var amount = Math.Max(1, option.Amount);
        switch (option.Power)
        {
            case BoonPower.Strength:
                await PowerCmd.Apply<StrengthPower>(choiceContext, targets, amount, null, null);
                break;
            case BoonPower.Dexterity:
                await PowerCmd.Apply<DexterityPower>(choiceContext, targets, amount, null, null);
                break;
            case BoonPower.Artifact:
                await PowerCmd.Apply<ArtifactPower>(choiceContext, targets, amount, null, null);
                break;
            case BoonPower.StrengthDown:
                await PowerCmd.Apply<StrengthPower>(choiceContext, targets, -amount, null, null);
                break;
            case BoonPower.Vulnerable:
                await PowerCmd.Apply<VulnerablePower>(choiceContext, targets, amount, null, null);
                break;
            case BoonPower.Weak:
                await PowerCmd.Apply<WeakPower>(choiceContext, targets, amount, null, null);
                break;
            case BoonPower.Frail:
                await PowerCmd.Apply<FrailPower>(choiceContext, targets, amount, null, null);
                break;
        }
    }

    /// <summary>
    ///     Rolls the once-per-combat blessing/curse. The two chances share one roll, so they can never both
    ///     fire and the remainder is simply "nothing happens".
    /// </summary>
    internal static BoonKind? RollCombatStart(ulong seed, IncidentSettings settings) =>
        settings.EnableCombatStartBoons
            ? BoonCatalog.RollCombatStart(
                seed, settings.CombatStartBlessingPercent, settings.CombatStartCursePercent)
            : null;

    internal static bool IsCombatStartEnabledFor(CombatState combatState, IncidentSettings settings) =>
        combatState.Encounter?.RoomType switch
        {
            MegaCrit.Sts2.Core.Rooms.RoomType.Monster => settings.EnableCombatStartNormalCombats,
            MegaCrit.Sts2.Core.Rooms.RoomType.Elite => settings.EnableCombatStartEliteCombats,
            MegaCrit.Sts2.Core.Rooms.RoomType.Boss => settings.EnableCombatStartBossCombats,
            _ => false,
        };
}
