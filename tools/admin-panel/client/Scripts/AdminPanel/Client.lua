--[[

	AdminPanel/Client.lua

	The admin panel. The server starts this script only for staff accounts.

	The script adds an Admin button to the settings window whenever that window exists. The button closes
	the settings window and opens the panel.

	The panel looks like a page of the settings window, with a tab per section:
	- Accounts: look an account up, change its fields, then save; Create makes a new account from the
	  same fields.
	- Players: look an account up to see its Crowns, then give or take some, or type a new total and save.

	Requests and replies travel as tab-separated fields in a raw text message: the client only accepts
	messages from protocols it has loaded, and the script debugger protocol is not one of them. Requests
	are named after their page ("accounts.lookup", "players.crowns"); see AccountsPage.cs and
	PlayersPage.cs on the server for the fields.

	The window names below must match the ones build.py writes into the GUI files.

]]

Include("API/Debug.lua");
Include("API/Utilities.lua");
Include("API/Globals.lua");
Include("API/ControlClasses.lua");

TransportMessage = "MSG_RAW_TEXT";
SettingsWindowName = "SettingPage";
SettingsCloseButtonName = "OkButton";
SettingsButtonGui = "AdminPanelSettingsButton.gui";
PanelGui = "AdminPanel.gui";
OpenButtonName = "adminpanel_open";
PanelName = "ap_window";

-- How often to look for the settings window, in seconds.
PollEvent = "AdminPanelPoll";
PollInterval = 0.5;

-- Auth levels as the server names them, and as the panel shows them.
Levels = { "None", "QualityAssurance", "HallMonitor", "Developer", "Administrator" };
LevelLabels = { "Player", "QA Tester", "Hall Monitor", "Developer", "Administrator" };

-- Markup for the smaller sentence lines, and the colors of request results.
SmallText = "<scale;0.75>";
StatusColors = { ok = "1E6B1E", error = "A01818" };

-- Each page's window, row of buttons, title and result line.
Pages = {
	accounts = { window = "ap_page_accounts", buttons = "ap_buttons_accounts", title = "Accounts", status = "ap_status" },
	players = { window = "ap_page_players", buttons = "ap_buttons_players", title = "Players", status = "ap_p_status" },
};
Tabs = { ap_tab_accounts = "accounts", ap_tab_players = "players" };

g_Root = nil;
g_Panel = nil;
g_Controls = {};
-- The page shown; kept between openings.
g_Page = "accounts";

-- The account last looked up or changed, as stored: { name, level, locked }. Nil until one is loaded.
g_Loaded = nil;
-- The selectors' current choices.
g_Level = 1;
g_Locked = false;
-- The account whose Crowns were last looked up or changed: { name, crowns }. Nil until one is loaded.
g_Player = nil;

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
	StartTimer(PollEvent, PollInterval, false);

	while true do
		local event = GetEvent("");
		if event then
			if event.EventName == PollEvent then
				addSettingsButton();
			else
				handleEvent(event);
			end
		end
	end
end

function handleEvent(event)
	if event.EventName == "WB_BUTTONUP" then
		local name = event.Name;
		if name == OpenButtonName then
			openPanel();
		elseif not g_Panel then
			return;
		elseif Tabs[name] then
			showPage(Tabs[name]);
		elseif name == "ap_btn_close" or name == "ap_p_btn_close" then
			closePanel();
		elseif name == "ap_p_btn_lookup" then
			lookUpPlayer();
		elseif name == "ap_p_btn_give" then
			giveCrowns();
		elseif name == "ap_p_btn_save" then
			saveCrowns();
		elseif name == "ap_btn_lookup" then
			lookUp();
		elseif name == "ap_btn_create" then
			create();
		elseif name == "ap_btn_save" then
			save();
		elseif name == "ap_level_prev" then
			chooseLevel(-1);
		elseif name == "ap_level_next" then
			chooseLevel(1);
		elseif name == "ap_locked_prev" or name == "ap_locked_next" then
			g_Locked = not g_Locked;
			showChoices();
		end
	elseif event.EventName == "WE_EDITDONE" and g_Panel and event.Name == "ap_edit_username" then
		-- Finishing the username looks it up, unless that account is already loaded.
		local name = username();
		if name ~= "" and not (g_Loaded and g_Loaded.name == name) then
			lookUp();
		end
	elseif event.EventName == "WE_EDITDONE" and g_Panel and event.Name == "ap_p_edit_username" then
		local name = playerName();
		if name ~= "" and not (g_Player and g_Player.name == name) then
			lookUpPlayer();
		end
	elseif event.EventName == "MSG" and event.MsgName == TransportMessage then
		handleReply(split(event.Message or ""));
	end
