--[[

	AdminPanel/Client.lua

	The admin panel. The server starts this script only for staff accounts.

	The script adds an Admin button to the settings window whenever that window exists. The button closes
	the settings window and opens the panel.

	The panel looks like the settings window with a side panel on its left:
	- The username bar above the tabs loads an account; every tab works on it.
	- The side panel shows the account's characters as heads, the selected one large at the top. The
	  character tabs act on the selected character, whether its player is online or not.
	- Account: change the account's fields, then save; Create makes a new account from the same fields;
	  Delete, pressed twice, deletes it.
	- Crowns: give or take Crowns, or type a new total and save.
	- Every other tab picks one of its actions with the arrows, takes up to three arguments and runs it.
	  The server sends the actions when the script starts, only those the staff member's level allows;
	  Tools runs chat commands on the staff member's own character.

	Requests and replies travel as tab-separated fields in a raw text message: the client only accepts
	messages from protocols it has loaded, and the script debugger protocol is not one of them. See
	AccountsPage.cs, PlayersPage.cs, CharactersPage.cs, ModerationPage.cs and PanelActions.cs on the server.

	The window names below must match the ones build.py writes into the GUI files.

]]

Include("API/Debug.lua");
Include("API/Utilities.lua");
Include("API/Globals.lua");
Include("API/ControlClasses.lua");
Include("API/WindowAnimations.lua");

TransportMessage = "MSG_RAW_TEXT";
SettingsWindowName = "SettingPage";
SettingsCloseButtonName = "OkButton";
SettingsButtonGui = "AdminPanelSettingsButton.gui";
PanelGui = "AdminPanel.gui";
OpenButtonName = "adminpanel_open";
PanelName = "ap_window";
PanelWidth = 960;

-- How often to look for the settings window, in seconds.
PollEvent = "AdminPanelPoll";
PollInterval = 0.5;

-- After an action on a character, its summary is loaded again once its session has saved the change.
RefreshEvent = "AdminPanelRefresh";
RefreshDelay = 1.5;

-- Auth levels as the server names them, and as the panel shows them.
Levels = { "None", "QualityAssurance", "HallMonitor", "Developer", "Administrator" };
LevelLabels = { "Player", "QA Tester", "Hall Monitor", "Developer", "Administrator" };

-- Markup for the smaller sentence lines, and the colors of request results.
SmallText = "<scale;0.75>";
StatusColors = { ok = "1E6B1E", error = "A01818" };
OnlineColor = "1E6B1E";

-- The tabs, left to right. Each shows a page with its row of buttons and a status line; the generic ones
-- share the action page and are filled from the catalog. "account" tabs act on the looked-up account,
-- "character" ones on the selected character.
Tabs = {
	{ id = "account", page = "ap_page_account", buttons = "ap_buttons_account", status = "ap_status", needs = "account" },
	{ id = "crowns", page = "ap_page_crowns", buttons = "ap_buttons_crowns", status = "ap_p_status", needs = "account" },
	{ id = "moderation", generic = true, needs = "account" },
	{ id = "character", generic = true, needs = "character" },
	{ id = "items", generic = true, needs = "character" },
	{ id = "pets", generic = true, needs = "character" },
	{ id = "spells", generic = true, needs = "character" },
	{ id = "quests", generic = true, needs = "character" },
	{ id = "progress", generic = true, needs = "character" },
	{ id = "dynamods", generic = true, needs = "character" },
	{ id = "tools", generic = true },
};
ActionPage = { page = "ap_page_action", buttons = "ap_buttons_action", status = "ap_a_status" };
ArgumentRows = 3;

-- The side panel's character rows, and the student portraits the heads use.
HeadSlots = 6;
Schools = { Fire = true, Ice = true, Storm = true, Myth = true, Life = true, Death = true, Balance = true };
NoPortrait = "GUI/Art/Art_Portrait_No_Selection.dds";

-- The output rows of the action page, and how many lines each shows.
OutputRows = { "ap_out1", "ap_out2" };
LinesPerRow = 2;

g_Root = nil;
g_Panel = nil;
g_Controls = {};

-- The staff member: { name, level, charId }, from the server when the script starts.
g_Self = nil;
-- The catalog: tab id -> list of { id, label, reach, args = { label, ... } }, and the chosen action per tab.
g_Actions = {};
g_ActionIndex = {};
-- The tab shown; kept between openings.
g_Tab = Tabs[1];

