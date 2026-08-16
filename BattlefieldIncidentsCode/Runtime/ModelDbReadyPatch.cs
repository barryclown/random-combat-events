using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     The card database is built after mods initialize, so the catalog audit runs from here instead.
///     The postfix only reads and logs, which keeps it safe to sit alongside other mods patching the
///     same method.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
internal static class ModelDbReadyPatch
{
    private static void Postfix()
    {
        try
        {
            BoonResolver.EnsureAudited();

            // Built now rather than on the first summon: it reads the game's encounter tables, so doing
            // it here proves the exclusion list still lines up with the real data before a fight needs it.
            MonsterPools.Warm();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Boon catalog audit could not run after model database init: {exception.Message}");
        }
    }
}
