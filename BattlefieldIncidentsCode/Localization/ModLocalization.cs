using System.Globalization;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;

namespace BattlefieldIncidents.Localization;

internal static class ModLocalization
{
    private const string ResourceFolder = "BattlefieldIncidents.Localization";

    internal static I18N Table { get; } = RitsuLibFramework.CreateModLocalizationWithFallback(
        MainFile.ModId,
        "BattlefieldIncidents-UI",
        resourceFolders: [ResourceFolder],
        resourceAssembly: typeof(ModLocalization).Assembly,
        fallbackLanguage: "eng");

    internal static ModSettingsText Text(string key, string englishFallback) =>
        ModSettingsText.I18N(Table, key, englishFallback);

    internal static string Get(string key, string englishFallback) =>
        Table.Get(key, englishFallback);

    internal static string Format(string key, string englishFallback, params object?[] arguments)
    {
        var template = Get(key, englishFallback);
        return string.Format(ResolveCulture(), template, arguments);
    }

    internal static string Turns(int value) => value == 1
        ? Format("format.turn.one", "{0} turn", value)
        : Format("format.turn.other", "{0} turns", value);

    internal static string Layers(int value) => value == 1
        ? Format("format.layer.one", "{0} stack", value)
        : Format("format.layer.other", "{0} stacks", value);

    private static CultureInfo ResolveCulture()
    {
        try
        {
            return MegaCrit.Sts2.Core.Localization.LocManager.Instance?.CultureInfo ??
                   CultureInfo.InvariantCulture;
        }
        catch
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
