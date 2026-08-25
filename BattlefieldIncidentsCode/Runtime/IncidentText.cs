using BattlefieldIncidents.Localization;
using BattlefieldIncidents.Scheduling;
using BattlefieldIncidents.Settings;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace BattlefieldIncidents.Runtime;

internal static class IncidentText
{
    public static string Name(IncidentKind kind) => kind switch
    {
        IncidentKind.Rockfall => L("incident.name.rockfall", "Rockfall"),
        IncidentKind.SwordRain => L("incident.name.sword_rain", "Sword Rain"),
        IncidentKind.ToxicFog => L("incident.name.toxic_fog", "Toxic Fog"),
        IncidentKind.VineSnare => L("incident.name.vine_snare", "Vine Snare"),
        IncidentKind.DampSeaWind => L("incident.name.damp_sea_wind", "Damp Sea Wind"),
        IncidentKind.HiveOnslaught => L("incident.name.hive_onslaught", "Hive Onslaught"),
        IncidentKind.GentleRain => L("incident.name.gentle_rain", "Gentle Rain"),
        IncidentKind.NeowsBlessing => L("incident.name.neows_blessing", "Neow's Blessing"),
        IncidentKind.ArchitectsCurse => L("incident.name.architects_curse", "Architect's Curse"),
        IncidentKind.Laser => L("incident.name.laser", "Laser"),
        IncidentKind.FreeSummon => L("incident.name.free_summon", "Wandering Monster"),
        IncidentKind.Mercenary => L("incident.name.mercenary", "Mercenary"),
        IncidentKind.EnemyRecruit => L("incident.name.enemy_recruit", "Enemy Recruit"),
        IncidentKind.Challenge => L("incident.name.challenge", "Challenger"),
        IncidentKind.LastMiracle => L("incident.name.last_miracle", "Last Miracle"),
        IncidentKind.VakuusTakeover => L("incident.name.vakuu", "Vakuu's Takeover"),
        IncidentKind.DarvsGamble => L("incident.name.darv", "Darv's Gamble"),
        IncidentKind.NonupeipesGift => L("incident.name.nonupeipe", "Nonupeipe's Gift"),
        IncidentKind.TanxsArmory => L("incident.name.tanx", "Tanx's Armory"),
        IncidentKind.TezcatarasEmber => L("incident.name.tezcatara", "Tezcatara's Ember"),
        IncidentKind.PaelsBlessing => L("incident.name.pael", "Pael's Blessing"),
        IncidentKind.OrobassOffer => L("incident.name.orobas", "Orobas's Offer"),
        IncidentKind.StranglingVines => L("incident.name.strangling_vines", "Strangling Vines"),
        _ => kind.ToString(),
    };

    /// <summary>
    ///     Announced at combat start, while the unit is still standing. A save the player cannot see
    ///     coming would read as the game refusing a kill, and it would hide the one piece of
    ///     information worth having: that this unit needs to be hit twice.
    /// </summary>
    public static string MiracleGranted(string unitName) =>
        F("miracle.granted",
            "A Last Miracle watches over {0}. The first lethal blow it takes this combat leaves it at 1 HP instead.",
            unitName);

    public static string MiracleTrigger(string unitName) =>
        F("miracle.trigger",
            "Last Miracle! {0} holds on at 1 HP. The charge is spent, so the next lethal blow lands.",
            unitName);

    /// <summary>
    ///     Describes what a blessing or curse just handed out. Card options borrow the game's own card
    ///     title so the notice always matches what the player sees on the card.
    /// </summary>
    public static string BoonTrigger(BoonKind kind, BoonOption option)
    {
        var subject = option.Payload == BoonPayload.Card
            ? BoonResolver.TitleFor(option) ?? option.Id
            : PowerLabel(option);

        if (kind == BoonKind.Blessing)
        {
            return option.Payload == BoonPayload.Card
                ? F("boon.blessing.card", "Neow smiles on you! {0} is added to your hand.", subject)
                : F("boon.blessing.power", "Neow smiles on you! You gain {0} {1}.", option.Amount, subject);
        }

        return option.Payload == BoonPayload.Card
            ? F("boon.curse.card", "The Architect meddles! {0} is shuffled into your {1}.",
                subject, PileLabel(option.Pile))
            : F("boon.curse.power", "The Architect meddles! You suffer {0} {1}.", option.Amount, subject);
    }

