using System.Reflection;
using BattlefieldIncidents.Localization;
using Godot;
using HarmonyLib;
using STS2RitsuLib.Ui.Toast;

namespace BattlefieldIncidents.Runtime;

/// <summary>
/// RitsuLib 0.5.11 defines close-button theme values but does not create the
/// button. Add one only to incident prediction requests, keeping other mods'
/// notifications and click behavior untouched.
/// </summary>
internal static class RitsuToastCloseButtonPatch
{
    private const string EntryTypeName = "STS2RitsuLib.Ui.Toast.RitsuToastEntry";
    private const string ButtonName = "BattlefieldIncidentsCloseButton";

    internal static void TryApply(Harmony harmony)
    {
        try
        {
            var entryType = AccessTools.TypeByName(EntryTypeName);
            var visualStyleType = AccessTools.TypeByName("STS2RitsuLib.Ui.Toast.RitsuToastVisualStyle");
            var updateMethod = entryType == null || visualStyleType == null
                ? null
                : AccessTools.Method(entryType, "UpdateRequest", [typeof(RitsuToastRequest), visualStyleType]);
            if (entryType == null || updateMethod == null)
            {
                MainFile.Logger.Warn("Ritsu toast close-button hook unavailable; prediction notices remain clickable only through the service.");
                return;
            }

            harmony.Patch(updateMethod,
                postfix: new HarmonyMethod(typeof(RitsuToastCloseButtonPatch), nameof(AfterUpdateRequest)));
        }
        catch (Exception exception)
        {
            // A RitsuLib UI change should not prevent the game or this mod from loading.
            MainFile.Logger.Warn($"Could not install the prediction close-button hook: {exception}");
        }
    }

    private static void AfterUpdateRequest(object __instance, RitsuToastRequest request)
    {
        if (__instance is not Node entry)
            return;

        var row = AccessTools.Field(__instance.GetType(), "_row")?.GetValue(__instance) as HBoxContainer;
        if (row == null)
            return;

        var button = row.GetChildren()
            .OfType<Button>()
            .FirstOrDefault(child => child.Name.ToString() == ButtonName);
        if (button == null)
        {
            button = CreateButton(entry);
            row.AddChild(button);
        }

        button.TooltipText = ModLocalization.Get("toast.close_warning", "Close warning");
        button.Visible = IncidentToast.HasManualCloseButton(request);
        if (button.Visible)
            ApplyStyle(button, AccessTools.Field(__instance.GetType(), "_style")?.GetValue(__instance));
    }

    private static Button CreateButton(Node entry)
    {
        var button = new Button
        {
            Name = ButtonName,
            Text = "×",
            TooltipText = ModLocalization.Get("toast.close_warning", "Close warning"),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            ClipText = true,
        };
        button.Pressed += () =>
        {
            var currentRequest = AccessTools.Field(entry.GetType(), "_request")?.GetValue(entry) as RitsuToastRequest;
            if (currentRequest != null)
                IncidentToast.CloseFromButton(currentRequest);
        };
        return button;
    }

    private static void ApplyStyle(Button button, object? style)
    {
        var size = Read(style, "CloseButtonSize", 26f);
        var borderWidth = Read(style, "CloseButtonBorderWidth", 1);
        var paddingHorizontal = Read(style, "CloseButtonPaddingHorizontal", 2f);
        var paddingVertical = Read(style, "CloseButtonPaddingVertical", 1f);
        var foreground = Read(style, "InteractiveBadgeForeground", Colors.White);
        var normalBackground = Read(style, "CloseButtonBackground", new Color(0.26f, 0.39f, 0.56f, 0.75f));
        var hoverBackground = Read(style, "CloseButtonBackgroundHover", normalBackground.Lightened(0.12f));
        var normalBorder = Read(style, "CloseButtonBorder", new Color(0.4f, 0.5f, 0.6f, 0.9f));
        var hoverBorder = Read(style, "CloseButtonBorderHover", normalBorder.Lightened(0.16f));

        button.CustomMinimumSize = new Vector2(size, size);
        button.AddThemeFontSizeOverride("font_size", Math.Max(16, Read(style, "BodyFontSize", 16)));
        button.AddThemeColorOverride("font_color", foreground);
        button.AddThemeColorOverride("font_hover_color", foreground);
        button.AddThemeColorOverride("font_pressed_color", foreground);
        button.AddThemeStyleboxOverride("normal",
            BuildButtonStyle(normalBackground, normalBorder, borderWidth, paddingHorizontal, paddingVertical));
        button.AddThemeStyleboxOverride("hover",
            BuildButtonStyle(hoverBackground, hoverBorder, borderWidth, paddingHorizontal, paddingVertical));
        button.AddThemeStyleboxOverride("pressed",
            BuildButtonStyle(hoverBackground.Darkened(0.08f), hoverBorder, borderWidth,
                paddingHorizontal, paddingVertical));
    }

    private static StyleBoxFlat BuildButtonStyle(
        Color background,
        Color border,
        int borderWidth,
        float paddingHorizontal,
        float paddingVertical) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = paddingHorizontal,
        ContentMarginRight = paddingHorizontal,
        ContentMarginTop = paddingVertical,
        ContentMarginBottom = paddingVertical,
    };

    private static T Read<T>(object? source, string propertyName, T fallback)
    {
        if (source == null)
            return fallback;

        var value = source.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(source);
        return value is T typed ? typed : fallback;
    }
}
