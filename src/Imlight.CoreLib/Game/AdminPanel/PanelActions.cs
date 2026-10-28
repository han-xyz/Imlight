/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// What a panel action works on.
/// </summary>
internal enum PanelReach {

    /// <summary>The looked-up account.</summary>
    Account,

    /// <summary>Nothing in particular, such as an IP address typed in.</summary>
    Server,

    /// <summary>The selected character, online or offline.</summary>
    Character,

    /// <summary>The selected character, only while it is online.</summary>
    CharacterOnline,

    /// <summary>The staff member's own character, through the chat command.</summary>
    Self,

}

internal sealed record PanelArgument(string Label, bool Optional = false);

/// <summary>
/// The staff member and the account a moderation action names.
/// </summary>
internal sealed record AccountActionContext(Account Actor, Wizard Character, IActorRefFactory Actors, string Username);

/// <summary>
/// One action of the admin panel's generic tabs: what it is called, what it asks for, who may use it and what runs.
/// </summary>
internal sealed record PanelAction(string Tab, string Id, string Label, AuthLevel Level, PanelReach Reach,
                                   PanelArgument[] Arguments) {

    /// <summary>
    /// False for an action that only shows something, which is not audited.
    /// </summary>
    internal bool Changes { get; init; } = true;

    internal Func<CharacterTarget, string[], AdminActionResult> OnCharacter { get; init; }
    internal Func<AccountActionContext, string[], AdminActionResult> OnAccount { get; init; }

    /// <summary>
    /// For <see cref="PanelReach.Self"/>: the chat command, with {0}, {1}… for the arguments.
    /// </summary>
    internal string Command { get; init; }

}

/// <summary>
/// The admin panel's catalog of actions, one per chat command, grouped by tab.
/// </summary>
/// <remarks>
/// The panel's script receives the catalog when it starts, limited to the actions the staff member's level allows,
/// and builds the generic tabs from it, so the actions are listed only here. Each action needs the level its chat
/// command needs. Character actions run through <see cref="CharacterActions"/>, moderation actions through
/// <see cref="AccountModeration"/>, and the staff member's own actions through the chat command itself.
/// </remarks>
internal static class PanelActions {

    internal const int MaxArguments = 3;

    private const string OptionalSuffix = " (optional)";

