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
 * LOCAL WAD CACHE TESTS
 * ========================================================================
 *
 * PURPOSE:
 * Behaviour tests for how the local cache serves a game file: from a plain copy on disk,
 * written again when its size no longer matches the cached file.
 *
 * USAGE EXAMPLE:
 * dotnet test tests/Imlight.Tests/Imlight.Tests.csproj
 *
 * NOTE:
 * LocalWadCache reads LocalWadCachePath once, so every test here shares one cache folder.
 *
 * TODO:
 *
 * Created by: BobFromIHT
 * Version: KALI 1.0
 * Last Updated: 10/31/2026
 */

using System;
using System.IO;
using System.Text;
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class LocalWadCacheTests {

    private static readonly string s_cacheDirectory = Path.Combine(Path.GetTempPath(), "imlight-wadcache-" + Guid.NewGuid().ToString("N"));

    public LocalWadCacheTests() {
        Directory.CreateDirectory(s_cacheDirectory);
        var iniFilePath = Path.Combine(s_cacheDirectory, "test.ini");
        File.WriteAllText(iniFilePath, string.Join('\n',
            "[Logging]",
            $"LogPath = {Path.Combine(Path.GetTempPath(), "imlight-tests", "log.txt")}",
            "LogLevel = ERROR",
            "[Patch Server]",
            $"LocalWadCachePath = {Path.Combine(s_cacheDirectory, "wad.db")}"));
        ConfigurationManager.Initialize(iniFilePath);
    }

    [Fact]
    public void CachedWad_ReadsFromCopyOnDisk() {
        LocalWadCache.CacheWad("TestZone-Copy", ArchiveOf("marker.txt", "hello"));

        var wad = LocalWadCache.GetCachedWad("TestZone-Copy");

        Assert.Equal("hello", ReadText(wad, "marker.txt"));
        Assert.True(File.Exists(CopyPath("TestZone-Copy")));
    }

    [Fact]
    public void CachedWad_StaleCopy_IsWrittenAgain() {
        LocalWadCache.CacheWad("TestZone-Stale", ArchiveOf("marker.txt", "hello"));
        Directory.CreateDirectory(Path.GetDirectoryName(CopyPath("TestZone-Stale"))!);
        File.WriteAllText(CopyPath("TestZone-Stale"), "stale");

        var wad = LocalWadCache.GetCachedWad("TestZone-Stale");

        Assert.Equal("hello", ReadText(wad, "marker.txt"));
    }

    [Fact]
    public void DeletedWad_LosesItsCopy() {
        LocalWadCache.CacheWad("TestZone-Deleted", ArchiveOf("marker.txt", "hello"));
        _ = LocalWadCache.GetCachedWad("TestZone-Deleted");

        LocalWadCache.DeleteWad("TestZone-Deleted");

        Assert.False(File.Exists(CopyPath("TestZone-Deleted")));
        Assert.Null(LocalWadCache.GetCachedWad("TestZone-Deleted"));
    }

    private static string CopyPath(string wadName)
        => Path.Combine(s_cacheDirectory, "wad-files", wadName + ".wad");

    private static string ReadText(Archive wad, string fileName)
        => Encoding.ASCII.GetString(wad.OpenFile(fileName)!.Value.Span);

    private static Archive ArchiveOf(string fileName, string content) {
        // A version 1 KIWAD holding one uncompressed file, laid out as ArchiveParser reads it.
        var data = Encoding.ASCII.GetBytes(content);
        var name = Encoding.UTF8.GetBytes(fileName + "\0");
        var headerLength = 5 + 4 + 4 + (4 + 4 + 4 + 1 + 4 + 4 + name.Length);

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
            writer.Write("KIWAD"u8);
            writer.Write(1u);
            writer.Write(1u);
            writer.Write((uint) headerLength);
            writer.Write((uint) data.Length);
            writer.Write((uint) data.Length);
            writer.Write(false);
            writer.Write(0u);
            writer.Write(name.Length);
            writer.Write(name);
            writer.Write(data);
        }
        stream.Position = 0;

        return ArchiveParser.Parse(stream);
    }

}
