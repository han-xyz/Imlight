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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.AdminPanel;

/// <summary>
/// Staff account commands typed in a terminal on the server machine, with no game client or character.
/// </summary>
/// <remarks>
/// The server listens on a Unix socket that only processes on its own machine (in Docker, its own container) can
/// reach, so whoever can use it already controls the server. <see cref="RunClient"/> is the other end, run as
/// <c>dotnet Imlight.Director.dll console [command]</c>. Each connection carries one command line, and the server
/// answers with lines of text and closes it.
/// Commands act as the configured admin account: the admin panel's checks apply to it, and the admin audit log
/// records its name for every change.
/// </remarks>
public static class ServerConsole {

    private const string SocketFileName = "imlight-console.sock";
    private const UnixFileMode OwnerOnlyDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static string SocketDirectory {
        get {
            var configured = ConfigurationManager.GetSetting("Admin Panel.ConsoleSocketDirectory");

            return Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? "run" : configured, AppContext.BaseDirectory);
        }
    }

    private static string SocketPath => Path.Combine(SocketDirectory, SocketFileName);

    private static bool TrustAllLocalPeers
        => bool.TryParse(ConfigurationManager.GetSetting("Admin Panel.ConsoleTrustAllLocalPeers"), out var trust) && trust;

    private static readonly string[] s_help = [
        "account list                               List every account.",
        "account info <user>                        Show an account.",
        "account create <user> <password> [level]   Create an account, optionally with an auth level.",
        "account password <user> <password>         Change an account's password.",
        "account level <user> <level>               Change an account's auth level.",
        "account lock <user>                        Stop an account from logging in.",
        "account unlock <user>                      Let a locked account log in again.",
        "crowns <user> [change]                     Show an account's Crowns, or add (500) or take away (-500).",
        $"Auth levels: {string.Join(", ", Enum.GetNames<AuthLevel>())}.",
        "Words are separated by spaces, so passwords cannot contain spaces."
    ];

    /// <summary>
    /// Starts answering console commands on a background thread. Failing to listen is logged, not thrown: the game
    /// runs without the console.
    /// </summary>
    /// <param name="actors">Reaches the sessions of online players, whose Crowns are changed through them.</param>
    /// <param name="actorUsername">The account the commands act as.</param>
    public static void Listen(IActorRefFactory actors, string actorUsername) {
        var socketPath = SocketPath;
        try {
            if (!PrepareSocketDirectory(SocketDirectory)) {
                return;
            }

            // A socket file left by an earlier run would block the bind.
            File.Delete(socketPath);
            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            File.SetUnixFileMode(socketPath, OwnerOnlyFile);
            listener.Listen(4);

            var thread = new Thread(() => Accept(listener, actors, actorUsername)) {
                IsBackground = true,
                Name = "ServerConsole"
            };
            thread.Start();
            Logger.Information("Server console ready: dotnet Imlight.Director.dll console");
        }
        catch (Exception ex) {
            Logger.Warning("Server console unavailable: could not listen on {0}: {1}", Logger.Args(socketPath, ex.Message));
        }
    }

    private static bool PrepareSocketDirectory(string directory) {
        if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory, OwnerOnlyDirectory);
        }

        if (ConsoleSocketPolicy.IsDirectoryAcceptable(ConsoleSocketNative.GetOwnerUid(directory),
                ConsoleSocketNative.ServerUid, File.GetUnixFileMode(directory))) {
            return true;
        }

        Logger.Warning("Server console disabled: {0} must be owned by the server's user and closed to group and others.",
            Logger.Args(directory));

        return false;
    }

    /// <summary>
    /// Sends a command to the server running on this machine and prints its reply. With no command, reads commands
    /// until "exit" or the end of input.
    /// </summary>
    /// <returns>The exit code: 0, or 1 when no server answered.</returns>
    public static int RunClient(string[] args) {
        if (args.Length > 0) {
            return Send(string.Join(' ', args)) ? 0 : 1;
        }

        Console.WriteLine("Imlight server console. Type help for the commands, exit to leave.");
        while (true) {
            Console.Write("imlight> ");
            var line = Console.ReadLine()?.Trim();
            if (line is null or "exit" or "quit") {
                return 0;
            }

            if (line.Length > 0 && !Send(line)) {
                return 1;
            }
        }
    }

    private static bool Send(string line) {
        try {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(SocketPath));
            using var stream = new NetworkStream(socket);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine(line);

            while (reader.ReadLine() is { } reply) {
                Console.WriteLine(reply);
            }

            return true;
        }
        catch (SocketException) {
            Console.Error.WriteLine("No Imlight server is answering. Is it running and done booting?");

            return false;
        }
    }

    // An exception escaping this thread would end the server, so none may.
    private static void Accept(Socket listener, IActorRefFactory actors, string actorUsername) {
        while (true) {
            try {
                using var client = listener.Accept();
                if (!ConsoleSocketPolicy.IsPeerAcceptable(ConsoleSocketNative.GetPeerUid(client), ConsoleSocketNative.ServerUid,
                        TrustAllLocalPeers)) {
                    Logger.Warning("Server console: refused a connection from another user.");

                    continue;
                }

                using var stream = new NetworkStream(client);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                if (reader.ReadLine() is not { } line) {
                    continue;
                }

                List<string> reply;
                try {
                    reply = Execute(actors, actorUsername, line);
                }
                catch (Exception ex) {
                    Logger.Error("Server console: the command failed: {0}", Logger.Args(ex));
                    reply = [$"The command failed: {ex.Message}"];
                }

                foreach (var replyLine in reply) {
                    writer.WriteLine(replyLine);
                }
            }
            catch (Exception ex) {
                Logger.Warning("Server console: a connection failed: {0}", Logger.Args(ex.Message));
            }
        }
    }

    private static List<string> Execute(IActorRefFactory actors, string actorUsername, string line) {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) {
            return [];
        }

        var group = words[0].ToLowerInvariant();
        var command = words.Length > 1 ? words[1].ToLowerInvariant() : "";
        // The arguments are never logged: some are passwords.
        Logger.Information("Server console: {0}", Logger.Args(group == "account" ? $"account {command}" : group));

        if (group is "help" or "?") {
            return [.. s_help];
        }

        var actor = AccountCollection.GetAccount(actorUsername, waitForIndex: true);
        if (actor is null) {
            return [$"The admin account {actorUsername} does not exist, so the console cannot act."];
        }

        return group switch {
            "account" => ExecuteAccount(actor, command, words[2..]),
            "crowns" => Crowns(actors, actor, words[1..]),
            _ => ["Unknown command. Type help for the commands."]
        };
    }

    private static List<string> ExecuteAccount(Account actor, string command, string[] args) {
        switch (command) {
            case "list":
                return List();
            case "info" when args.Length == 1:
                return Info(actor, args[0]);
            case "create" when args.Length is 2 or 3:
                return Create(actor, args[0], args[1], args.Length == 3 ? args[2] : null);
            case "password" when args.Length == 2:
                return [AccountAdministration.ChangePassword(actor, null, args[0], args[1]).Message];
            case "level" or "authlevel" when args.Length == 2:
                return [AccountAdministration.SetAuthLevel(actor, null, args[0], args[1]).Message];
            case "lock" when args.Length == 1:
                return [AccountAdministration.SetLocked(actor, null, args[0], true).Message];
            case "unlock" when args.Length == 1:
                return [AccountAdministration.SetLocked(actor, null, args[0], false).Message];
            default:
                var usage = s_help.Where(help => help.StartsWith($"account {command} ")).ToList();

                return usage.Count > 0
                    ? ["Usage: " + usage[0]]
                    : ["Unknown account command. Type help for the commands."];
        }
    }

    private static List<string> List() {
        var accounts = AccountCollection.GetAccountSummaries();
        if (accounts.Count == 0) {
            return ["No accounts."];
        }

        var width = accounts.Max(account => account.Username.Length);

        return accounts
            .Select(account => $"{account.Username.PadRight(width)}  {account.AuthLevel}{(account.IsLocked ? ", locked" : "")}")
            .ToList();
    }

    private static List<string> Info(Account actor, string username) {
        var result = AccountAdministration.Lookup(actor, username, out var account);
        if (!result.Succeeded) {
            return [result.Message];
        }

        var history = account.InfractionHistory;
        var lastLogin = account.LastLoginTime == default
            ? "never logged in"
            : $"last login {account.LastLoginTime:yyyy-MM-dd HH:mm} UTC from {account.LastLoginIp}";
        var lines = new List<string> {
            $"{account.Username}: {account.AuthLevel}{(account.IsLocked ? ", locked" : "")}",
            $"Created {account.CreationTime:yyyy-MM-dd}, {lastLogin}",
            $"{account.CharacterIds.Count} characters, {AccountAdministration.FormatCrowns(account.Crowns)} Crowns, "
                + $"{history.Infractions.Count} infractions"
        };
        if (history.IsCurrentlyBanned) {
            lines.Add($"Banned until {history.BanEndsAt:yyyy-MM-dd HH:mm} UTC");
        }

        if (history.IsCurrentlyMuted) {
            lines.Add($"Muted until {history.MuteEndsAt:yyyy-MM-dd HH:mm} UTC");
        }

        return lines;
    }

    private static List<string> Create(Account actor, string username, string password, string level) {
        var created = AccountAdministration.CreateAccount(actor, null, username, password);
        if (!created.Succeeded || level is null) {
            return [created.Message];
        }

        return [created.Message, AccountAdministration.SetAuthLevel(actor, null, username, level).Message];
    }

    private static List<string> Crowns(IActorRefFactory actors, Account actor, string[] args) {
        if (args.Length == 1) {
            var result = AccountAdministration.Lookup(actor, args[0], out var account);

            return [result.Succeeded
                ? $"{account.Username} has {AccountAdministration.FormatCrowns(account.Crowns)} Crowns."
                : result.Message];
        }

        if (args.Length != 2) {
            return ["Usage: " + s_help.First(help => help.StartsWith("crowns "))];
        }

        if (!int.TryParse(args[1].Replace(",", ""), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out var change)) {
            return ["Enter a whole number of Crowns, like 500 or -500."];
        }

        return [AccountAdministration.AdjustCrowns(actor, null, args[0], change,
            (accountId, amount) => AccountAdministration.AdjustOnlineCrowns(actors, accountId, amount), out _).Message];
    }

}