    internal static IReadOnlyList<PanelAction> All { get; } = [

        // Character.
        ForCharacter("character", "level", "Set Level", (t, a) => CharacterActions.SetLevel(t, a[0]), Arg("Level")),
        ForCharacter("character", "levelup", "Level Up", (t, _) => CharacterActions.LevelUp(t)),
        ForCharacter("character", "addxp", "Add XP", (t, a) => CharacterActions.AddExperience(t, a[0]), Arg("XP")),
        ForCharacter("character", "addgold", "Add Gold", (t, a) => CharacterActions.AddGold(t, a[0]), Arg("Gold")),
        ForCharacter("character", "maxgold", "Set Max Gold", (t, a) => CharacterActions.SetMaxGold(t, a[0]), Arg("Max gold")),
        ForCharacter("character", "health", "Set Health", (t, a) => CharacterActions.SetHealth(t, a[0]), Arg("Health")),
        ForCharacter("character", "mana", "Set Mana", (t, a) => CharacterActions.SetMana(t, a[0]), Arg("Mana")),
        ForCharacter("character", "energy", "Set Energy", (t, a) => CharacterActions.SetEnergy(t, a[0]), Arg("Energy")),
        ForCharacter("character", "refillhealth", "Refill Health", (t, _) => CharacterActions.RefillHealth(t)),
        ForCharacter("character", "refillmana", "Refill Mana", (t, _) => CharacterActions.RefillMana(t)),
        ForCharacter("character", "refillenergy", "Refill Energy", (t, _) => CharacterActions.RefillEnergy(t)),
        ForOnline("character", "maxhealth", "Max Health Until Relog", (t, a) => CharacterActions.SetMaxHealth(t, a[0]), Arg("Max health")),
        ForOnline("character", "maxmana", "Max Mana Until Relog", (t, a) => CharacterActions.SetMaxMana(t, a[0]), Arg("Max mana")),
        ForOnline("character", "maxenergy", "Max Energy Until Relog", (t, a) => CharacterActions.SetMaxEnergy(t, a[0]), Arg("Max energy")),
        ForCharacter("character", "cantriplevel", "Set Cantrip Level", (t, a) => CharacterActions.SetCantripLevel(t, a[0]), Arg("Cantrip level")),
        ForCharacter("character", "cantriplevelup", "Cantrip Level Up", (t, _) => CharacterActions.CantripLevelUp(t)),
        ForCharacter("character", "addtraining", "Add Training Points", (t, a) => CharacterActions.AddTrainingPoints(t, a[0]), Arg("Training points")),
        ForCharacter("character", "settraining", "Set Training Points", (t, a) => CharacterActions.SetTrainingPoints(t, a[0]), Arg("Training points")),
        ForCharacter("character", "potions", "Set Potions", (t, a) => CharacterActions.SetPotionMax(t, a[0]), Arg("Potions")),
        ForCharacter("character", "name", "Name Override", (t, a) => CharacterActions.SetNameOverride(t, a[0]), Arg("New name")),
        ForCharacter("character", "badgetitle", "Badge Title", (t, a) => CharacterActions.SetBadgeTitle(t, a[0]), Arg("Title key", optional: true)),
        ForOnline("character", "speed", "Speed Bonus", (t, a) => CharacterActions.SetSpeed(t, a[0]), Arg("Percent")),

        // Items.
        ForCharacter("items", "additem", "Add Item", (t, a) => CharacterActions.AddItem(t, a[0]), Arg("Item template ID")),
        ForCharacter("items", "addsnack", "Add Snack", (t, a) => CharacterActions.AddSnack(t, a[0]), Arg("Snack template ID")),
        ForCharacter("items", "addreagent", "Add Reagent", (t, a) => CharacterActions.AddReagent(t, a[0]), Arg("Reagent template ID")),
        ForCharacter("items", "addreagents", "Add Reagents", (t, a) => CharacterActions.AddReagents(t, a[0], a[1]), Arg("Reagent template ID"), Arg("Count")),
        ForCharacter("items", "house", "Give House", (t, a) => CharacterActions.GrantHouse(t, a[0]), Arg("House template ID")),
        ForCharacter("items", "banklimit", "Set Bank Limit", (t, a) => CharacterActions.SetBankLimit(t, a[0]), Arg("Bank limit")),
        ForCharacter("items", "expandbank", "Expand Bank", (t, _) => CharacterActions.ExpandBank(t)),

        // Pets.
        ForCharacter("pets", "petlevel", "Set Pet Level", (t, a) => CharacterActions.SetPetLevel(t, a[0]), Arg("Pet level")),
        ForOnline("pets", "petxp", "Add Pet XP", (t, a) => CharacterActions.AddPetExperience(t, a[0]), Arg("XP")),
        ForCharacter("pets", "pethappiness", "Set Pet Happiness", (t, a) => CharacterActions.SetPetHappiness(t, a[0]), Arg("Happiness")),
        ForCharacter("pets", "pettalent", "Give Pet Talent", (t, a) => CharacterActions.GivePetTalent(t, a[0]), Arg("Talent name")),
        ForCharacter("pets", "hatchlist", "List Hatch Parents", (t, _) => CharacterActions.Hatch(t, "")) with { Changes = false },
        ForCharacter("pets", "hatch", "Hatch Two Pets", (t, a) => CharacterActions.Hatch(t, $"{a[0]} {a[1]}"), Arg("First pet (number)"), Arg("Second pet (number)")),

        // Spells.
        ForCharacter("spells", "learn", "Learn Spell", (t, a) => CharacterActions.LearnSpell(t, a[0]), Arg("Spell template ID")),
        ForCharacter("spells", "unlearn", "Unlearn Spell", (t, a) => CharacterActions.UnlearnSpell(t, a[0]), Arg("Spell template ID")),
        ForCharacter("spells", "unlearnall", "Unlearn All Spells", (t, _) => CharacterActions.UnlearnAllSpells(t)),
        ForCharacter("spells", "allcantrips", "Learn All Cantrips", (t, _) => CharacterActions.LearnAllCantrips(t)),
        ForOnline("spells", "addtemp", "Add Temporary Spell", (t, a) => CharacterActions.AddTemporarySpell(t, a[0]), Arg("Spell template ID")),
        ForOnline("spells", "removetemp", "Remove Temporary Spell", (t, a) => CharacterActions.RemoveTemporarySpell(t, a[0]), Arg("Spell template ID")),

        // Quests.
        ForOnline("quests", "questlist", "List Quests", (t, _) => CharacterActions.RunQuestCommand(t, "list", "")) with { Changes = false },
        ForOnline("quests", "questinfo", "Quest Info", (t, a) => CharacterActions.RunQuestCommand(t, "info", a[0]), Arg("Quest name", optional: true)) with { Changes = false },
        ForOnline("quests", "completegoal", "Complete Goal", (t, a) => CharacterActions.RunQuestCommand(t, "completegoal", a[0]), Arg("Quest name", optional: true)),
        ForOnline("quests", "completequest", "Complete Quest", (t, a) => CharacterActions.RunQuestCommand(t, "complete", a[0]), Arg("Quest name")),
        ForOnline("quests", "grantquest", "Grant Quest", (t, a) => CharacterActions.RunQuestCommand(t, "grant", a[0]), Arg("Quest name")),
        ForOnline("quests", "forcegrant", "Force Grant Quest", (t, a) => CharacterActions.RunQuestCommand(t, "forcegrant", a[0]), Arg("Quest name")),
        ForOnline("quests", "removequest", "Remove Quest", (t, a) => CharacterActions.RunQuestCommand(t, "remove", a[0]), Arg("Quest name")),
        ForOnline("quests", "offerquest", "Offer Quest", (t, a) => CharacterActions.OfferQuest(t, a[0]), Arg("Quest name")),

        // Badges and PvP ratings.
        ForOnline("progress", "badgegrant", "Grant Badge", (t, a) => CharacterActions.RunBadgeCommand(t, "grant", a[0]), Arg("Badge ID or name")),
        ForOnline("progress", "badgeprogress", "Add Badge Progress", (t, a) => CharacterActions.RunBadgeCommand(t, "progress", a[0], a[1]), Arg("Counter key"), Arg("Amount")),
        ForOnline("progress", "badgelist", "List Badges", (t, _) => CharacterActions.RunBadgeCommand(t, "list", "")) with { Changes = false },
        ForOnline("progress", "badgereset", "Reset Badges", (t, _) => CharacterActions.RunBadgeCommand(t, "reset", "")),
        ForOnline("progress", "ratingshow", "Show PvP Rating", (t, a) => CharacterActions.RunRatingCommand(t, "show", "", a[0]), Arg("League", optional: true)) with { Changes = false },
        ForOnline("progress", "ratingset", "Set PvP Rating", (t, a) => CharacterActions.RunRatingCommand(t, "set", a[0], a[1]), Arg("Rating"), Arg("League", optional: true)),
        ForOnline("progress", "rankset", "Set PvP Rank", (t, a) => CharacterActions.RunRatingCommand(t, "rankset", a[0], a[1]), Arg("Rank index or name"), Arg("League", optional: true)),
        ForOnline("progress", "ratingclear", "Clear PvP Ratings", (t, a) => CharacterActions.RunRatingCommand(t, "clear", "", a[0]), Arg("League", optional: true)),
        ForOnline("progress", "seasonreset", "New PvP Season Record", (t, a) => CharacterActions.RunRatingCommand(t, "seasonreset", "", a[0]), Arg("League", optional: true)),

        // Dynamods.
        ForCharacter("dynamods", "dynamodlist", "List Dynamods", (t, _) => CharacterActions.ListDynamods(t)) with { Changes = false },
        ForCharacter("dynamods", "dynamodset", "Set Dynamod", (t, a) => CharacterActions.SetDynamod(t, a[0], a[1]), Arg("State (on or off)"), Arg("Client tag")),
        ForCharacter("dynamods", "dynamodtoggle", "Toggle Dynamod", (t, a) => CharacterActions.ToggleDynamod(t, a[0]), Arg("Client tag")),
        ForCharacter("dynamods", "dynamodremove", "Remove Dynamod", (t, a) => CharacterActions.RemoveDynamod(t, a[0]), Arg("Client tag")),

        // Moderation.
        ForAccount("moderation", "info", "Account Info", (c, _) => AccountModeration.Info(c.Actor, c.Username)) with { Changes = false },
        ForAccount("moderation", "infractions", "List Infractions", (c, _) => AccountModeration.Infractions(c.Actor, c.Username)) with { Changes = false },
        ForAccount("moderation", "warn", "Warn", (c, a) => AccountModeration.Warn(c.Actor, c.Character, c.Username, a[0]), Arg("Reason")),
        ForAccount("moderation", "removeinfraction", "Remove Infraction", (c, a) => AccountModeration.RemoveInfraction(c.Actor, c.Character, c.Username, a[0]), Arg("Infraction number")),
        ForAccount("moderation", "mute", "Mute", (c, a) => AccountModeration.Mute(c.Actor, c.Character, c.Actors, c.Username, a[0], a[1]), Arg("Duration, like 1d12h"), Arg("Reason")),
        ForAccount("moderation", "unmute", "Unmute", (c, _) => AccountModeration.Unmute(c.Actor, c.Character, c.Actors, c.Username)),
        ForAccount("moderation", "kick", "Kick", (c, a) => AccountModeration.Kick(c.Actor, c.Character, c.Actors, c.Username, a[0]), Arg("Reason", optional: true)),
        ForAccount("moderation", "ban", "Ban Account", (c, a) => AccountModeration.Ban(c.Actor, c.Character, c.Actors, c.Username, a[0], a[1]), Arg("Duration, like 7d"), Arg("Reason")),
        ForAccount("moderation", "baninfo", "Ban Info", (c, _) => AccountModeration.BanInfo(c.Actor, c.Username)) with { Changes = false },
        ForAccount("moderation", "unban", "Unban Account", (c, _) => AccountModeration.Unban(c.Actor, c.Character, c.Username)),
        ForServer("moderation", "banip", "Ban IP", (c, a) => AccountModeration.BanIp(c.Actor, c.Character, a[0], a[1], a[2]), Arg("IP address"), Arg("Duration, like 7d"), Arg("Reason")),
        ForServer("moderation", "unbanip", "Unban IP", (c, a) => AccountModeration.UnbanIp(c.Actor, c.Character, a[0]), Arg("IP address")),
        ForServer("moderation", "banmachine", "Ban Machine", (c, a) => AccountModeration.BanMachine(c.Actor, c.Character, a[0], a[1], a[2]), Arg("Machine ID"), Arg("Duration, like 7d"), Arg("Reason")),
        ForServer("moderation", "unbanmachine", "Unban Machine", (c, a) => AccountModeration.UnbanMachine(c.Actor, c.Character, a[0]), Arg("Machine ID")),

        // The staff member's own character.
        ForSelf("tools", "teleport", "Teleport", "teleport {0}", Arg("Zone")),
        ForSelf("tools", "gps", "Show Location", "debug gps"),
        ForSelf("tools", "disableobj", "Disable Object", "debug disableobj {0}", Arg("Zone tag")),
        ForSelf("tools", "enableobj", "Enable Object", "debug enableobj {0}", Arg("Zone tag")),
        ForSelf("tools", "setobj", "Set Object State", "debug setobj {0} {1}", Arg("State"), Arg("Object name")),
        ForSelf("tools", "cheatwin", "Win the Duel", "cheat win"),
        ForSelf("tools", "cinematics", "Toggle Cinematics", "cheat cinematic"),
        ForSelf("tools", "nofizzle", "Toggle No Fizzle", "cheat nofizzle"),
        ForSelf("tools", "groupinfo", "Group Info", "group info"),
        ForSelf("tools", "groupkick", "Remove From Group", "group kick {0}", Arg("Member name")),
        ForSelf("tools", "groupleader", "Make Group Leader", "group leader {0}", Arg("Member name")),
        ForSelf("tools", "groupdisband", "Disband Group", "group disband"),
        ForSelf("tools", "statue", "Statue Here", "statue here") with { Level = AuthLevel.Administrator },

    ];