end

function handleReply(fields)
	if fields[1] == "status" then
		setStatus(fields[2], fields[3]);
	elseif fields[1] == "account" then
		showAccount(fields[2], fields[3], fields[4] == "1", fields[5], fields[6]);
	elseif fields[1] == "player" then
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
		local names = { "ap_title", "ap_edit_username", "ap_edit_password", "ap_level_value", "ap_locked_value",
			"ap_summary", "ap_details", "ap_status", "ap_p_edit_username", "ap_p_edit_crowns", "ap_p_edit_give",
			"ap_p_summary", "ap_p_details", "ap_p_status" };
		for _, page in pairs(Pages) do
			table.insert(names, page.window);
			table.insert(names, page.buttons);
		end
		for _, name in ipairs(names) do
			g_Controls[name] = g_Panel.FindNamedWindow(name);
			if not g_Controls[name] then
				step(PanelGui .. " has no " .. name);
			end
		end
	end

	resetForm();
	showPage(g_Page);
	g_Panel.ShowWindow(true);
	send("open");
end

function closePanel()
	setText("ap_edit_password", "");
	g_Panel.ShowWindow(false);
	send("close");
end

-- Starts every opening with an empty form.
function resetForm()
	g_Loaded = nil;
	g_Level = 1;
	g_Locked = false;
	setText("ap_edit_username", "");
	setText("ap_edit_password", "");
	setText("ap_summary", "");
	setText("ap_details", "");
	setText("ap_status", "");
	showValues();

	g_Player = nil;
	setText("ap_p_edit_username", "");
	setText("ap_p_edit_crowns", "");
	setText("ap_p_edit_give", "");
	setText("ap_p_summary", "");
	setText("ap_p_details", "");
	setText("ap_p_status", "");
end

-- Shows one page with its buttons and title, and hides the others.
function showPage(name)
	g_Page = name;
	for pageName, page in pairs(Pages) do
		local shown = pageName == name;
		for _, part in ipairs({ page.window, page.buttons }) do
			if g_Controls[part] then
				g_Controls[part].ShowWindow(shown);
			end
		end
	end
	setText("ap_title", "<center>" .. Pages[name].title .. "</center>");
end

function username()
	return trim(getText("ap_edit_username"));
end

function lookUp()
	local name = username();
	if name == "" then
		setStatus("error", "Type a username first.");
		return;
	end
	setStatus(nil, "");
	send("accounts.lookup", { name });
end

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
	setText("ap_edit_username", name);
	setText("ap_summary", SmallText .. name .. ": " .. (summary or ""));
	setText("ap_details", SmallText .. (details or ""));
	setStatus(nil, "");
	showValues();
end

-- Players page.

function playerName()
	return trim(getText("ap_p_edit_username"));
end

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

function lookUpPlayer()
	local name = playerName();
	if name == "" then
		setStatus("error", "Type a username first.");
		return;
	end
	setStatus(nil, "");
	send("players.lookup", { name });
end

function giveCrowns()
	local name = playerName();
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
	local name = playerName();
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
	setText("ap_p_edit_username", name);
	setText("ap_p_edit_crowns", formatCrowns(g_Player.crowns));
	setText("ap_p_summary", SmallText .. (summary or ""));
	setText("ap_p_details", SmallText .. (details or ""));
	setText("ap_p_status", "");
end

-- Shows a result on the page shown; kind is ok, error, info or nil. An empty text clears the line.
function setStatus(kind, text)
	local line = Pages[g_Page].status;
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

function setText(name, text)
	local control = g_Controls[name];
	if control then
		control.SetText(text);
	end
end

function getText(name)
	local control = g_Controls[name];
	if control then
		return control.GetText() or "";
	end
	return "";
end
