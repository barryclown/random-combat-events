using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace BattlefieldIncidents;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "BattlefieldIncidents";
    public const string SettingsKey = "settings";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        try
        {
            Settings.SettingsBootstrap.Register();
            Runtime.IncidentDirector.Register();

            var harmony = new Harmony(ModId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Runtime.RitsuToastCloseButtonPatch.TryApply(harmony);
            Logger.Info("Random Combat Events 0.1.0-alpha.3 initialized.");
        }
        catch (Exception exception)
        {
            Logger.Error($"Random Combat Events initialization failed: {exception}");
            throw;
        }
    }
}
