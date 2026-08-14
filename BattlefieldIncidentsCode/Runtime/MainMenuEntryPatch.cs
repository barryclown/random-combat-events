using BattlefieldIncidents.Localization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2RitsuLib.Settings;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     Adds a main-menu entry that opens this mod's settings directly, so the player does not have to
///     find it through the mod settings list. Neither BaseLib nor RitsuLib exposes an API for this, so
///     the entry is a duplicate of the game's own menu button appended to the same container. Other mods
///     patch the same method; everything here is additive and keyed by node name so they can coexist.
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
[HarmonyPriority(Priority.Last)]
internal static class MainMenuEntryPatch
{
    private const string ButtonName = "BattlefieldIncidentsSettingsButton";
    private const string ButtonsPath = "%MainMenuTextButtons";
    private const string TemplateName = "SettingsButton";
    private const string AnchorName = "QuitButton";

    private static void Postfix(NMainMenu __instance)
    {
        try
        {
            EnsureEntry(__instance);
        }
        catch (Exception exception)
        {
            // The main menu must still work if the game's scene layout changed under us.
            MainFile.Logger.Warn($"Could not add the main menu entry: {exception}");
        }
    }

    private static void EnsureEntry(NMainMenu mainMenu)
    {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        var buttons = mainMenu.GetNodeOrNull<Control>(ButtonsPath) ??
                      mainMenu.GetNodeOrNull<Control>("MainMenuTextButtons");
        if (buttons == null)
        {
            MainFile.Logger.Warn("Main menu button list not found; skipping the settings entry.");
            return;
        }

        if (buttons.GetNodeOrNull<NMainMenuTextButton>(ButtonName) is { } existing)
        {
            SetLabel(existing);
            return;
        }

        if (buttons.GetNodeOrNull<NMainMenuTextButton>(TemplateName) is not { } template)
        {
            MainFile.Logger.Warn($"Main menu template button '{TemplateName}' not found; skipping the settings entry.");
            return;
        }

        // Signals are deliberately excluded: the template is already wired to the game's settings screen.
        var duplicateFlags = (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts);
        if (template.Duplicate(duplicateFlags) is not NMainMenuTextButton button)
        {
            MainFile.Logger.Warn("Duplicating the main menu template button did not return a menu button.");
            return;
        }

        button.Name = ButtonName;
        button.Visible = true;
        ClearFocusChain(button);
        buttons.AddChild(button);

        // Sit above Quit so the mod entry reads as part of the menu rather than an afterthought.
        if (buttons.GetNodeOrNull<Control>(AnchorName) is { } anchor)
            buttons.MoveChild(button, anchor.GetIndex());

        button.SetEnabled(true);
        SetLabel(button);
        ConnectSignals(mainMenu, button);
        button.AddChild(new MainMenuEntryLanguageWatcher());
        MainFile.Logger.Info($"Main menu entry added at index {button.GetIndex()} of {buttons.GetChildCount()}.");
    }

    /// <summary>
    ///     Other mods wire explicit focus neighbours between their own entries, and duplicating a button
    ///     copies those paths. Clearing them lets Godot pick neighbours from the layout instead, which is
    ///     correct wherever this entry ends up in the list.
    /// </summary>
    private static void ClearFocusChain(Control button)
    {
        button.FocusNeighborTop = new NodePath();
        button.FocusNeighborBottom = new NodePath();
        button.FocusNeighborLeft = new NodePath();
        button.FocusNeighborRight = new NodePath();
        button.FocusNext = new NodePath();
        button.FocusPrevious = new NodePath();
    }

    private static void ConnectSignals(NMainMenu mainMenu, NMainMenuTextButton button)
    {
        button.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(_ => OpenSettings()));

        // The game wires the caret animation in _Ready, before this button exists, so repeat it here.
        button.Connect(
            NClickableControl.SignalName.Focused,
            Callable.From<NMainMenuTextButton>(focused =>
                Callable.From(() => CallMenuFocus(mainMenu, "MainMenuButtonFocused", focused)).CallDeferred()));
        button.Connect(
            NClickableControl.SignalName.Unfocused,
            Callable.From<NMainMenuTextButton>(unfocused =>
                CallMenuFocus(mainMenu, "MainMenuButtonUnfocused", unfocused)));
    }

    private static void CallMenuFocus(NMainMenu mainMenu, string method, NMainMenuTextButton button)
    {
        if (!GodotObject.IsInstanceValid(mainMenu) || !GodotObject.IsInstanceValid(button))
            return;

        mainMenu.Call(method, button);
    }

    internal static void SetLabel(NMainMenuTextButton button)
    {
        if (button.label is { } label)
            label.Text = ModLocalization.Get("main_menu.settings_entry", "Combat Events");
    }

    private static void OpenSettings()
    {
        var result = ModSettingsNavigator.RequestOpenByIds(MainFile.ModId, null, null, null);
        if (!result.Success)
            MainFile.Logger.Warn($"Main menu entry could not open the settings page: {result.Message}");
    }
}

/// <summary>
///     Godot broadcasts a translation-changed notification to every node, which is the only signal we get
///     when the player switches language. The button's own refresh path only handles the game's own
///     localization keys, so the label is re-read here instead.
/// </summary>
internal sealed partial class MainMenuEntryLanguageWatcher : Node
{
    public override void _Notification(int what)
    {
        if (what != NotificationTranslationChanged)
            return;

        if (GetParent() is NMainMenuTextButton button && GodotObject.IsInstanceValid(button))
            MainMenuEntryPatch.SetLabel(button);
    }
}
