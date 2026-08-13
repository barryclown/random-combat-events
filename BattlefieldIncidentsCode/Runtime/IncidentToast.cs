using Godot;
using STS2RitsuLib.Ui.Toast;

namespace BattlefieldIncidents.Runtime;

/// <summary>
/// Shows incident notices without RitsuLib's remaining-time progress track.
/// Predictions stay visible until the player presses their close button (or
/// the predicted incident occurs). Result notices close after six seconds.
/// </summary>
internal static class IncidentToast
{
    private const double DisplaySeconds = 6d;
    private static readonly Dictionary<RitsuToastRequest, IncidentWarningNotice> ManualWarnings =
        new(ReferenceEqualityComparer.Instance);

    public static void ShowInfo(string body, string title, Texture2D? image) =>
        ShowTimed(body, title, image, RitsuToastLevel.Info);

    public static void ShowTimedWarning(string body, string title, Texture2D? image) =>
        ShowTimed(body, title, image, RitsuToastLevel.Warning);

    public static IncidentWarningNotice ShowWarning(string body, string title, Texture2D? image)
    {
        var request = new RitsuToastRequest(body, title, image, RitsuToastLevel.Warning)
            .Persistent()
            .WithDismissOnClick(false);
        var notice = new IncidentWarningNotice(request);
        ManualWarnings.Add(request, notice);
        notice.Handle = RitsuToastService.ShowTracked(request);
        return notice;
    }

    internal static bool HasManualCloseButton(RitsuToastRequest request) =>
        ManualWarnings.ContainsKey(request);

    internal static void CloseFromButton(RitsuToastRequest request)
    {
        if (ManualWarnings.TryGetValue(request, out var notice))
            notice.Close();
    }

    internal static void CloseWarning(IncidentWarningNotice notice, bool immediate)
    {
        ManualWarnings.Remove(notice.Request);
        notice.Handle?.Close(immediate);
    }

    private static void ShowTimed(string body, string title, Texture2D? image, RitsuToastLevel level)
    {
        var handle = RitsuToastService.ShowTracked(
            new RitsuToastRequest(body, title, image, level)
                .Persistent());

        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            // A combat notice should always have a scene tree. Avoid leaving a
            // permanent notice behind if another caller invokes this too early.
            handle.Close(immediate: true);
            return;
        }

        var timer = tree.CreateTimer(
            DisplaySeconds,
            processAlways: true,
            processInPhysics: false,
            ignoreTimeScale: false);
        timer.Timeout += () => handle.Close();
    }
}

internal sealed class IncidentWarningNotice(RitsuToastRequest request)
{
    internal RitsuToastRequest Request { get; } = request;
    internal RitsuToastHandle? Handle { get; set; }

    internal void Close(bool immediate = false) => IncidentToast.CloseWarning(this, immediate);
}
