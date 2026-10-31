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
 * SPIRALDB SYNC TESTS
 * ========================================================================
 * 
 * PURPOSE:
 * Behaviour tests for how SpiralDB prepares its local checkout: cloning into
 * a mounted empty directory, refusing a directory that holds other files, and
 * taking the remote's files when the remote moves.
 * 
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Han
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using Imlight.Common;
using Imlight.CoreLib.WizardData;
using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class SpiralDbSyncTests : IDisposable {

    private readonly string _root = Path.Combine(Path.GetTempPath(), "spiraldb-sync-" + Guid.NewGuid().ToString("N"));

    public SpiralDbSyncTests() {
        Directory.CreateDirectory(_root);
        var iniFilePath = Path.Combine(_root, "test.ini");
        File.WriteAllText(iniFilePath, string.Join('\n',
            "[Logging]",
            $"LogPath = {Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt")}",
            "LogLevel = ERROR",
            "[Database]",
            "SpiralDBRemoteUrl = unset",
            "SpiralDBBranch = unset",
            "SpiralDBLocalPath = unset",
            "SpiralDBAutoFetch = true",
            "SpiralDBDisableRemote = false",
            "SpiralDBFetchTimeout = 30",
            "SpiralDBRollbackOnFailure = true"));
        ConfigurationManager.Initialize(iniFilePath);
    }

    public void Dispose() {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Sync_EmptyExistingDirectory_Clones() {
        CreateRemote();
        var dataPath = Path.Combine(_root, "data");
        Directory.CreateDirectory(dataPath);

        SpiralDB.SyncRepository(dataPath);

        Assert.True(Repository.IsValid(dataPath));
        Assert.True(File.Exists(Path.Combine(dataPath, "marker.json")));
    }

    [Fact]
    public void Sync_NonEmptyNonRepo_Throws() {
        CreateRemote();
        var dataPath = Path.Combine(_root, "data");
        Directory.CreateDirectory(dataPath);
        File.WriteAllText(Path.Combine(dataPath, "stray.txt"), "x");

        var ex = Assert.Throws<InvalidOperationException>(() => SpiralDB.SyncRepository(dataPath));

        Assert.Contains(dataPath, ex.Message);
    }

    [Fact]
    public void Sync_RemoteMoved_TakesRemoteFiles() {
        CreateRemote();
        var dataPath = Path.Combine(_root, "data");
        SpiralDB.SyncRepository(dataPath);
        var remoteTip = CommitToRemote("quests.json", remove: "marker.json");

        SpiralDB.SyncRepository(dataPath);

        using var repository = new Repository(dataPath);
        Assert.Equal(remoteTip, repository.Head.Tip.Sha);
        Assert.True(File.Exists(Path.Combine(dataPath, "quests.json")));
        Assert.False(File.Exists(Path.Combine(dataPath, "marker.json")));
        Assert.Equal([".git", "quests.json"], Directory.GetFileSystemEntries(dataPath).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Sync_UpToDate_KeepsCheckout() {
        CreateRemote();
        var dataPath = Path.Combine(_root, "data");
        SpiralDB.SyncRepository(dataPath);
        File.WriteAllText(Path.Combine(dataPath, "local.txt"), "x");

        SpiralDB.SyncRepository(dataPath);

        Assert.True(File.Exists(Path.Combine(dataPath, "local.txt")));
    }

    private void CreateRemote() {
        var remotePath = Path.Combine(_root, "remote");
        Repository.Init(remotePath);
        CommitToRemote("marker.json");

        using var repository = new Repository(remotePath);
        SpiralDB.s_remoteUrl = remotePath;
        SpiralDB.s_cloneDepth = 0;
        SpiralDB.s_branch = repository.Head.FriendlyName;
    }

    private string CommitToRemote(string add, string remove = null) {
        var remotePath = Path.Combine(_root, "remote");
        File.WriteAllText(Path.Combine(remotePath, add), "{}");

        using var repository = new Repository(remotePath);
        Commands.Stage(repository, add);
        if (remove != null) {
            Commands.Remove(repository, remove);
        }
        var signature = new Signature("test", "test@example.com", DateTimeOffset.UtcNow);
        return repository.Commit(add, signature, signature).Sha;
    }
}