    private static string PileLabel(BoonPile pile) => pile switch
    {
        BoonPile.Draw => L("boon.pile.draw", "draw pile"),
        BoonPile.Discard => L("boon.pile.discard", "discard pile"),
        _ => L("boon.pile.hand", "hand"),
    };

    private static string PowerLabel(BoonOption option) => option.Power switch
    {
        BoonPower.Strength or BoonPower.StrengthDown => PowerName<StrengthPower>(),
        BoonPower.Dexterity => PowerName<DexterityPower>(),
        BoonPower.Artifact => PowerName<ArtifactPower>(),
        BoonPower.Vulnerable => PowerName<VulnerablePower>(),
        BoonPower.Weak => PowerName<WeakPower>(),
        BoonPower.Frail => PowerName<FrailPower>(),
        _ => option.Id,
    };

    /// <param name="playerLaserDamage">
    ///     What the laser works out to for the player reading the notice. A percentage of health means
    ///     nothing at a glance, so the one number they can act on has to be spelled out.
    /// </param>
    public static string Warning(
        ScheduledCheckpoint checkpoint,
        int turnsRemaining,
        IncidentSettings settings,
        decimal playerLaserDamage) => checkpoint.Incident switch
    {
        IncidentKind.Rockfall =>
            F("incident.warning.rockfall",
                "In {0}, falling rocks will deal {1} damage to every unit at the end of the player side's turn. Block can prevent it.",
                ModLocalization.Turns(turnsRemaining), settings.RockfallDamage),
        IncidentKind.SwordRain =>
            F("incident.warning.sword_rain",
                "In {0}, Sword Rain will strike every unit on each side at the end of that side's turn for {1} damage × {2}. Block can prevent it.",
                ModLocalization.Turns(turnsRemaining), settings.SwordRainDamagePerHit,
                settings.SwordRainHitCount),
        IncidentKind.ToxicFog =>
            F("incident.warning.toxic_fog",
                "In {0}, Toxic Fog will cover the battlefield for that turn. After taking unblocked attack damage, a unit gains {1} {2}.",
                ModLocalization.Turns(turnsRemaining), settings.ToxicFogPoisonPerHit,
                PowerName<PoisonPower>()),
        IncidentKind.VineSnare =>
            F("incident.warning.vine_snare",
                "In {0}, vines will apply {1} {2} to all players and enemies.",
                ModLocalization.Turns(turnsRemaining), settings.VineSnareVulnerable,
                PowerName<VulnerablePower>()),
        IncidentKind.DampSeaWind =>
            F("incident.warning.damp_sea_wind",
                "In {0}, Damp Sea Wind will apply {1} {2} and {3} {4} to all players and enemies.",
                ModLocalization.Turns(turnsRemaining), settings.DampSeaWindWeak,
                PowerName<WeakPower>(), settings.DampSeaWindFrail, PowerName<FrailPower>()),
        IncidentKind.HiveOnslaught =>
            F("incident.warning.hive_onslaught",
                "In {0}, Hive Onslaught will begin. For {1}, both sides take {2} damage at the end of their own side's turn. Block can prevent it.",
                ModLocalization.Turns(turnsRemaining), ModLocalization.Turns(checkpoint.Duration),
                settings.HiveOnslaughtDamage),
        IncidentKind.Laser =>
            F("incident.warning.laser",
                "In {0}, a laser will sweep the field. Every unit takes {1}% of its own max HP at the end of its own side's turn, and you will take {2}. Block can prevent it.",
                ModLocalization.Turns(turnsRemaining), settings.LaserHpPercent, playerLaserDamage),
        IncidentKind.FreeSummon =>
            F("incident.warning.free_summon",
                "In {0}, another monster will wander into the fight and pick a side.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.Mercenary =>
            F("incident.warning.mercenary",
                "In {0}, a monster will offer to fight for you for gold.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.EnemyRecruit =>
            F("incident.warning.enemy_recruit",
                "In {0}, a monster will move to join the enemy, and gold will be able to change its mind.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.Challenge =>
            F("incident.warning.challenge",
                "In {0}, something will ask to join the fight against you, for extra spoils.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.GentleRain =>
            F("incident.warning.gentle_rain",
                "In {0}, Gentle Rain will heal every living unit for {1}% of its own max HP. Players heal at least {2}; anything else heals at least 1.",
                ModLocalization.Turns(turnsRemaining), settings.GentleRainHealPercent,
                settings.GentleRainPlayerMinimumHeal),
        IncidentKind.NeowsBlessing =>
            F("incident.warning.neows_blessing", "In {0}, Neow's Blessing will grant you a gift.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.ArchitectsCurse =>
            F("incident.warning.architects_curse", "In {0}, the Architect's Curse will burden you.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.VakuusTakeover =>
            F("incident.warning.vakuu",
                "In {0}, Vakuu will offer to take the turn after that off your hands.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.DarvsGamble =>
            F("incident.warning.darv",
                "In {0}, Darv will offer a full hand in exchange for knowing what anything costs.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.NonupeipesGift =>
            F("incident.warning.nonupeipe", "In {0}, Nonupeipe will leave you a gift.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.TanxsArmory =>
            F("incident.warning.tanx", "In {0}, Tanx will throw you a weapon for that turn.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.TezcatarasEmber =>
            F("incident.warning.tezcatara",
                "In {0}, Tezcatara will lend you a relic cast in wax, good until this fight ends.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.PaelsBlessing =>
            F("incident.warning.pael",
                "In {0}, Pael will promise you energy and a card for the turn after that.",
                ModLocalization.Turns(turnsRemaining)),
        IncidentKind.OrobassOffer =>
            F("incident.warning.orobas",
                "In {0}, Orobas will offer you a card from outside your own discipline.",
                ModLocalization.Turns(turnsRemaining)),
        _ => F("incident.warning.default", "A combat event will occur in {0}.",
            ModLocalization.Turns(turnsRemaining)),
    };

    public static string Trigger(
        ScheduledCheckpoint checkpoint,
        IncidentSettings settings,
        decimal playerLaserDamage) => checkpoint.Incident switch
    {
        IncidentKind.Rockfall =>
            F("incident.trigger.rockfall",
                "The boulders are coming loose! At the end of this turn, every unit will take {0} damage. Block can prevent it.",
                settings.RockfallDamage),
        IncidentKind.SwordRain =>
            F("incident.trigger.sword_rain",
                "Blades fill the sky! Each side will take {0} damage × {1} at the end of its own turn. Block can prevent it.",
                settings.SwordRainDamagePerHit, settings.SwordRainHitCount),
        IncidentKind.ToxicFog =>
            F("incident.trigger.toxic_fog",
                "Toxic Fog covers the battlefield! This turn, after taking unblocked attack damage, a unit gains {0} {1}.",
                settings.ToxicFogPoisonPerHit, PowerName<PoisonPower>()),
        IncidentKind.VineSnare =>
            F("incident.trigger.vine_snare",
                "Vines bind both sides! All players and enemies gain {0} {1}.",
                settings.VineSnareVulnerable, PowerName<VulnerablePower>()),
        IncidentKind.DampSeaWind =>
            F("incident.trigger.damp_sea_wind",
                "Damp Sea Wind erodes the battlefield! All players and enemies gain {0} {1} and {2} {3}.",
                settings.DampSeaWindWeak, PowerName<WeakPower>(), settings.DampSeaWindFrail,
                PowerName<FrailPower>()),
        IncidentKind.Laser =>
            F("incident.trigger.laser",
                "A laser sweeps the field! Every unit takes {0}% of its own max HP at the end of its own side's turn, and you will take {1}. Block can prevent it.",
                settings.LaserHpPercent, playerLaserDamage),
        IncidentKind.HiveOnslaught => HiveWave(checkpoint, 1, settings),
        IncidentKind.GentleRain =>
            F("incident.trigger.gentle_rain",
                "Gentle Rain soaks the battlefield! Every living unit heals {0}% of its own max HP. Players heal at least {1}; anything else heals at least 1.",
                settings.GentleRainHealPercent, settings.GentleRainPlayerMinimumHeal),
        _ => L("incident.trigger.default", "The battlefield shifts!"),
    };

    public static string HiveWave(ScheduledCheckpoint checkpoint, int wave, IncidentSettings settings) =>
        F("incident.hive.wave",
            "Hive wave {0}/{1} approaches! Both sides will take {2} damage at the end of their own side's turn. Block can prevent it.",
            wave, checkpoint.Duration, settings.HiveOnslaughtDamage);

    /// <summary>Names one wave of the Hive, so a three-turn event does not post the same line thrice.</summary>
    public static string HiveWaveLabel(int wave, int duration) =>
        F("incident.label.hive_wave", "Hive Onslaught · wave {0}/{1}", wave, duration);

    /// <summary>
    ///     What a blast of incident damage actually did, written after it landed rather than before.
    ///     <para>
    ///     These events resolve once per side, and the side that is not yours resolves in the middle of
    ///     the enemy turn where nothing on screen accounts for it. A prediction cannot say whether the
    ///     enemies were really hit, or for how much, and a percentage of "its own max HP" is not a number
    ///     anybody can check by eye. So the numbers here are counted from the damage results.
    ///     </para>
    /// </summary>
    public static string DamageResult(string label, DamageScope scope, DamageTally tally)
    {
        if (tally.Targets <= 0)
            return F("incident.result.nothing", "{0} finds nothing left to hit.", label);

        var units = ModLocalization.Units(tally.Targets);
        return (scope, tally.Blocked > 0) switch
        {
            (DamageScope.Players, false) =>
                F("incident.result.players", "{0} lands on your side: {1} lost {2} HP in total.",
                    label, units, tally.Unblocked),
            (DamageScope.Players, true) =>
                F("incident.result.players_blocked",
                    "{0} lands on your side: {1} lost {2} HP in total, and {3} more was blocked.",
                    label, units, tally.Unblocked, tally.Blocked),
            (DamageScope.Enemies, false) =>
                F("incident.result.enemies", "{0} lands on the enemy side: {1} lost {2} HP in total.",
                    label, units, tally.Unblocked),
            (DamageScope.Enemies, true) =>
                F("incident.result.enemies_blocked",
                    "{0} lands on the enemy side: {1} lost {2} HP in total, and {3} more was blocked.",
                    label, units, tally.Unblocked, tally.Blocked),
            (_, false) =>
                F("incident.result.everyone", "{0} lands on the whole room: {1} lost {2} HP in total.",
                    label, units, tally.Unblocked),
            _ =>
                F("incident.result.everyone_blocked",
                    "{0} lands on the whole room: {1} lost {2} HP in total, and {3} more was blocked.",
                    label, units, tally.Unblocked, tally.Blocked),
        };
    }

    /// <summary>
    ///     Combat-start boons get their own title. They are the one thing that cannot be announced a turn
    ///     ahead, so the player needs to see at a glance that this one came from the opening roll rather
    ///     than from the turn route.
    /// </summary>
    public static string CombatStartTitle(IncidentKind kind) =>
        F("toast.title.combat_start", "Combat Start · {0}", Name(kind));

    public static string IncidentTitle(IncidentKind kind) =>
        F("toast.title.incident", "Combat Event · {0}", Name(kind));

    public static string OmenTitle(int turn) =>
        F("toast.title.omen", "Event Omen · Turn {0}", turn);

    /// <summary>The picture that goes with an incident. Chosen in <see cref="IncidentArt" />.</summary>
    public static Texture2D Icon(IncidentKind kind) => IncidentArt.Icon(kind);

    // The Ancients. Each one names what it actually did, because "a blessing occurred" is not something
    // a player can plan the next turn around.

    public static string VakuuOffer() =>
        L("ancient.vakuu.offer",
            "Vakuu reaches for your hands. Let go, and next turn it plays every card you are holding — in the order it finds them, at whatever it feels like — and burns each one afterwards, playable or not.");

    public static string VakuuAccepted() =>
        L("ancient.vakuu.accepted", "You let go. Next turn is Vakuu's.");

    public static string VakuuDeclined() =>
        L("ancient.vakuu.declined", "You keep hold of your hand. Vakuu withdraws.");

    public static string VakuuTrigger() =>
        L("ancient.vakuu.trigger", "Vakuu plays your hand and burns what it touched.");

    public static string DarvOffer() =>
        L("ancient.darv.offer",
            "Darv offers to fill your hand next turn. The price is that every card you draw after that costs whatever it pleases, for the rest of this fight.");

    public static string DarvAccepted() =>
        L("ancient.darv.accepted", "Darv shakes on it. Your hand fills next turn.");

    public static string DarvDeclined() =>
        L("ancient.darv.declined", "You turn Darv down. It shrugs and drifts off.");

    public static string DarvTrigger() =>
        L("ancient.darv.trigger",
            "Darv fills your hand, and the prices come loose. Every card drawn from here costs what it likes.");

    public static string NonupeipeMaxHp(int amount) =>
        F("ancient.nonupeipe.max_hp", "Nonupeipe leaves you something warm. Everyone gains {0} max HP.",
            amount);

    public static string NonupeipeGold(int amount) =>
        F("ancient.nonupeipe.gold",
            "Nonupeipe promises everyone {0} once this fight is won. She is good for it.",
            ModLocalization.Gold(amount));

    public static string NonupeipeMarkedRoom() =>
        L("ancient.nonupeipe.marked_room",
            "Nonupeipe marks a fight further up the map. Something there will be waiting on its last point of health.");

    public static string NonupeipeNoRoomLeft() =>
        L("ancient.nonupeipe.no_room",
            "Nonupeipe looks up the map for somewhere to leave her mark, and finds nothing ahead of you worth marking.");

    public static string MarkedRoomTrigger(string monsterName) =>
        F("ancient.nonupeipe.marked_trigger",
            "Nonupeipe got here first. {0} is down to 1 HP before you have thrown anything.", monsterName);

    public static string TanxWeapon(string cardName) =>
        F("ancient.tanx.weapon",
            "Tanx throws you {0}. Free this turn, and gone at the end of it.", cardName);

    public static string TezcataraEmber(string relicName) =>
        F("ancient.tezcatara.ember",
            "Tezcatara presses {0} into your hands, still soft. It works until this fight ends, and then it does not.",
            relicName);

    public static string PaelPromise(int energy, int cards) =>
        F("ancient.pael.promise", "Pael promises you {0} energy and {1} card(s), next turn.",
            energy, cards);

    public static string PaelTrigger(int energy, int cards) =>
        F("ancient.pael.trigger", "Pael keeps its word: {0} energy and {1} card(s).", energy, cards);

    public static string OrobasGift(string cardName) =>
        F("ancient.orobas.gift",
            "Orobas hands over {0} from another discipline entirely. Free this turn, and gone at the end of it.",
            cardName);

    public static string DialogYield() => L("dialog.yield", "Let go");

    public static string DialogKeepControl() => L("dialog.keep_control", "Keep my hand");

    public static string DialogTakeTheDeal() => L("dialog.take_deal", "Deal");

    public static string DialogWalkAway() => L("dialog.walk_away", "Walk away");

    // Co-op only.

    public static string VinesCaught(string victimName, string vinesName, int escapeTurns) =>
        F("coop.vines.caught",
            "{1} has {0} by the arms — they cannot play a card until it is cut down, and it will only take one hit. Potions still work, and the vines lose interest after {2}.",
            victimName, vinesName, ModLocalization.Turns(escapeTurns));

    public static string VinesCut(string victimName) =>
        F("coop.vines.cut", "The vines come apart. {0} has their hands back.", victimName);

    public static string VinesWithered(string victimName) =>
        F("coop.vines.withered", "The vines lose their grip on their own. {0} is free.", victimName);

    // Summon events. Every one of these names the monster, because "something joins the fight" is not
    // a decision and "a Bowlbug joins the fight" is.

    public static string FreeSummon(string monsterName, bool joinsPlayer) => joinsPlayer
        ? F("summon.free.ally", "{0} wanders in and takes your side.", monsterName)
        : F("summon.free.enemy", "{0} wanders in and sides against you.", monsterName);

    public static string SummonLeft(string monsterName, bool wasAlly, bool wasPaidFor)
    {
        if (!wasAlly)
            return F("summon.left.enemy", "{0} loses interest and wanders off.", monsterName);

        // Help that was bought and then walked out is a different event from help that wandered off, and
        // the difference is the gold that does not come back.
        return wasPaidFor
            ? F("summon.left.hired",
                "{0} decides it has done enough and walks off. What you paid stays paid.", monsterName)
            : F("summon.left.ally", "{0} loses interest and wanders off. You are on your own again.",
                monsterName);
    }

    public static string MercenaryOffer(string monsterName, int price) =>
        F("summon.mercenary.offer",
            "{0} offers to fight for you, for {1} gold. It wants paying next turn.", monsterName, price);

    public static string MercenaryTooExpensive(string monsterName, int price) =>
        F("summon.mercenary.too_expensive",
            "{0} sizes up your purse, quotes {1} gold, and moves on when it sees you cannot cover it.",
            monsterName, price);

    public static string MercenaryDeclined(string monsterName) =>
        F("summon.mercenary.declined", "You wave {0} off. It shrugs and leaves.", monsterName);

    public static string MercenaryAccepted(string monsterName, int price) =>
        F("summon.mercenary.accepted",
            "You shake on it with {0}. {1} gold changes hands next turn.", monsterName, price);

    public static string MercenaryHelps(string monsterName, int price) =>
        F("summon.mercenary.helps", "You pay {1} gold and {0} joins your side.", monsterName, price);

    public static string MercenaryRanOff(string monsterName, int price) =>
        F("summon.mercenary.ran_off",
            "You pay {1} gold and {0} is already gone. Nothing you can do about it.", monsterName, price);

    public static string MercenaryBetrayed(string monsterName, int price) =>
        F("summon.mercenary.betrayed",
            "You pay {1} gold and {0} decides anyone carrying that much is worth robbing. It joins the enemy.",
            monsterName, price);

    public static string RecruitOffer(string monsterName, int standDownPrice, int hirePrice) =>
        F("summon.recruit.offer",
            "{0} is about to join the enemy. Gold can talk it down for {1}, or bring it over for {2}.",
            monsterName, standDownPrice, hirePrice);

    public static string RecruitFollowUp(string monsterName, int standDownPrice, int hirePrice) =>
        F("summon.recruit.follow_up",
            "How much is {0} worth to you? Standing down costs {1}. Fighting for you costs {2}.",
            monsterName, standDownPrice, hirePrice);

    public static string RecruitStoodDown(string monsterName, int price) =>
        F("summon.recruit.stood_down", "{1} gold, and {0} stays out of it.", monsterName, price);

    public static string RecruitRanOff(string monsterName, int price) =>
        F("summon.recruit.ran_off", "{1} gold, and {0} takes it and vanishes.", monsterName, price);

    public static string RecruitHelps(string monsterName, int price) =>
        F("summon.recruit.helps", "{1} gold, and {0} turns around to fight for you.", monsterName, price);

    public static string RecruitJoinedEnemies(string monsterName) =>
        F("summon.recruit.joined_enemies", "{0} joins the enemy side anyway.", monsterName);

    public static string RecruitPaidButJoinedEnemies(string monsterName, int price) =>
        F("summon.recruit.paid_joined",
            "{1} gold gone, and {0} joins the enemy side anyway.", monsterName, price);

    public static string ChallengeOffer(string monsterName, bool isElite) => isElite
        ? F("summon.challenge.offer_elite",
            "{0} wants in on this fight. It looks like it can take one. Beating it pays.", monsterName)
        : F("summon.challenge.offer", "{0} wants in on this fight. Beating it pays.", monsterName);

    public static string ChallengeAccepted(string monsterName) =>
        F("summon.challenge.accepted", "You wave {0} in. It joins the enemy side.", monsterName);

    public static string ChallengeFled(string monsterName) =>
        F("summon.challenge.fled",
            "You wave {0} in and it thinks better of it, dropping what it was carrying as it goes.",
            monsterName);

    public static string ChallengeLeft(string monsterName) =>
        F("summon.challenge.left", "You turn {0} away. It says its piece and goes.", monsterName);

    public static string ChallengeForcedItself(string monsterName) =>
        F("summon.challenge.forced", "You turn {0} away. It joins anyway.", monsterName);

    public static string DialogHire(int price) => F("dialog.hire", "Pay {0}", price);

    public static string DialogDecline() => L("dialog.decline", "No thanks");

    public static string DialogNegotiate() => L("dialog.negotiate", "Talk to it");

    public static string DialogIgnore() => L("dialog.ignore", "Ignore it");

    public static string DialogStandDown(int price) => F("dialog.stand_down", "Stand down ({0})", price);

    public static string DialogRecruit(int price) => F("dialog.recruit", "Fight for me ({0})", price);

    public static string DialogAccept() => L("dialog.accept", "Bring it on");

    public static string DialogRefuse() => L("dialog.refuse", "Not now");

    private static string PowerName<T>() where T : PowerModel =>
        ModelDb.Power<T>().Title.GetFormattedText();

    private static string L(string key, string englishFallback) =>
        ModLocalization.Get(key, englishFallback);

    private static string F(string key, string englishFallback, params object?[] arguments) =>
        ModLocalization.Format(key, englishFallback, arguments);
}
