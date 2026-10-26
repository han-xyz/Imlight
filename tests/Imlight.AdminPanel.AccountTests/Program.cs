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
 *
 * ========================================================================
 * ADMIN PANEL ACCOUNT ACTION CHECKS
 * ========================================================================
 *
 * PURPOSE:
 * Exercises the account actions behind the admin panel and the account commands against a dedicated loopback
 * RavenDB database.
 *
 * USAGE EXAMPLE:
 * Start a throwaway RavenDB server on 127.0.0.1:18080 (unsecured, local only), then:
 * dotnet run --project tests/Imlight.AdminPanel.AccountTests -- <empty test directory>
 *
 * NOTE:
 * Only http://127.0.0.1:18080 and AdminPanelTests databases are used.
 *
 * Created by: BobFromIHT with Claude
 * Version: KALI 1.0
 * Last Updated: 10/26/2026
 */

using System.Reflection;
using Imlight.Common;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

if (args.Length != 1) {
    throw new ArgumentException("Usage: AccountTests <explicit-test-directory>");
}

var workDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(workDirectory);
Directory.SetCurrentDirectory(workDirectory);
var database = "AdminPanelTests-" + Guid.NewGuid().ToString("N");

var configPath = Path.Combine(workDirectory, "test.ini");
File.WriteAllText(configPath, $$"""
    [Database]
    PlayerDatabaseUrl = http://127.0.0.1:18080
    PlayerDatabaseName = {{database}}
    PlayerDatabaseCertificatePath = ./no-test-certificate.pfx
    DatabaseMaxNumberOfRequestsPerSession = 32
    DatabaseRequestTimeoutInSeconds = 30
    DatabaseWaitForNonStaleResultsTimeout = 10
    [Login Server]
    MaxAllowedCharactersPerAccount = 6
    [Logging]
    LogPath = ./account-tests.log
    LogLevel = ERROR
    SeqSinkUrl = http://127.0.0.1:1
    """);
ConfigurationManager.Initialize(configPath);

using var store = OpenStore(database);
store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(database)));
var playerStoreField = typeof(PlayerDatabase).BaseType?.GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Player database test injection is unavailable.");
playerStoreField.SetValue(PlayerDatabase.Instance, store);

var actions = typeof(Account).Assembly.GetType("Imlight.CoreLib.Game.AdminPanel.AccountAdministration")
    ?? throw new InvalidOperationException("AccountAdministration is missing.");
var checks = 0;

void Check(bool condition, string name) {
    if (!condition) {
        throw new InvalidOperationException("FAILED: " + name);
    }
    checks++;
    Console.WriteLine("PASS: " + name);
}

