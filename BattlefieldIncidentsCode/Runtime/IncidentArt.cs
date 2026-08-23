using BattlefieldIncidents.Scheduling;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using GameEvents = MegaCrit.Sts2.Core.Models.Events;

namespace BattlefieldIncidents.Runtime;

/// <summary>
///     The picture on an incident's notice.
///     <para>
///     Every incident used to borrow a status-effect icon, which meant Vakuu was announced with a
///     handful of knives and three different events all showed the same Strength arrow. An event named
///     after somebody should wear that somebody's face, so the Ancients use the portrait the run-history
///     screen already draws for them, and the events that bring another monster in use the map's own
///     room icons from the same set.
///     Status icons are kept only where the incident really is a status effect.
///     </para>
///     <para>
///     Nothing is cached here on purpose. The game's asset cache hands back the same texture on every
///     call and reloads it if it has been freed, and it frees exactly these textures when the run
///     returns to the main menu; a copy held in this class would survive that and hand a dead handle to
///     the next fight.
///     </para>
/// </summary>
internal static class IncidentArt
{
    /// <summary>Where the game keeps the small square icons it uses for map rooms and Ancients.</summary>
    private const string RoomIconRoot = "res://images/ui/run_history/";

    public static Texture2D Icon(IncidentKind kind) => Portrait(kind) ?? StatusIcon(kind);

    /// <summary>
    ///     Resolves every picture once at load and says in the log which ones came back empty.
    ///     <para>
    ///     Borrowed art is the kind of thing that breaks silently: the game renames a file, a portrait
    ///     quietly falls back to a status icon, and nobody notices until a screenshot looks wrong. One
    ///     line at load is cheaper than finding out mid-fight.
    ///     </para>
    /// </summary>
    public static void Warm()
    {
        var borrowed = 0;
        var missing = new List<IncidentKind>();
        foreach (var kind in Enum.GetValues<IncidentKind>())
        {
            if (Portrait(kind) != null)
                borrowed++;
            else if (WantsPortrait(kind))
                missing.Add(kind);
        }

        MainFile.Logger.Info($"Incident art: {borrowed} picture(s) taken from the game's own assets.");
        if (missing.Count > 0)
        {
            MainFile.Logger.Warn(
                $"Incident art missing, falling back to status icons: {string.Join(", ", missing)}.");
        }
    }

    /// <summary>The incidents that are supposed to have a picture of their own, for the load check.</summary>
    private static bool WantsPortrait(IncidentKind kind) => kind
        is IncidentKind.VakuusTakeover or IncidentKind.DarvsGamble or IncidentKind.NonupeipesGift
        or IncidentKind.TanxsArmory or IncidentKind.TezcatarasEmber or IncidentKind.PaelsBlessing
        or IncidentKind.OrobassOffer or IncidentKind.NeowsBlessing or IncidentKind.FreeSummon
        or IncidentKind.Mercenary or IncidentKind.EnemyRecruit or IncidentKind.Challenge;

    /// <summary>
    ///     The face of whoever this event belongs to, or null for the incidents that are weather rather
    ///     than a character and are better served by a status icon.
    /// </summary>
    private static Texture2D? Portrait(IncidentKind kind)
    {
        try
        {
            return kind switch
            {
                IncidentKind.VakuusTakeover => Ancient<GameEvents.Vakuu>(),
                IncidentKind.DarvsGamble => Ancient<GameEvents.Darv>(),
                IncidentKind.NonupeipesGift => Ancient<GameEvents.Nonupeipe>(),
                IncidentKind.TanxsArmory => Ancient<GameEvents.Tanx>(),
                IncidentKind.TezcatarasEmber => Ancient<GameEvents.Tezcatara>(),
                IncidentKind.PaelsBlessing => Ancient<GameEvents.Pael>(),
                IncidentKind.OrobassOffer => Ancient<GameEvents.Orobas>(),
                IncidentKind.NeowsBlessing => Ancient<GameEvents.Neow>(),

                // The four summon events read as rooms that walked in on their own, so they wear the
                // icons the map uses for exactly that: an ordinary fight, a purchase, an unidentified
                // monster, and an Elite.
                IncidentKind.FreeSummon => RoomIcon("monster"),
                IncidentKind.Mercenary => RoomIcon("shop"),
                IncidentKind.EnemyRecruit => RoomIcon("unknown_monster"),
                IncidentKind.Challenge => RoomIcon("elite"),

                _ => null,
            };
        }
        catch (Exception exception)
        {
            // A missing picture is never worth losing the notice over.
            MainFile.Logger.Warn($"Could not load the picture for {kind}; falling back. {exception.Message}");
            return null;
        }
    }

    /// <summary>
    ///     The Ancient's face, as the run-history screen draws it. Not <c>MapIcon</c>: the map node art
    ///     is a white line-drawing of the whole shrine, which at 44px is an unreadable tangle. The
    ///     run-history icon is a full-colour portrait, and it comes from the same set as the room icons
    ///     below, so the whole notice sheet ends up looking like one family.
    /// </summary>
    private static Texture2D? Ancient<T>() where T : AncientEventModel =>
        ModelDb.AncientEvent<T>().RunHistoryIcon;

    private static Texture2D? RoomIcon(string name) =>
        PreloadManager.Cache.GetAsset<Texture2D>($"{RoomIconRoot}{name}.png");

    /// <summary>
    ///     What is left: the incidents that really are a status effect on the battlefield, each wearing
    ///     the icon of the effect it applies or of the thing it most resembles. No two share one now, so
    ///     a glance at the notice is enough to tell which event fired.
    /// </summary>
    private static Texture2D StatusIcon(IncidentKind kind) => kind switch
    {
        IncidentKind.Rockfall => ModelDb.Power<RollingBoulderPower>().Icon,
        IncidentKind.SwordRain => ModelDb.Power<FanOfKnivesPower>().Icon,
        IncidentKind.ToxicFog => ModelDb.Power<PoisonPower>().Icon,
        IncidentKind.VineSnare => ModelDb.Power<VulnerablePower>().Icon,
        IncidentKind.DampSeaWind => ModelDb.Power<WeakPower>().Icon,
        IncidentKind.HiveOnslaught => ModelDb.Power<PersonalHivePower>().Icon,
        IncidentKind.GentleRain => ModelDb.Power<RegenPower>().Icon,
        IncidentKind.ArchitectsCurse => ModelDb.Power<FrailPower>().Icon,
        IncidentKind.Laser => ModelDb.Power<LightningRodPower>().Icon,
        IncidentKind.LastMiracle => ModelDb.Power<BufferPower>().Icon,
        IncidentKind.StranglingVines => ModelDb.Power<ConstrictPower>().Icon,
        _ => ModelDb.Power<VulnerablePower>().Icon,
    };
}
