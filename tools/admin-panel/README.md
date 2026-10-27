# Admin panel client files

The in-game admin panel is a client script, `Scripts/AdminPanel/Client.lua`, plus two GUI files. `AdminPanelService` starts the script for staff accounts when `[Admin Panel] Enabled = true` (or `IMLIGHT__Admin_Panel__Enabled=true`). The script adds an **Admin** button to the settings window. Each action needs the account's own auth level, and every change is written to the `AdminAudit` collection.

The client loads scripts only from its own archives, so each staff PC needs these files added to its `Data/GameData/Root.wad`. Players who aren't staff need nothing. The script never starts for them.

## Install on a staff PC

Close the game client first, then run:

```sh
python -I tools/admin-panel/build.py --client "<game client folder>" --install
```

The script builds the GUI files from the client's own settings, login, trade and banking windows, so no KingsIsle content is stored here. The trade and banking windows are read from `Data/GameData/_Shared-WorldData.wad`, which is never changed. The script writes the files to `tools/admin-panel/out/` (gitignored), then adds all three to `Root.wad`. The untouched archive is kept at `Data/Root.wad.adminpanel-original`, and every install starts from it. `--uninstall` restores it.

A client update replaces `Root.wad`, so install again after patching. Install again after updating Imlight too, when the panel's files changed.

## Using the panel

The panel opens on your own account. Type another username above the tabs and press **Look Up** (Hall Monitor and up) to load that account. The side panel shows the account's characters as heads: the game's student portrait for each character's gender and school, with its name and level, and "online" while its player is in the game. Click a head to select that character; the selected one also shows large at the top.

| Tab | Acts on | Does |
| --- | --- | --- |
| Account | the account | Auth level, lock and password, then Save. Create makes a new account from the same fields. Delete, pressed twice, deletes the account. |
| Crowns | the account | Gives or takes Crowns, or sets a new total. |
| Moderation | the account | Info, infractions, warnings, mutes, kicks and bans; bans of an IP address or machine ID. |
| Character | the selected character | Level, XP, gold, health, mana, energy, cantrip level, training points, potions, name, badge title, speed. |
| Items | the selected character | Items, snacks, reagents, house deeds, the bank limit. |
| Pets | the selected character | The equipped pet's level, XP, happiness and talents; hatching. |
| Spells | the selected character | Learning and unlearning spells. |
| Quests | the selected character | Listing, granting, completing, removing and offering quests. |
| Progress | the selected character | Badges and PvP ratings. |
| Dynamods | the selected character | Listing, setting, toggling and removing dynamods. |
| Tools | you | Teleporting, object states, combat cheats, your group, statues. These run the chat command, and its reply is in chat. |

On every tab but Account and Crowns, pick an action with the arrows, fill in its fields and press **Run**. The tab lists only the actions your auth level allows, each needing the level of its chat command. Acting on another account's character also needs Hall Monitor and outranking that account. With no output, the character tabs show the selected character's level, gold and stats.

A character's player can be offline. An online character is changed by its own session, so the change reaches the player at once. An offline character is loaded and changed in the database, and the player sees the change at the next login. Actions marked "(online)" need the player online: quests, badges and PvP ratings run through the session's own services, and some changes last only for the session (max health, mana and energy, temporary spells, speed).

A mute, unmute or ban reaches an online player's session at once, and a ban or kick closes the session in whichever zone or realm it is.

## Messages

The panel sends tab-separated fields in an `EXTENDEDBASE` `MSG_RAW_TEXT`, wrapped in `GAME_5 MSG_MESSAGE_PROCESS`. The client sends process messages only for protocols it has loaded, which rules out the script debugger's protocol. `AccountsPage.cs`, `PlayersPage.cs`, `CharactersPage.cs` and `ModerationPage.cs` document the requests and replies, and `PanelActions.cs` is the catalog of actions the script receives when it starts.

An action on an online character goes to that character's session as `ADMIN_112 MSG_ADMINCHARACTERACTION`. `AdminActionService` runs it there and answers with `MSG_ADMINACTIONREPLY`; the quest, badge and rating services answer the staff member the same way while carrying one out. The panel reports a session that does not answer within 5 seconds.