    private static readonly Dictionary<string, PanelAction> s_byId = All.ToDictionary(action => action.Id);

    internal static PanelAction Find(string id) => id is not null && s_byId.TryGetValue(id, out var action) ? action : null;

    /// <summary>
    /// Whether an account's level allows an action. As for chat commands, the developer level counts as
    /// administrator outside debug builds.
    /// </summary>
    internal static bool Allows(Account actor, PanelAction action) {
        var level = action.Level;
#if !DEBUG
        if (level == AuthLevel.Developer) {
            level = AuthLevel.Administrator;
        }
#endif

        return actor.AuthLevel >= level;
    }

    /// <summary>
    /// The catalog replies for the panel's script: action TAB ID LABEL REACH ARGUMENT…, for each action the account
    /// may use. An optional argument's label says so.
    /// </summary>
    internal static IEnumerable<string[]> Describe(Account actor)
        => All.Where(action => Allows(actor, action))
            .Select(action => (string[]) [
                "action", action.Tab, action.Id, action.Label, ReachName(action.Reach),
                .. action.Arguments.Select(argument => argument.Optional ? argument.Label + OptionalSuffix : argument.Label)
            ]);

    /// <summary>
    /// Reads the arguments, trimmed and padded to <see cref="MaxArguments"/>, and checks the required ones are filled in.
    /// </summary>
    internal static bool TryReadArguments(PanelAction action, string[] fields, out string[] arguments,
                                          out AdminActionResult refusal) {
        arguments = new string[MaxArguments];
        for (var i = 0; i < arguments.Length; i++) {
            arguments[i] = i < fields.Length ? fields[i]?.Trim() ?? "" : "";
        }

        refusal = null;
        for (var i = 0; i < action.Arguments.Length; i++) {
            if (!action.Arguments[i].Optional && arguments[i].Length == 0) {
                refusal = AdminActionResult.Refuse($"Fill in {action.Arguments[i].Label}.");

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Runs a character action and audits a change. Callers check the staff member's level and their right to act
    /// on the character's account first.
    /// </summary>
    internal static AdminActionResult RunOnCharacter(PanelAction action, CharacterTarget target, string[] arguments,
                                                     Account actor, Wizard actorCharacter) {
        var wizard = target.Wizard;
        var name = wizard.PlayerNameBehavior.GetWizardName();
        if (action.Reach == PanelReach.CharacterOnline && !target.Online) {
            return AdminActionResult.Refuse($"{name} is offline, and {action.Label} needs the character online.");
        }

        AdminActionResult result;
        try {
            result = action.OnCharacter(target, arguments);
        }
        catch (Exception ex) {
            Logger.Error("Admin panel: {0} failed on character {1}: {2}", Logger.Args(action.Id, wizard.CharId, ex));

            return AdminActionResult.Refuse($"{action.Label} failed: {ex.Message}");
        }

        if (result.Succeeded && action.Changes) {
            var shown = arguments.Take(action.Arguments.Length).Where(argument => argument.Length > 0);
            AccountAdministration.Audit(actor, actorCharacter, AdminAuditAction.CharacterChanged,
                $"{action.Id} on {name} ({wizard.CharId}){(shown.Any() ? ": " + string.Join(", ", shown) : "")}");
        }

        return result;
    }

    /// <summary>
    /// The chat command line of a <see cref="PanelReach.Self"/> action. The command name comes from the catalog, so
    /// the arguments cannot pick another command.
    /// </summary>
    internal static string BuildCommand(PanelAction action, string[] arguments)
        => string.Format(action.Command, [.. arguments.Cast<object>()]).Trim();

    private static string ReachName(PanelReach reach) => reach switch {
        PanelReach.Account => "account",
        PanelReach.Server => "server",
        PanelReach.Character => "character",
        PanelReach.CharacterOnline => "online",
        PanelReach.Self => "self",
        _ => "",
    };

    private static PanelArgument Arg(string label, bool optional = false) => new(label, optional);

    private static PanelAction ForCharacter(string tab, string id, string label,
                                         Func<CharacterTarget, string[], AdminActionResult> run,
                                         params PanelArgument[] arguments)
        => new(tab, id, label, AuthLevel.QualityAssurance, PanelReach.Character, arguments) { OnCharacter = run };

    private static PanelAction ForOnline(string tab, string id, string label,
                                      Func<CharacterTarget, string[], AdminActionResult> run,
                                      params PanelArgument[] arguments)
        => new(tab, id, label, AuthLevel.QualityAssurance, PanelReach.CharacterOnline, arguments) { OnCharacter = run };

    private static PanelAction ForAccount(string tab, string id, string label,
                                       Func<AccountActionContext, string[], AdminActionResult> run,
                                       params PanelArgument[] arguments)
        => new(tab, id, label, AuthLevel.HallMonitor, PanelReach.Account, arguments) { OnAccount = run };

    private static PanelAction ForServer(string tab, string id, string label,
                                      Func<AccountActionContext, string[], AdminActionResult> run,
                                      params PanelArgument[] arguments)
        => new(tab, id, label, AuthLevel.HallMonitor, PanelReach.Server, arguments) { OnAccount = run };

    private static PanelAction ForSelf(string tab, string id, string label, string command,
                                    params PanelArgument[] arguments)
        => new(tab, id, label, AuthLevel.QualityAssurance, PanelReach.Self, arguments) { Command = command };

}