-- The looked-up account's name, its characters ({ id, name, school, gender, level, online, zone }) and how
-- many the server said it has. Nil until an account is loaded.
g_Account = nil;
g_Characters = {};
g_ExpectedCharacters = 0;
g_HeadPage = 1;
-- The selected character's id, and its summary lines.
g_Selected = nil;
g_Summary = {};
-- The action page's output lines, and the page of them shown.
g_Output = {};
g_OutputPage = 1;

-- The account last looked up or changed, as stored: { name, level, locked }. Nil until one is loaded.
g_Loaded = nil;
-- The Account tab's selectors' current choices.
g_Level = 1;
g_Locked = false;
-- The account whose Crowns were last looked up or changed: { name, crowns }. Nil until one is loaded.
g_Player = nil;
-- The account Delete was pressed for once; a second press deletes it.
g_DeleteArmed = nil;

function step(text)
	Log("AdminPanel: " .. text);
end

-- Sends an action and its fields. Tabs separate the fields, so they are removed from the values.
function send(action, fields)
	local text = action;
	for _, field in ipairs(fields or {}) do
		text = text .. "\t" .. string.gsub(field or "", "\t", " ");
	end
	SendProcessMessage(TransportMessage, { Message = text });
end

function split(text)
	local fields = {};
	local start = 1;
	while true do
		local tab = string.find(text, "\t", start, true);
		if not tab then
			table.insert(fields, string.sub(text, start));
			return fields;
		end
		table.insert(fields, string.sub(text, start, tab - 1));
		start = tab + 1;
	end
end

-- The fields from a position on, as a list.
function rest(fields, first)
	local list = {};
	for index = first, #fields do
		table.insert(list, fields[index]);
	end
	return list;
end

function trim(text)
	return (string.gsub(text or "", "^%s*(.-)%s*$", "%1"));
end

function levelIndex(name)
	for index, level in ipairs(Levels) do
		if level == name then
			return index;
		end
	end
	return 1;
end

function main()
	step("started");
	RegisterEvent("WB_BUTTONUP");
	RegisterEvent("WE_EDITDONE");
	RegisterEvent("MSG");
	g_Root = OpenClass(GetRootWindow());

	-- A panel left over from before a zone change belongs to no script; the server has already closed it.
	local leftover = g_Root.FindNamedWindow(PanelName);
	if leftover then
		leftover.ShowWindow(false);
		DestroyClass(leftover);
	end

	send("started");

	-- A repeating timer drives the polling, as in the client's own scripts. The engine's Sleep counts seconds
	-- and blocks event handling, so it is not used for this.
	RegisterEvent(PollEvent);
	RegisterEvent(RefreshEvent);
	StartTimer(PollEvent, PollInterval, false);

	while true do
		local event = GetEvent("");
		if event then
			if event.EventName == PollEvent then
				addSettingsButton();
			elseif event.EventName == RefreshEvent then
				if g_Panel and g_Selected then
					send("character.select", { g_Selected });
				end
			else
				handleEvent(event);
			end
		end
	end
end

function handleEvent(event)
	if event.EventName == "WB_BUTTONUP" then
		local name = event.Name or "";
		if name == OpenButtonName then
			openPanel();
		elseif not g_Panel then
			return;
		else
			handleButton(name);
		end
	elseif event.EventName == "WE_EDITDONE" and g_Panel and event.Name == "ap_edit_username" then
		-- Finishing the username looks it up, unless that account is already loaded.
		local name = username();
		if name ~= "" and name ~= g_Account then
			lookUp();
		end
	elseif event.EventName == "MSG" and event.MsgName == TransportMessage then
		handleReply(split(event.Message or ""));
	end
end

