using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace BattlefieldIncidents.Runtime;

/// <summary>Small capability bridge for the public 0.107.1 and beta 0.111.0 APIs.</summary>
internal static class GameApiCompat
{
    // IsMock was added after the public API. Older models cannot be mocks.
    internal static bool IsMock(object model) =>
        model.GetType().GetProperty("IsMock", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(model) is true;

    // Seed changed from uint to ulong; a direct getter embeds the return type in the ABI.
    internal static ulong RunSeed(object rng) =>
        Convert.ToUInt64(rng.GetType().GetProperty("Seed")?.GetValue(rng)
            ?? throw new MissingMemberException(rng.GetType().FullName, "Seed"));

    internal static Task Exhaust(PlayerChoiceContext context, CardModel card)
    {
        var method = typeof(CardCmd).GetMethod("Exhaust",
            [typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool), typeof(bool)])
            ?? throw new MissingMethodException(typeof(CardCmd).FullName, "Exhaust");
        // Invoke each runtime's optional defaults. Both Task and Task<T> can be awaited as Task.
        return method.Invoke(null, [context, card, Type.Missing, Type.Missing]) as Task
               ?? throw new InvalidOperationException("CardCmd.Exhaust did not return a Task.");
    }

    internal static Rng CreateRng(ulong seed, string name)
    {
        var modern = typeof(Rng).GetConstructor([typeof(ulong), typeof(string)]);
        if (modern != null)
            return (Rng)modern.Invoke([seed, name]);

        var legacy = typeof(Rng).GetConstructor([typeof(uint), typeof(int)])
                     ?? throw new MissingMethodException(typeof(Rng).FullName, "supported constructor");
        // The old API only accepts 32 bits. Fold both halves deterministically; never use
        // string.GetHashCode(), whose random process salt would reroll a reloaded fight.
        var folded = unchecked((uint)seed ^ (uint)(seed >> 32));
        return (Rng)legacy.Invoke([folded, 0]);
    }

    internal static CardCreationOptions CombatCardOptions(Player player, RoomType roomType)
    {
        var options = CardCreationOptions.ForRoom(player, roomType);
        return Enum.TryParse<CardCreationFlags>("IsFromCombat", out var flag)
            ? options.WithFlags(flag)
            : options;
    }
}
