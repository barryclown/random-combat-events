using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     The seven Ancients that are not Neow, as events in a fight.
///     <para>
///     Every one of them settles for the whole table rather than for whichever player the game handed
///     our hook first. That is not a co-op nicety: a gift that lands on one seat and skips the others
///     turns a shared route into a lottery about turn order.
///     </para>
/// </summary>
internal static class PioneerEvents
{
    private const string RngName = "rce_pioneer";

    /// <summary>
    ///     Runs one Ancient's event and returns the line that describes what happened, or null when the
    ///     shrine had nothing to give on this build.
    /// </summary>
    public static async Task<string?> ResolveAsync(
        IncidentKind kind,
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        int round,
        ulong seed) => kind switch
    {
        IncidentKind.VakuusTakeover => await OfferTakeoverAsync(combatState, runtime, round, seed),
        IncidentKind.DarvsGamble => await OfferGambleAsync(combatState, runtime, round, seed),
        IncidentKind.NonupeipesGift => await GiveGiftAsync(combatState, runtime, settings, seed),
        IncidentKind.TanxsArmory => await LendWeaponAsync(combatState, seed),
        IncidentKind.TezcatarasEmber => await GiveWaxRelicAsync(combatState, runtime),
        IncidentKind.PaelsBlessing => Promise(runtime, settings, round, seed),
        IncidentKind.OrobassOffer => await OfferForeignCardAsync(choiceContext, combatState, settings, seed),
        _ => null,
    };

    // ---- Vakuu -------------------------------------------------------------------------------------