function handleButton(name)
	local tabId = string.match(name, "^ap_tab_(.+)$");
	local head = string.match(name, "^ap_head(%d+)_btn$");
	if tabId then
		showTab(findTab(tabId));
	elseif head then
		selectHead(tonumber(head) + 1);
	elseif name == "ap_btn_lookup" or name == "ap_a_btn_refresh" then
		lookUp();
	elseif name == "ap_btn_close" or name == "ap_p_btn_close" or name == "ap_a_btn_close" then
		closePanel();
	elseif name == "ap_heads_prev" then
		showHeadPage(g_HeadPage - 1);
	elseif name == "ap_heads_next" then
		showHeadPage(g_HeadPage + 1);
	elseif name == "ap_act_prev" then
		chooseAction(-1);
	elseif name == "ap_act_next" then
		chooseAction(1);
	elseif name == "ap_out_prev" then
		showOutputPage(g_OutputPage - 1);
	elseif name == "ap_out_next" then
		showOutputPage(g_OutputPage + 1);
	elseif name == "ap_a_btn_run" then
		runAction();
	elseif name == "ap_a_btn_clear" then
		clearAction();
	elseif name == "ap_p_btn_give" then
		giveCrowns();
	elseif name == "ap_p_btn_save" then
		saveCrowns();
	elseif name == "ap_btn_create" then
		create();
	elseif name == "ap_btn_save" then
		save();
	elseif name == "ap_btn_delete" then
		delete();
	elseif name == "ap_level_prev" then
		chooseLevel(-1);
	elseif name == "ap_level_next" then
		chooseLevel(1);
	elseif name == "ap_locked_prev" or name == "ap_locked_next" then
		g_Locked = not g_Locked;
		showChoices();
	end
end

function handleReply(fields)
	local kind = fields[1];
	if kind == "status" then
		setStatus(fields[2], fields[3]);
	elseif kind == "output" then
		receiveOutput(fields[2], rest(fields, 3));
	elseif kind == "self" then
		g_Self = { name = fields[2], level = levelIndex(fields[3]), charId = fields[4] };
	elseif kind == "action" then
		receiveAction(fields);
	elseif kind == "chars" then
		receiveCharacters(fields[2], tonumber(fields[3]) or 0);
	elseif kind == "char" then
		receiveCharacter(fields);
	elseif kind == "charinfo" then
		if fields[2] == g_Selected then
			g_Summary = rest(fields, 3);
			showOutput();
		end
	elseif kind == "account" then
		showAccount(fields[2], fields[3], fields[4] == "1", fields[5], fields[6]);
	elseif kind == "player" then
		showPlayer(fields[2], fields[3], fields[4], fields[5]);
	end
end

-- Adds the Admin button to the settings window if the window exists and does not have it yet.
function addSettingsButton()
	local settings = g_Root.FindNamedWindow(SettingsWindowName);
	if not settings then
		return;
	end

	local existing = settings.FindNamedWindow(OpenButtonName);
	if existing then
		DestroyClass(existing);
	else
		local button = CreateGUIClass(SettingsButtonGui, true);
		if button then
			button.DetachSelf();
			settings.AttachWindow(button);
			step("added the Admin button to the settings window");
		else
			step("could not load " .. SettingsButtonGui);
		end
	end
	DestroyClass(settings);
end

-- Closes the settings window the way its own OK button does.
function closeSettings()
	local settings = g_Root.FindNamedWindow(SettingsWindowName);
	if not settings then
		return;
	end

	local ok = settings.FindNamedWindow(SettingsCloseButtonName);
	if ok then
		-- The engine's Sleep counts seconds.
		ok.SetButtonDown(true);
		Sleep(0.1);
		ok.SetButtonDown(false);
		DestroyClass(ok);
	end
	DestroyClass(settings);
end

function openPanel()
	closeSettings();

	if not g_Panel then
		g_Panel = g_Root.FindNamedWindow(PanelName) or CreateGUIClass(PanelGui, false);
		if not g_Panel then
			step("could not load " .. PanelGui);
			return;
		end
		g_Controls = {};
		fitPanel();
	end

	g_Panel.ShowWindow(true);
	send("open");
	resetForms();
	showTab(g_Tab);
	showHeads();

	-- Start on the staff member's own account.
	if username() == "" and g_Self then
		setText("ap_edit_username", g_Self.name);
	end
	if username() ~= "" then
		lookUp();
	end
end

function closePanel()
	setText("ap_edit_password", "");
	g_Panel.ShowWindow(false);
	send("close");
end

-- Shrinks the panel on a screen too narrow for it, with the scale animation the client's own scripts use.
-- A call to a window function the client lacks stops the script, so only functions its scripts use are called.
function fitPanel()
	local width = g_Root.GetWindowWidth();
	if not width or width <= 0 or width >= PanelWidth then
		return;
	end
	g_Panel.PushAnimation(CreateWinAnimScaleTime(width / PanelWidth, 0.01));
	step("scaled the panel to the screen width " .. width);
