using BattlefieldIncidents.Scheduling;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     A yes/no question asked in the middle of a fight, on the game's own popup.
///     <para>
///     Our combat hooks are awaited by the game, so awaiting the player's answer here simply holds the
///     turn open — no separate input mode, no patching of the combat loop.
///     </para>
/// </summary>
internal static class IncidentDialog
{
    // The popup takes LocStrings, which can only name an entry in one of the game's own tables. Our text
    // lives in the mod's table instead, so the popup is opened with these placeholders and every label is
    // overwritten with plain text before the player ever sees it.
    private const string PlaceholderTable = "gameplay_ui";
    private const string PlaceholderKey = "CONFIRM_LOAD_SAVE.confirm";

    /// <summary>
    ///     Asks the question and waits. Returns <paramref name="fallback" /> without showing anything if
    ///     the popup cannot be built — during automated tests, or if the UI is not in a state to take it.
    ///     A summon event is a bonus, so failing to ask must never stall the fight.
    /// </summary>
    public static async Task<bool> AskAsync(
        string title,
        string body,
        string confirmLabel,
        string declineLabel,
        bool fallback = false)
    {
        try
        {
            var popup = NGenericPopup.Create();
            if (popup == null)
            {
                MainFile.Logger.Info("Dialog unavailable; answering with the safe default.");
                return fallback;
            }

            var modals = NModalContainer.Instance;
            if (modals == null)
            {
                MainFile.Logger.Warn("No modal container; answering with the safe default.");
                return fallback;
            }

            modals.Add(popup);

            // The buttons resolve their own labels in _Ready, which Godot has not run yet — the node
            // only just entered the tree. Touching them now throws inside the game's own SetText, so
            // give the engine one frame to finish building the popup first.
            if (Engine.GetMainLoop() is SceneTree tree)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var placeholder = new LocString(PlaceholderTable, PlaceholderKey);
            var answer = popup.WaitForConfirmation(placeholder, placeholder, placeholder, placeholder);

            // Same lookup WaitForConfirmation just did, so the nodes are guaranteed to be resolved.
            var panel = popup.GetNodeOrNull<NVerticalPopup>("VerticalPopup");
            if (panel == null)
            {
                MainFile.Logger.Warn("Popup has no VerticalPopup child; answering with the safe default.");
                return fallback;
            }

            panel.SetText(title, body);
            panel.YesButton.SetText(confirmLabel);
            panel.NoButton.SetText(declineLabel);

            return await answer;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Combat dialog failed; answering with the safe default. {exception}");
            return fallback;
        }
    }

    /// <summary>
    ///     The same question, asked of the whole table. Everyone answers on their own screen, the
    ///     majority carries it, and a tie is settled by the combat seed so that every machine reaches
    ///     the same verdict without one of them having to be the authority on it.
    ///     <para>
    ///     In a single-player run this is <see cref="AskAsync" /> and nothing else.
    ///     </para>
    /// </summary>
    /// <param name="seed">
    ///     The tie-breaker. Taken from the route rather than from a live roll, so a reload settles a
    ///     split table the same way it settled it the first time.
    /// </param>
    public static async Task<bool> AskPartyAsync(
        CombatState combatState,
        ulong seed,
        string title,
        string body,
        string confirmLabel,
        string declineLabel,
        bool fallback = false)
    {
        if (!Party.IsCoop)
            return await AskAsync(title, body, confirmLabel, declineLabel, fallback);

        var players = Party.Members(combatState);
        var synchronizer = RunManager.Instance?.PlayerChoiceSynchronizer;
        if (players.Count <= 1 || synchronizer == null)
            return await AskAsync(title, body, confirmLabel, declineLabel, fallback);

        try
        {
            // Reserved for every seat, in the run's own order, before anything is awaited. The IDs have
            // to line up across peers, and the only way to guarantee that is to claim them all while the
            // code is still running lockstep.
            var choiceIds = players.Select(synchronizer.ReserveChoiceId).ToArray();
            var ballots = new int[players.Count];
            Array.Fill(ballots, -1);

            for (var seat = 0; seat < players.Count; seat++)
            {
                if (!LocalContext.IsMe(players[seat]))
                    continue;

                var answer = await AskAsync(title, body, confirmLabel, declineLabel, fallback);
                ballots[seat] = answer ? 1 : 0;
                synchronizer.SyncLocalChoice(players[seat], choiceIds[seat],
                    PlayerChoiceResult.FromIndex(ballots[seat]));
            }

            for (var seat = 0; seat < players.Count; seat++)
            {
                if (LocalContext.IsMe(players[seat]))
                    continue;

                ballots[seat] = (await synchronizer.WaitForRemoteChoice(players[seat], choiceIds[seat]))
                    .AsIndex();
            }

            var verdict = VoteTally.Resolve(ballots, seed);
            MainFile.Logger.Info(
                $"Party vote on \"{title}\": [{string.Join(",", ballots)}] -> {verdict}.");
            return verdict == 1;
        }
        catch (Exception exception)
        {
            // Never leave the table staring at a fight that will not continue. Take the safe answer and
            // say so loudly, because a silent fallback here is a desync waiting to be blamed on the game.
            MainFile.Logger.Error(
                $"Party vote failed; every seat falls back to {fallback}. {exception}");
            return fallback;
        }
    }

    /// <summary>
    ///     Three outcomes out of a two-button popup, asked as two questions: first whether to spend at
    ///     all, then which way to spend. The panel only carries a yes and a no button, and adding a third
    ///     means surgery on a scene we do not own — two plain questions cost one extra click and cannot
    ///     break.
    /// </summary>
    public static async Task<int> AskThreeWayAsync(
        CombatState combatState,
        ulong seed,
        string title,
        string openingBody,
        string spendLabel,
        string ignoreLabel,
        string followUpBody,
        string firstOptionLabel,
        string secondOptionLabel)
    {
        var wantsToSpend = await AskPartyAsync(
            combatState, seed, title, openingBody, spendLabel, ignoreLabel);
        if (!wantsToSpend)
            return 0;

        // A second, separately seeded vote: a table that splits on the first question has no business
        // splitting the same way on the second.
        var choseFirst = await AskPartyAsync(
            combatState, seed ^ 0xD1B5_4A32_D192_ED03UL, title, followUpBody,
            firstOptionLabel, secondOptionLabel);
        return choseFirst ? 1 : 2;
    }
}