    /// <summary>
    ///     Asked now, paid next turn. Committing before the hand is drawn is the entire decision: you
    ///     are betting that whatever you are about to hold is worth more played blind than played well.
    /// </summary>
    private static async Task<string?> OfferTakeoverAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        int round,
        ulong seed)
    {
        var accepted = await IncidentDialog.AskPartyAsync(
            combatState, seed,
            IncidentText.IncidentTitle(IncidentKind.VakuusTakeover),
            IncidentText.VakuuOffer(),
            IncidentText.DialogYield(),
            IncidentText.DialogKeepControl());
        if (!accepted)
            return IncidentText.VakuuDeclined();

        var deferral = runtime.DeferralFor(round + 1);
        deferral.Takeover = true;
        deferral.Seed = seed;
        return IncidentText.VakuuAccepted();
    }

    /// <summary>
    ///     Plays the hand, in the order it sits, and burns whatever it touched. Cards that cannot be
    ///     played are burned too — Vakuu does not read the rules, and clearing a Burn out of your hand is
    ///     the closest this event comes to an apology.
    /// </summary>
    private static async Task<int> RunTakeoverAsync(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        Player player,
        ulong seed)
    {
        var hand = PileType.Hand.GetPile(player);
        if (hand == null)
            return 0;

        var played = 0;
        foreach (var card in hand.Cards.ToList())
        {
            if (player.Creature.IsDead || CombatManager.Instance.IsOverOrEnding)
                break;

            // Re-checked every time round: the pile shifts under us as cards resolve.
            if (card.Pile?.Type != PileType.Hand)
                continue;

            if (!card.CanPlay())
            {
                await CardCmd.Exhaust(choiceContext, card);
                played++;
                continue;
            }

            var target = PickTarget(combatState, seed, played);
            await CardPileCmd.Add(card, PileType.Play);
            card.ExhaustOnNextPlay = true;
            await CardCmd.AutoPlay(choiceContext, card, target);
            played++;
        }

        return played;
    }

    /// <summary>
    ///     Something for a targeted card to point at. Drawn from the combat seed rather than live, so a
    ///     reload aims the same blow at the same monster.
    /// </summary>
    private static Creature? PickTarget(CombatState combatState, ulong seed, int index)
    {
        var enemies = combatState.Creatures
            .Where(creature => creature.Side == CombatSide.Enemy && creature.IsHittable)
            .ToList();
        if (enemies.Count == 0)
            return null;

        var pick = SummonRoll.PickIndex(enemies.Count, seed ^ ((ulong)(uint)(index + 1) * 0x9E3779B97F4A7C15UL));
        return enemies[Math.Clamp(pick, 0, enemies.Count - 1)];
    }

    // ---- Darv --------------------------------------------------------------------------------------

    private static async Task<string?> OfferGambleAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        int round,
        ulong seed)
    {
        var accepted = await IncidentDialog.AskPartyAsync(
            combatState, seed,
            IncidentText.IncidentTitle(IncidentKind.DarvsGamble),
            IncidentText.DarvOffer(),
            IncidentText.DialogTakeTheDeal(),
            IncidentText.DialogWalkAway());
        if (!accepted)
            return IncidentText.DarvDeclined();

        var deferral = runtime.DeferralFor(round + 1);
        deferral.Gamble = true;
        deferral.Seed = seed;
        return IncidentText.DarvAccepted();
    }

    private static async Task<int> RunGambleAsync(PlayerChoiceContext choiceContext, Player player)
    {
        var hand = PileType.Hand.GetPile(player);
        var room = Math.Max(0, CardPile.MaxCardsInHand - (hand?.Cards.Count ?? 0));
        if (room > 0)
            await CardPileCmd.Draw(choiceContext, room, player);

        // Applied after the draw, so the cards Darv just handed over keep their honest prices and only
        // what comes afterwards is scrambled.
        await PowerCmd.Apply<ConfusedPower>(choiceContext, [player.Creature], 1, null, null);
        return room;
    }

    // ---- Nonupeipe ---------------------------------------------------------------------------------

    private static async Task<string?> GiveGiftAsync(
        CombatState combatState,
        CombatIncidentState runtime,
        IncidentSettings settings,
        ulong seed)
    {
        var gift = PioneerRoll.Gift(seed, settings.NonupeipeMaxHpWeight, settings.NonupeipeGoldWeight,
            settings.NonupeipeMarkedRoomWeight);
        if (gift == null)
        {
            MainFile.Logger.Info("Nonupeipe rolled, but every one of her gifts is switched off.");
            return null;
        }

        var members = Party.Members(combatState);
        switch (gift.Value)
        {
            case NonupeipeGift.MaxHp:
                foreach (var player in members.Where(player => player.Creature.IsAlive))
                    await CreatureCmd.GainMaxHp(player.Creature, settings.NonupeipeMaxHp);

                return IncidentText.NonupeipeMaxHp(settings.NonupeipeMaxHp);

            case NonupeipeGift.GoldAfterCombat:
                runtime.PromisedGold += settings.NonupeipeGold;
                return IncidentText.NonupeipeGold(settings.NonupeipeGold);

            default:
                var marked = MarkedRooms.Mark(members.FirstOrDefault(), seed);
                return marked
                    ? IncidentText.NonupeipeMarkedRoom()
                    : IncidentText.NonupeipeNoRoomLeft();
        }
    }

    // ---- Tanx --------------------------------------------------------------------------------------

    private static async Task<string?> LendWeaponAsync(CombatState combatState, ulong seed)
    {
        string? lastName = null;
        foreach (var player in Party.Members(combatState).Where(player => player.Creature.IsAlive))
        {
            var pool = OwnAttacks(player).ToList();
            if (pool.Count == 0)
                continue;

            var rng = new Rng(seed ^ player.NetId, RngName);
            var card = CardFactory.GetDistinctForCombat(player, pool, 1, rng).FirstOrDefault();
            if (card == null)
                continue;

            if (await LendAsync(card, player))
                lastName = card.Title;
        }

        return lastName == null ? null : IncidentText.TanxWeapon(lastName);
    }

    private static IEnumerable<CardModel> OwnAttacks(Player player) =>
        UnlockedCards(player, player.Character).Where(card => card.Type == CardType.Attack);

    // ---- Tezcatara ---------------------------------------------------------------------------------

    private static async Task<string?> GiveWaxRelicAsync(CombatState combatState, CombatIncidentState runtime)
    {
        string? lastName = null;
        foreach (var player in Party.Members(combatState).Where(player => player.Creature.IsAlive))
        {
            // Pulled from the run's own relic queue, so it obeys everything the run already decided
            // about what this player may still be offered.
            var relic = RelicFactory.PullNextRelicFromFront(player).ToMutable();
            relic.IsWax = true;
            var obtained = await RelicCmd.Obtain(relic, player);
            runtime.WaxRelics.Add(obtained);
            lastName = obtained.Title.GetFormattedText();
        }

        return lastName == null ? null : IncidentText.TezcataraEmber(lastName);
    }

    /// <summary>Melts everything Tezcatara lent out. Called however the fight ended.</summary>
    public static async Task MeltWaxAsync(CombatIncidentState runtime)
    {
        foreach (var relic in runtime.WaxRelics.Where(relic => relic is { IsMelted: false }))
            await RelicCmd.Melt(relic);

        runtime.WaxRelics.Clear();
    }

    // ---- Pael --------------------------------------------------------------------------------------

    private static string? Promise(
        CombatIncidentState runtime,
        IncidentSettings settings,
        int round,
        ulong seed)
    {
        if (settings.PaelEnergy <= 0 && settings.PaelCards <= 0)
            return null;

        var deferral = runtime.DeferralFor(round + 1);
        deferral.Energy += settings.PaelEnergy;
        deferral.Draw += settings.PaelCards;
        deferral.Seed = seed;
        return IncidentText.PaelPromise(settings.PaelEnergy, settings.PaelCards);
    }

    // ---- Orobas ------------------------------------------------------------------------------------

    /// <summary>
    ///     Cards from every discipline but your own. The game's own selection screen is used rather than
    ///     our popup, because that screen already knows how to let four people each pick their own card
    ///     without the four machines disagreeing about what was picked.
    /// </summary>
    private static async Task<string?> OfferForeignCardAsync(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        IncidentSettings settings,
        ulong seed)
    {
        string? lastName = null;
        foreach (var player in Party.Members(combatState).Where(player => player.Creature.IsAlive))
        {
            var pool = ForeignCards(player).ToList();
            if (pool.Count == 0)
                continue;

            var rng = new Rng(seed ^ player.NetId, RngName);
            var offered = CardFactory
                .GetDistinctForCombat(player, pool, Math.Clamp(settings.OrobasChoices, 1, 5), rng)
                .ToList();
            if (offered.Count == 0)
                continue;

            var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, offered, player, canSkip: true);
            if (chosen == null)
                continue;

            if (await LendAsync(chosen, player))
                lastName = chosen.Title;
        }

        return lastName == null ? null : IncidentText.OrobasGift(lastName);
    }

    private static IEnumerable<CardModel> ForeignCards(Player player) =>
        ModelDb.AllCharacters
            .Where(character => character.Id != player.Character.Id)
            .SelectMany(character => UnlockedCards(player, character));

    // ---- Shared ------------------------------------------------------------------------------------

    /// <summary>
    ///     Puts a borrowed card in hand: free for this turn, and Ethereal so the turn is all it gets.
    ///     Anything still holding it when the turn ends burns, which is what makes "free" a deadline
    ///     rather than a gift you can bank.
    /// </summary>
    private static async Task<bool> LendAsync(CardModel card, Player player)
    {
        card.SetToFreeThisTurn();
        card.AddKeyword(CardKeyword.Ethereal);
        var result = await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player,
            CardPilePosition.Top);
        if (result.success)
            return true;

        MainFile.Logger.Warn($"The game refused to put {card.Id} into {player.NetId}'s hand.");
        return false;
    }

    private static IEnumerable<CardModel> UnlockedCards(Player player, CharacterModel character)
    {
        try
        {
            return character.CardPool.GetUnlockedCards(
                player.UnlockState, player.RunState.CardMultiplayerConstraint);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not read {character.Id}'s card pool. {exception}");
            return Array.Empty<CardModel>();
        }
    }

    /// <summary>
    ///     Pays out everything that was promised for this turn, for every seat. Returns each line with
    ///     the Ancient that owes it, because one turn can settle two different promises and a notice
    ///     signed by the wrong shrine is worse than no notice at all.
    /// </summary>
    public static async Task<List<(IncidentKind Kind, string Report)>> SettleDeferralsAsync(
        PlayerChoiceContext choiceContext,
        CombatState combatState,
        CombatIncidentState runtime,
        int round)
    {
        var reports = new List<(IncidentKind Kind, string Report)>();
        if (!runtime.Deferrals.Remove(round, out var deferral) || deferral.IsEmpty)
            return reports;

        foreach (var player in Party.Members(combatState).Where(player => player.Creature.IsAlive))
        {
            if (deferral.Energy > 0)
                await PlayerCmd.GainEnergy(deferral.Energy, player);

            if (deferral.Draw > 0)
                await CardPileCmd.Draw(choiceContext, deferral.Draw, player);

            if (deferral.Gamble)
            {
                var drawn = await RunGambleAsync(choiceContext, player);
                MainFile.Logger.Info($"Darv filled {player.NetId}'s hand with {drawn} card(s).");
            }

            if (!deferral.Takeover)
                continue;

            var played = await RunTakeoverAsync(choiceContext, combatState, player, deferral.Seed);
            MainFile.Logger.Info($"Vakuu ran {played} card(s) out of {player.NetId}'s hand.");
        }

        if (deferral.Energy > 0 || deferral.Draw > 0)
        {
            reports.Add((IncidentKind.PaelsBlessing,
                IncidentText.PaelTrigger(deferral.Energy, deferral.Draw)));
        }

        if (deferral.Gamble)
            reports.Add((IncidentKind.DarvsGamble, IncidentText.DarvTrigger()));
        if (deferral.Takeover)
            reports.Add((IncidentKind.VakuusTakeover, IncidentText.VakuuTrigger()));

        return reports;
    }
}