(bool Succeeded, string Message) Run(string action, params object?[] arguments) {
    var method = actions.GetMethod(action, BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{action} is missing.");
    var result = method.Invoke(null, arguments)!;
    var type = result.GetType();

    return ((bool) type.GetProperty("Succeeded")!.GetValue(result)!, (string) type.GetProperty("Message")!.GetValue(result)!);
}

// Sessions act through accounts loaded with their infraction history.
Account Staff(string name, AuthLevel level) {
    var account = new Account(name, "", DatabaseUtilities.CreateHashedPassword("staff-password")) { AuthLevel = level };
    Check(AccountCollection.CreateAccount(account), $"{name} staff account stored");
    account.InfractionHistory = new InfractionHistory(account.AccountId, []);

    return account;
}

int AuditCount(AdminAuditAction action) {
    using var session = store.OpenSession();

    return session.Query<AdminAuditEntry>(collectionName: AdminAuditCollection.CollectionName)
        .Customize(query => query.WaitForNonStaleResults())
        .Count(entry => entry.Action == action);
}

var admin = Staff("admin", AuthLevel.Administrator);
var monitor = Staff("monitor", AuthLevel.HallMonitor);
var player = new Account("player", "", DatabaseUtilities.CreateHashedPassword("player-password"));
Check(AccountCollection.CreateAccount(player), "player account stored");

// The account's own auth level gates everything.
var refused = Run("CreateAccount", player, null, "alice", "first-password");
Check(!refused.Succeeded && refused.Message.Contains("needs auth level"), "a player account cannot use admin actions");
Check(AccountCollection.GetAccount("alice", waitForIndex: true) is null, "a refused create writes nothing");

// Create and password.
Check(Run("CreateAccount", admin, null, " alice ", "first-password").Succeeded, "an administrator can create an account");
var alice = AccountCollection.GetAccount("alice", waitForIndex: true);
Check(alice is not null && alice.AuthLevel == AuthLevel.None, "the new account is stored under its trimmed name as a player");
Check(alice!.PasswordHash == DatabaseUtilities.CreateHashedPassword("first-password") && !alice.PasswordHash.Contains("first-password"),
    "the password is stored as a hash");
Check(!Run("CreateAccount", admin, null, "alice", "other").Succeeded, "a duplicate username is refused");
Check(!Run("CreateAccount", admin, null, "bob", "").Succeeded && AccountCollection.GetAccount("bob", waitForIndex: true) is null,
    "an empty password is refused");
Check(AuditCount(AdminAuditAction.AccountCreated) == 1, "only the successful create is audited");
Check(Run("ChangePassword", admin, null, "alice", "second-password").Succeeded &&
    AccountCollection.GetAccount("alice", waitForIndex: true)!.PasswordHash == DatabaseUtilities.CreateHashedPassword("second-password"),
    "an administrator can change a password");
Check(!Run("ChangePassword", admin, null, "nobody", "x").Succeeded, "changing a missing account's password is refused");
Check(AuditCount(AdminAuditAction.PasswordChanged) == 1, "the password change is audited");
using (var session = store.OpenSession()) {
    var details = session.Query<AdminAuditEntry>(collectionName: AdminAuditCollection.CollectionName)
        .Customize(query => query.WaitForNonStaleResults()).ToList().Select(entry => entry.Detail ?? "");
    Check(!details.Any(detail => detail.Contains("first-password") || detail.Contains("second-password")),
        "no password reaches the audit log");
}

// Auth levels.
Check(Run("SetAuthLevel", admin, null, "alice", "hallmonitor").Succeeded &&
    AccountCollection.GetAccount("alice", waitForIndex: true)!.AuthLevel == AuthLevel.HallMonitor, "auth levels are set by name");
Check(Run("SetAuthLevel", admin, null, "alice", "0").Succeeded &&
    AccountCollection.GetAccount("alice", waitForIndex: true)!.AuthLevel == AuthLevel.None, "auth levels are set by number");
Check(!Run("SetAuthLevel", admin, null, "alice", "Wizard").Succeeded, "an unknown auth level is refused");
Check(!Run("SetAuthLevel", admin, null, "admin", "None").Succeeded, "nobody can change their own auth level");
Check(AuditCount(AdminAuditAction.AuthLevelChanged) == 2, "both auth level changes are audited");

// A hall monitor's limits. Lacking the level is a plain refusal; acting on an account it does not outrank is an infraction.
Check(Run("CreateAccount", monitor, null, "carol", "carol-password").Succeeded, "a hall monitor can create an account");
Check(!Run("ChangePassword", monitor, null, "carol", "x").Succeeded, "a hall monitor cannot change passwords");
Check(!Run("SetAuthLevel", monitor, null, "carol", "HallMonitor").Succeeded, "a hall monitor cannot set auth levels");
Check(AccountCollection.GetAccount("monitor", waitForIndex: true)!.InfractionHistory.Infractions.Count == 0,
    "refusals for a missing level add no infractions");
Check(!Run("SetLocked", monitor, null, "admin", true).Succeeded && !AccountCollection.GetAccount("admin", waitForIndex: true)!.IsLocked,
    "a hall monitor cannot lock a higher account");
Check(AccountCollection.GetAccount("monitor", waitForIndex: true)!.InfractionHistory.Infractions.Count == 1,
    "trying to act on a higher account is recorded as an infraction");

// Lock, unlock and info.
Check(Run("SetLocked", monitor, null, "carol", true).Succeeded && AccountCollection.GetAccount("carol", waitForIndex: true)!.IsLocked,
    "a hall monitor can lock an account");
var lookupArguments = new object?[] { monitor, "carol", null };
var lookup = Run("Lookup", lookupArguments);
Check(lookup.Succeeded && lookupArguments[2] is Account { Username: "carol", IsLocked: true }, "lookup returns the locked account");
Check(Run("SetLocked", monitor, null, "carol", false).Succeeded && !AccountCollection.GetAccount("carol", waitForIndex: true)!.IsLocked,
    "a hall monitor can unlock an account");
Check(AuditCount(AdminAuditAction.AccountLocked) == 1 && AuditCount(AdminAuditAction.AccountUnlocked) == 1,
    "locks and unlocks are audited");
Check(!Run("Lookup", monitor, "nobody", null).Succeeded, "looking up a missing account is refused");

// The Accounts page: replies are tab-separated fields, as the panel receives them.
var page = typeof(Account).Assembly.GetType("Imlight.CoreLib.Game.AdminPanel.AccountsPage")
    ?? throw new InvalidOperationException("AccountsPage is missing.");
List<string[]> Page(Account actor, string request, params string[] fields) {
    var replies = new List<string[]>();
    page.GetMethod("Handle", BindingFlags.Static | BindingFlags.NonPublic)!
        .Invoke(null, [actor, null, request, fields, (Action<string[]>) replies.Add]);
    return replies;
}
string[] Status(List<string[]> replies) => replies.Last(reply => reply[0] == "status");
string[]? Shown(List<string[]> replies) => replies.LastOrDefault(reply => reply[0] == "account");

var created = Page(admin, "create", "dave", "dave-password", "Developer", "1");
Check(Status(created)[1] == "ok" && Shown(created) is ["account", "dave", "Developer", "1", ..],
    "create applies the chosen level and lock and shows the new account");
Check(AccountCollection.GetAccount("dave", waitForIndex: true)!.PasswordHash == DatabaseUtilities.CreateHashedPassword("dave-password"),
    "create stores the password hash");
var shownDave = Page(admin, "lookup", "dave");
Check(shownDave.All(reply => reply[0] != "status") && Shown(shownDave) is ["account", "dave", "Developer", "1", var summary, var shownDetails] &&
    summary.StartsWith("Created ") && summary.Contains("never logged in") && shownDetails.StartsWith("0 characters, 0 infractions"),
    "lookup shows level, lock, summary and details without a status line");
var loginTime = new DateTime(2026, 10, 25, 18, 30, 0, DateTimeKind.Utc);
Check(AccountCollection.RecordLogin(AccountCollection.GetAccount("dave", waitForIndex: true)!.AccountId, loginTime,
    "100.64.0.7:51234", 77), "a login is recorded");
var storedLogin = AccountCollection.GetAccount("dave", waitForIndex: true)!;
Check(storedLogin.LastLoginTime == loginTime && storedLogin.LastLoginIp == "100.64.0.7:51234" && storedLogin.LastLoginMachineId == 77 &&
    storedLogin.PasswordHash == DatabaseUtilities.CreateHashedPassword("dave-password") && storedLogin.AuthLevel == AuthLevel.Developer,
    "the recorded login is stored without touching the rest of the account");
Check(Shown(Page(admin, "lookup", "dave")) is [.., var loggedSummary, var loggedDetails] &&
    loggedSummary.Contains($"last login {loginTime.ToLocalTime():M/d/yyyy h:mm tt}") && loggedDetails.EndsWith("last IP 100.64.0.7"),
    "lookup shows the last login in local time and the IP without its port");
Check(Status(Page(admin, "lookup", "nobody"))[2].Contains("press Create"), "a missing account suggests creating it");
Check(Status(Page(admin, "lookup", " "))[1] == "error", "an empty username is refused");
Check(Status(Page(admin, "save", "dave", "", "", ""))[1] == "info", "saving nothing changes nothing");
var saved = Page(admin, "save", "dave", "QualityAssurance", "0", "new-password");
Check(Status(saved)[1] == "ok" && Shown(saved) is ["account", "dave", "QualityAssurance", "0", ..] &&
    AccountCollection.GetAccount("dave", waitForIndex: true)!.PasswordHash == DatabaseUtilities.CreateHashedPassword("new-password"),
    "save applies level, unlock and password together");
var partial = Page(monitor, "save", "dave", "", "1", "x");
Check(Status(partial) is ["status", "error", var partialMessage] && partialMessage.Contains("locked") &&
    partialMessage.Contains("Stopped:") && AccountCollection.GetAccount("dave", waitForIndex: true)!.IsLocked,
    "a partial save reports what was done and what was refused");
Check(Status(Page(admin, "rename", "dave"))[1] == "error", "an unknown page request is refused");

// The Players page: Crowns.
var players = typeof(Account).Assembly.GetType("Imlight.CoreLib.Game.AdminPanel.PlayersPage")
    ?? throw new InvalidOperationException("PlayersPage is missing.");
var changeType = typeof(Account).Assembly.GetType("Imlight.CoreLib.Game.AdminPanel.CrownChange")
    ?? throw new InvalidOperationException("CrownChange is missing.");
var adjustType = typeof(Func<,,>).MakeGenericType(typeof(ulong), typeof(int), changeType);
// What the account's online session answers, or null when the account is offline.
Func<ulong, int, object?> online = (_, _) => null;
var onlineCalls = 0;
var adjust = typeof(OnlineSession).GetMethod(nameof(OnlineSession.Adjust))!.MakeGenericMethod(changeType)
    .CreateDelegate(adjustType, new OnlineSession((account, change) => { onlineCalls++; return online(account, change); }));
object Change(bool applied, int before, int after, string? problem = null) => Activator.CreateInstance(changeType, applied, before, after, problem)!;
List<string[]> Players(Account actor, string request, params string[] fields) {
    var replies = new List<string[]>();
    players.GetMethod("Handle", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
        [actor, null, request, fields, (Action<string[]>) replies.Add, adjust]);
    return replies;
}
string[]? Player(List<string[]> replies) => replies.LastOrDefault(reply => reply[0] == "player");
int Crowns(string name) => AccountCollection.GetAccount(name, waitForIndex: true)!.Crowns;

Check(AccountCollection.CreateAccount(new Account("erin", "", DatabaseUtilities.CreateHashedPassword("erin-password"))),
    "erin's account stored");
var erin = Players(monitor, "lookup", "erin");
Check(erin.All(reply => reply[0] != "status") && Player(erin) is ["player", "erin", "0", var erinSummary, "0 characters"] &&
    erinSummary == "erin: None, offline", "a lookup shows the account's Crowns, level and presence");
Check(Status(Players(monitor, "crowns", "erin", "500"))[1] == "error" && Crowns("erin") == 0 && onlineCalls == 0,
    "a hall monitor cannot change Crowns");
Check(Status(Players(admin, "crowns", "erin", "lots"))[1] == "error", "a non-number is refused");
Check(Status(Players(admin, "crowns", "erin", "0"))[1] == "error", "a change of 0 is refused");
var given = Players(admin, "crowns", "erin", "2,500");
Check(Status(given) is ["status", "ok", var givenMessage] && givenMessage == "erin now has 2,500 Crowns (+2,500)." &&
    Player(given) is ["player", "erin", "2,500", ..], "giving Crowns to an offline account shows the new balance");
Check(Crowns("erin") == 2500 && onlineCalls == 1, "an offline account's balance is saved after checking for its session");
Check(Status(Players(admin, "crowns", "erin", "-4000")) is ["status", "error", var shortMessage] &&
    shortMessage == "erin has only 2,500 Crowns." && Crowns("erin") == 2500, "Crowns cannot go below zero");
Check(Status(Players(admin, "crowns", "erin", "-2500"))[1] == "ok" && Crowns("erin") == 0, "Crowns can be taken down to zero");
Check(Status(Players(admin, "crowns", "erin", int.MaxValue.ToString()))[1] == "ok" &&
    Status(Players(admin, "crowns", "erin", "1"))[2] == "That is more Crowns than an account can hold.",
    "a balance cannot overflow");
Check(AuditCount(AdminAuditAction.CrownsChanged) == 3, "each applied Crowns change is audited");

// An online account changes through its session, which owns the cached balance.
online = (_, change) => Change(true, 100, 100 + change);
var viaSession = Players(admin, "crowns", "erin", "500");
Check(Status(viaSession)[2] == "erin now has 600 Crowns (+500)." && Player(viaSession) is ["player", "erin", "600", ..] &&
    Crowns("erin") == int.MaxValue, "an online account's change is left to its session and shows the session's balance");
online = (_, _) => Change(false, 100, 100);
Check(Status(Players(admin, "crowns", "erin", "-500"))[2] == "erin has only 100 Crowns.", "the session's refusal is reported");
online = (_, _) => Change(false, 0, 0, "The player's session did not answer.");
Check(Status(Players(admin, "crowns", "erin", "5")) is ["status", "error", "The player's session did not answer."] &&
    AuditCount(AdminAuditAction.CrownsChanged) == 4, "an unanswered change is reported and not audited");
online = (_, _) => null;
Check(Status(Players(admin, "lookup", "nobody"))[1] == "error", "looking up a missing player is refused");

Console.WriteLine($"Completed {checks} checks in {database}.");

static DocumentStore OpenStore(string database) {
    var result = new DocumentStore { Urls = ["http://127.0.0.1:18080"], Database = database };
    var bridge = typeof(Wizard).Assembly.GetType("Imlight.CoreLib.WizardData.RavenSystemTextJsonBridge")
        ?? throw new InvalidOperationException("Raven serialization bridge is missing.");
    bridge.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [result.Conventions]);
    result.Initialize();
    return result;
}

sealed class OnlineSession(Func<ulong, int, object?> answer) {
    public T? Adjust<T>(ulong account, int change) where T : class => (T?) answer(account, change);
}
