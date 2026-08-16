using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Pays out for the monsters a summon event added to the enemy side, to the same recipe the game
///     uses for a room of its own: gold, a card to choose from, a potion on a roll, and a relic for
///     anything Elite-sized.
///     <para>
///     This lives apart from <see cref="IncidentDirector" /> because of how the game dispatches the
///     rewards hook: <c>Hook.ModifyRewards</c> walks <c>runState.IterateHookListeners(null)</c>, and
///     passing null there skips every combat-state subscriber — which is exactly what the director is.
///     Subscribing the director for run hooks as well would fix the rewards and break everything else,
///     because the hooks that walk <c>IterateHookListeners(combatState)</c> would then find it twice and
///     fire each of its combat hooks twice over. A second, tiny model with one job avoids that entirely.
///     </para>
/// </summary>
public sealed class SpoilsCourier : SingletonModel, ICustomModel
{
    private static SpoilsCourier? _instance;
    private static bool _registered;

    /// <summary>Rewards are a room-end concern, so this model has no business inside a fight.</summary>
    public override bool ShouldReceiveCombatHooks => false;

    public static void Register()
    {
        if (_registered)
            return;

        _registered = true;
        ModHelper.SubscribeForRunStateHooks(MainFile.ModId, _ =>
        {
            _instance ??= ModelDb.GetById<SpoilsCourier>(ModelDb.GetId<SpoilsCourier>());
            return [_instance];
        });
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        try
        {
            var (normals, elites, seed) = IncidentDirector.TakeCarriedSpoils(room);
            if (normals == 0 && elites == 0)
                return false;

            var settings = SettingsBootstrap.Read();
            var roomType = room?.RoomType ?? RoomType.Monster;
            var gold = 0;
            var potions = 0;
            var relics = 0;
            var cards = 0;

            for (var index = 0; index < normals + elites; index++)
            {
                var isElite = index >= normals;
                // Each addition is rolled on its own stream, so two monsters cannot share one verdict.
                var entrySeed = seed ^ ((ulong)(uint)(index + 1) * 0x9E3779B97F4A7C15UL);

                gold += Spread(isElite ? settings.ExtraEliteGold : settings.ExtraMonsterGold, entrySeed);

                if (SummonRoll.PickIndex(100, entrySeed ^ 0xB07105UL) < settings.ExtraPotionPercent)
                    potions++;

                // An Elite always leaves a relic, the same way an Elite room always does.
                if (isElite || SummonRoll.PickIndex(100, entrySeed ^ 0xE11CUL) < settings.ExtraRelicPercent)
                    relics++;

                if (settings.ExtraCardReward)
                    cards++;
            }

            if (gold > 0)
                rewards.Add(new GoldReward(gold, player));

            for (var extra = 0; extra < cards; extra++)
            {
                rewards.Add(new CardReward(
                    CardCreationOptions.ForRoom(player, roomType).WithFlags(CardCreationFlags.IsFromCombat),
                    3, player));
            }

            for (var extra = 0; extra < potions; extra++)
                rewards.Add(new PotionReward(player));

            for (var extra = 0; extra < relics; extra++)
                rewards.Add(new RelicReward(player));

            MainFile.Logger.Info(
                $"Spoils raised for {normals} extra monster(s) and {elites} extra elite(s): " +
                $"+{gold} gold, {cards} card reward(s), {potions} potion(s), {relics} relic(s).");
            return true;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not raise the spoils: {exception}");
            return false;
        }
    }

    /// <summary>
    ///     A pile that lands somewhere around the configured figure rather than exactly on it, the way
    ///     the game's own gold rewards are a range rather than a fixed number.
    /// </summary>
    private static int Spread(int middle, ulong seed)
    {
        if (middle <= 0)
            return 0;

        var lowest = Math.Max(1, middle * 3 / 4);
        var highest = Math.Max(lowest + 1, middle * 5 / 4);
        return lowest + SummonRoll.PickIndex(highest - lowest + 1, seed);
    }
}
