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
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Boon catalog audit could not run after model database init: {exception.Message}");
        }
    }
}
