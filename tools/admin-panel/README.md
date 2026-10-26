# Admin panel client files

The in-game admin panel is a client script, `Scripts/AdminPanel/Client.lua`, plus two GUI files. `AdminPanelService` starts the script for staff accounts when `[Admin Panel] Enabled = true` (or `IMLIGHT__Admin_Panel__Enabled=true`). The script adds an **Admin** button to the settings window. Each action needs the account's own auth level, and every change is written to the `AdminAudit` collection.

The client loads scripts only from its own archives, so each staff PC needs these files added to its `Data/GameData/Root.wad`. Players who aren't staff need nothing. The script never starts for them.

## Install on a staff PC

Close the game client first, then run:

```sh
python -I tools/admin-panel/build.py --client "<game client folder>" --install
```

The script builds the GUI files from the client's own settings and login windows, so no KingsIsle content is stored here. It writes them to `tools/admin-panel/out/` (gitignored), then adds all three files to `Root.wad`. The untouched archive is kept at `Data/Root.wad.adminpanel-original`, and every install starts from it. `--uninstall` restores it.

A client update replaces `Root.wad`, so install again after patching.

## Messages

The panel sends tab-separated fields in an `EXTENDEDBASE` `MSG_RAW_TEXT`, wrapped in `GAME_5 MSG_MESSAGE_PROCESS`. The client sends process messages only for protocols it has loaded, which rules out the script debugger's protocol. `AccountsPage.cs` and `PlayersPage.cs` document the requests and replies.