end

-- The control with a window name, looked up once.
function getControl(name)
	local control = g_Controls[name];
	if control == nil then
		control = g_Panel.FindNamedWindow(name) or false;
		if not control then
			step(PanelGui .. " has no " .. name);
		end
		g_Controls[name] = control;
	end
	return control or nil;
end

function show(name, shown)
	local control = getControl(name);
	if control then
		control.ShowWindow(shown);
	end
end

function setText(name, text)
	local control = getControl(name);
	if control then
		control.SetText(text);
	end
end

function getText(name)
	local control = getControl(name);
	if control then
		return control.GetText() or "";
	end
	return "";
end

function username()
	return trim(getText("ap_edit_username"));
end

-- Empties the Account and Crowns forms, so nothing of a previous account is left on screen.
function resetForms()
	g_Loaded = nil;
	g_Level = 1;
	g_Locked = false;
	g_DeleteArmed = nil;
	setText("ap_edit_password", "");
	setText("ap_summary", "");
	setText("ap_details", "");
	setText("ap_status", "");
	showValues();

	g_Player = nil;
	setText("ap_p_edit_crowns", "");
	setText("ap_p_edit_give", "");
	setText("ap_p_summary", "");
	setText("ap_p_details", "");
	setText("ap_p_status", "");
end

function lookUp()
	local name = username();
	if name == "" then
		setStatus("error", "Type a username first.");
		return;
	end
	if name ~= g_Account then
		resetForms();
	end
	g_DeleteArmed = nil;
	setStatus("info", "Looking up " .. name .. "...");
	send("lookup", { name });
end

-- Tabs.

function findTab(id)
	for _, tab in ipairs(Tabs) do
		if tab.id == id then
			return tab;
		end
	end
	return Tabs[1];
end

-- The page, buttons and status line a tab shows.
function pageOf(tab)
	if tab.generic then
		return ActionPage;
	end
	return tab;
end

-- Shows one tab's page and buttons, and hides the others.
function showTab(tab)
	g_Tab = tab;
	g_DeleteArmed = nil;
	local shownPage = pageOf(tab);
	-- The tabs are one checkbox group, so clicking one checks it and clears the others. Scripts cannot set a
	-- checkbox (the client stops a script that calls SetChecked), so only a click changes the tab.
	for _, other in ipairs(Tabs) do
		local page = pageOf(other);
		if page ~= shownPage then
			show(page.page, false);
			show(page.buttons, false);
		end
	end
	show(shownPage.page, true);
	show(shownPage.buttons, true);

	if tab.generic then
		g_Output = {};
		g_OutputPage = 1;
		showAction();
		setStatus(nil, "");
		if tab.needs == "character" and not g_Selected then
			setStatus("info", "Pick a character on the left.");
		elseif tab.needs == "account" and not g_Account then
			setStatus("info", "Look an account up first.");
		end
	end
end

-- Characters.

function receiveCharacters(name, count)
	g_Account = name;
	g_Characters = {};
	g_ExpectedCharacters = count;
	g_HeadPage = 1;
	setText("ap_edit_username", name);
	if count == 0 then
		g_Selected = nil;
		g_Summary = {};
		setStatus(nil, "");
		showHeads();
		showOutput();
	end
end

function receiveCharacter(fields)
	local character = {
		id = fields[3], name = fields[4], school = fields[5], gender = fields[6],
		level = fields[7], online = fields[8] == "1", zone = fields[9]
	};

	-- A single character sent again after a change replaces its old entry.
	if fields[2] == "-" then
		for index, existing in ipairs(g_Characters) do
			if existing.id == character.id then
				g_Characters[index] = character;
				showHeads();
				return;
			end
		end
		return;
	end

	table.insert(g_Characters, character);
	if #g_Characters < g_ExpectedCharacters then
		return;
	end

	-- The whole list is in: keep the selection if it is still there, else pick a sensible one.
	if not findCharacter(g_Selected) then
		g_Selected = nil;
		if g_Self and g_Account == g_Self.name and findCharacter(g_Self.charId) then
			g_Selected = g_Self.charId;
		elseif #g_Characters == 1 then
			g_Selected = g_Characters[1].id;
		end
	end
	setStatus(nil, "");
	showHeads();
	selectCharacter(g_Selected);
end

function findCharacter(id)
	for index, character in ipairs(g_Characters) do
		if character.id == id then
			return character, index;
		end
	end
	return nil;
end

function selectHead(slot)
	local character = g_Characters[(g_HeadPage - 1) * HeadSlots + slot];
	if character then
		selectCharacter(character.id);
	end
end

function selectCharacter(id)
	g_Selected = id;
	g_Summary = {};
	g_Output = {};
	g_OutputPage = 1;
	showHeads();
	showOutput();
	if id then
		send("character.select", { id });
		if g_Tab.needs == "character" then
			setStatus(nil, "");
		end
	end
end

function portraitOf(character)
	if not Schools[character.school] then
		return NoPortrait;
	end
	local student = character.gender == "Female" and "Girl" or "Boy";
	return "GUI/NpcPortraits/Art_Portrait_" .. student .. "_" .. character.school .. ".dds";
end

function setPortrait(name, texture)
	local sprite = getControl(name);
	if sprite then
		sprite.SetMaterialByName(texture);
	end
end

-- Shows the heads of the page of characters shown, the selected one large at the top, and the pager.
function showHeads()
	local pages = math.max(1, math.ceil(#g_Characters / HeadSlots));
	g_HeadPage = math.min(math.max(g_HeadPage, 1), pages);
	for slot = 1, HeadSlots do
		local character = g_Characters[(g_HeadPage - 1) * HeadSlots + slot];
		local prefix = "ap_head" .. (slot - 1);
		show(prefix, character ~= nil);
		if character then
			local line = character.name .. "<br>Level " .. character.level .. " " .. character.school;
			if character.online then
				line = line .. " <color;" .. OnlineColor .. ">online</color>";
			end
			setText(prefix .. "_text", "<scale;0.65>" .. line);
			setPortrait(prefix .. "_face", portraitOf(character));
			show(prefix .. "_ring", character.id == g_Selected);
		end
	end

	show("ap_heads", pages > 1);
	setText("ap_heads_value", "<center>" .. g_HeadPage .. " / " .. pages .. "</center>");

	local selected = findCharacter(g_Selected);
	if selected then
		setPortrait("ap_portrait", portraitOf(selected));
		setText("ap_char_name", "<center><scale;0.8>" .. selected.name .. "</center>");
	else
		setPortrait("ap_portrait", NoPortrait);
		setText("ap_char_name", "");
	end

	local footer = "";
	if g_Account then
		local count = #g_Characters;
		footer = g_Account .. ": " .. count .. " character" .. (count == 1 and "" or "s");
	end
	setText("ap_side_status", "<center><scale;0.7>" .. footer .. "</center>");
end

function showHeadPage(page)
	g_HeadPage = page;
	showHeads();
end

-- The action page.

function receiveAction(fields)
	local tab = fields[2];
	if not g_Actions[tab] then
		g_Actions[tab] = {};
		g_ActionIndex[tab] = 1;
	end
	table.insert(g_Actions[tab], { id = fields[3], label = fields[4], reach = fields[5], args = rest(fields, 6) });
end

function currentAction()
	local actions = g_Actions[g_Tab.id] or {};
	return actions[g_ActionIndex[g_Tab.id] or 1], #actions;
end

function chooseAction(change)
	local _, count = currentAction();
	if count == 0 then
		return;
	end
	local index = (g_ActionIndex[g_Tab.id] or 1) + change;
	if index < 1 then
		index = count;
	elseif index > count then
		index = 1;
	end
	g_ActionIndex[g_Tab.id] = index;
	showAction();
end

-- Shows the chosen action and as many argument rows as it takes.
function showAction()
	local action, count = currentAction();
	if not action then
		setText("ap_act_value", "<center><scale;0.7>None for your level</center>");
	else
		local label = action.label;
		if action.reach == "online" then
			label = label .. " (online)";
		end
		setText("ap_act_value", "<center><scale;0.65>" .. label .. "</center>");
	end
	for row = 1, ArgumentRows do
		local argument = action and action.args[row];
		show("ap_a_row" .. row, argument ~= nil);
		if argument then
			setText("ap_a_row" .. row .. "_label", argument);
		end
		setText("ap_arg" .. row, "");
	end
	showOutput();
end

function runAction()
	local action = currentAction();
	if not action then
		setStatus("error", "Your level has no actions on this tab.");
		return;
	end

	local fields = { action.id };
	for row = 1, #action.args do
		table.insert(fields, getText("ap_arg" .. row));
	end

	if action.reach == "self" then
		send("tools.run", fields);
	elseif action.reach == "account" or action.reach == "server" then
		if action.reach == "account" and not g_Account then
			setStatus("error", "Look an account up first.");
			return;
		end
		table.insert(fields, 1, g_Account or "");
		send("moderation.run", fields);
	else
		if not g_Selected then
			setStatus("error", "Pick a character on the left first.");
			return;
		end
		table.insert(fields, 1, g_Selected);
		send("character.run", fields);
		StartTimer(RefreshEvent, RefreshDelay, true);
	end

	g_Output = {};
	g_OutputPage = 1;
	showOutput();
	setStatus("info", "Working...");
end

function clearAction()
	for row = 1, ArgumentRows do
		setText("ap_arg" .. row, "");
	end
	g_Output = {};
	g_OutputPage = 1;
	showOutput();
	setStatus(nil, "");
end

function receiveOutput(mode, lines)
	if mode == "set" then
		g_Output = {};
		g_OutputPage = 1;
	end
	for _, line in ipairs(lines) do
		if line ~= "" then
			table.insert(g_Output, line);
		end
	end
	showOutput();
end

-- The lines the output rows show: the last action's, or else the selected character's summary on a
-- character tab.
function outputLines()
	if #g_Output > 0 then
		return g_Output;
	end
	if g_Tab.needs == "character" then
		return g_Summary;
	end
	return {};
end

function showOutput()
	local lines = outputLines();
	local perPage = #OutputRows * LinesPerRow;
	local pages = math.max(1, math.ceil(#lines / perPage));
	g_OutputPage = math.min(math.max(g_OutputPage, 1), pages);
	local first = (g_OutputPage - 1) * perPage;
	for row, name in ipairs(OutputRows) do
		local shown = {};
		for offset = 1, LinesPerRow do
			local line = lines[first + (row - 1) * LinesPerRow + offset];
			if line then
				table.insert(shown, line);
			end
		end
		setText(name, "<scale;0.65>" .. table.concat(shown, "<br>"));
	end

	show("ap_out_prev", pages > 1);
	show("ap_out_next", pages > 1);
	setText("ap_out_page", pages > 1 and ("<center><scale;0.7>" .. g_OutputPage .. "/" .. pages .. "</center>") or "");
end

function showOutputPage(page)
	g_OutputPage = page;
	showOutput();
end

-- Account tab.

function create()
	local name = username();
	local password = getText("ap_edit_password");
	if name == "" or password == "" then
		setStatus("error", "To create an account, fill in a new username and a password.");
		return;
	end
	setText("ap_edit_password", "");
	setStatus(nil, "");
	send("accounts.create", { name, password, Levels[g_Level], g_Locked and "1" or "0" });
end

function save()
	local name = username();
	if not g_Loaded then
		setStatus("error", "Look up an account first, then change it and press Save.");
		return;
	end
	if name ~= g_Loaded.name then
		setStatus("error", "The username changed. Press Look Up to load " .. name .. " first.");
		return;
	end

	-- Only changed fields are sent; an empty field leaves that setting as it is.
	local level = "";
	if Levels[g_Level] ~= g_Loaded.level then
		level = Levels[g_Level];
	end
	local locked = "";
	if g_Locked ~= g_Loaded.locked then
		locked = g_Locked and "1" or "0";
	end
	local password = getText("ap_edit_password");
	if level == "" and locked == "" and password == "" then
		setStatus("info", "Nothing to save: change the level, the lock or the password first.");
		return;
	end

	setText("ap_edit_password", "");
	setStatus(nil, "");
	send("accounts.save", { name, level, locked, password });
end

-- Deleting an account cannot be undone, so it takes a second press.
function delete()
	if not g_Loaded or username() ~= g_Loaded.name then
		setStatus("error", "Look up the account to delete first.");
		return;
	end
	if g_DeleteArmed ~= g_Loaded.name then
		g_DeleteArmed = g_Loaded.name;
		setStatus("error", "Press Delete again to delete " .. g_Loaded.name .. " and all its characters for good.");
		return;
	end
	g_DeleteArmed = nil;
	setStatus(nil, "");
	send("accounts.delete", { g_Loaded.name });
end

function chooseLevel(change)
	g_Level = g_Level + change;
	if g_Level < 1 then
		g_Level = #Levels;
	elseif g_Level > #Levels then
		g_Level = 1;
	end
	showChoices();
end

-- Shows the selectors' choices.
function showValues()
	setText("ap_level_value", "<center><scale;0.9>" .. LevelLabels[g_Level] .. "</center>");
	setText("ap_locked_value", "<center>" .. (g_Locked and "Yes" or "No") .. "</center>");
end

-- Shows the selectors' choices, and which of them differ from the loaded account.
function showChoices()
	showValues();

	if g_Loaded and username() == g_Loaded.name then
		local changes = {};
		if Levels[g_Level] ~= g_Loaded.level then
			table.insert(changes, "auth level to " .. LevelLabels[g_Level]);
		end
		if g_Locked ~= g_Loaded.locked then
			table.insert(changes, g_Locked and "lock the account" or "unlock the account");
		end
		if #changes > 0 then
			setStatus("info", "Unsaved: " .. table.concat(changes, ", ") .. ". Press Save to apply.");
		else
			setStatus(nil, "");
		end
	end
end

function showAccount(name, level, locked, summary, details)
	g_Loaded = { name = name, level = level, locked = locked };
	g_Level = levelIndex(level);
	g_Locked = locked;
	setText("ap_summary", SmallText .. name .. ": " .. (summary or ""));
	setText("ap_details", SmallText .. (details or ""));
	showValues();
end

-- Crowns tab.

-- Reads a whole number of Crowns, allowing a sign and thousands separators. Nil when it isn't one.
function parseCrowns(text)
	local digits = string.gsub(trim(text), ",", "");
	if not string.find(digits, "^[+-]?%d+$") then
		return nil;
	end
	return tonumber(digits);
end

function formatCrowns(crowns)
	local text = tostring(crowns);
	while true do
		local grouped, count = string.gsub(text, "^(-?%d+)(%d%d%d)", "%1,%2");
		text = grouped;
		if count == 0 then
			return text;
		end
	end
end

function giveCrowns()
	local name = username();
	local amount = parseCrowns(getText("ap_p_edit_give"));
	if name == "" then
		setStatus("error", "Type a username first.");
		return;
	end
	if not amount or amount == 0 then
		setStatus("error", "Enter how many Crowns to give, like 500, or to take away, like -500.");
		return;
	end
	setText("ap_p_edit_give", "");
	setStatus(nil, "");
	send("players.crowns", { name, tostring(amount) });
end

-- Saves the Crowns row as a change from the looked-up balance, so a purchase made meanwhile is kept.
function saveCrowns()
	local name = username();
	if not g_Player then
		setStatus("error", "Look up an account first, then change its Crowns and press Save.");
		return;
	end
	if name ~= g_Player.name then
		setStatus("error", "The username changed. Press Look Up to load " .. name .. " first.");
		return;
	end
	local crowns = parseCrowns(getText("ap_p_edit_crowns"));
	if not crowns or crowns < 0 then
		setStatus("error", "Enter the new number of Crowns, like 5,000.");
		return;
	end
	if crowns == g_Player.crowns then
		setStatus("info", "Nothing to save: type a new number of Crowns first.");
		return;
	end
	setStatus(nil, "");
	send("players.crowns", { name, tostring(crowns - g_Player.crowns) });
end

function showPlayer(name, crowns, summary, details)
	g_Player = { name = name, crowns = parseCrowns(crowns) or 0 };
	setText("ap_p_edit_crowns", formatCrowns(g_Player.crowns));
	setText("ap_p_summary", SmallText .. (summary or ""));
	setText("ap_p_details", SmallText .. (details or ""));
end

-- Shows a result on the tab shown; kind is ok, error, info or nil. An empty text clears the line.
function setStatus(kind, text)
	local line = pageOf(g_Tab).status;
	if (text or "") == "" then
		setText(line, "");
		return;
	end
	local color = StatusColors[kind];
	if color then
		text = "<color;" .. color .. ">" .. text .. "</color>";
	end
	setText(line, SmallText .. text);
end
