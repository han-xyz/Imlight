using System;
using System.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Xunit;

namespace Imlight.Tests;

[Collection("env")]
public sealed class RequirementQueryTests {

    // Captured from the client in Unicorn Way, right after a quest goal completed.
    private const string CompletedYardQuest =
        "87A43D020000000000180041562D594152442D4330372D3030315F436F6D706C657465650000000000";
    private const string NotCompletedUnicornWay =
        "87A43D0201000000001C0057432D434F4D4D4F4E532D4D41494E2D3030315F436F6D706C657465010B00556E69636F726E20576179000000";
    private const string OrCompletedGrimTales =
        "87A43D0200010000001C0057432D554E49434F524E2D4D41494E2D3030385F436F6D706C657465510A004772696D2054616C6573300000";

    public RequirementQueryTests() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Logging]\nLogPath = ./logs/log.txt\nLogLevel = ERROR\nLogFormat = {Message:lj}{NewLine}\nSeqSinkUrl = http://localhost:5341\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

    [Fact]
    public void TryDecode_ClientReqHasEntry_ReadsEntryName() {
        Assert.True(RequirementQuery.TryDecode(Convert.FromHexString(CompletedYardQuest), out var requirement));

        var entry = Assert.IsType<ReqHasEntry>(requirement);
        Assert.Equal("AV-YARD-C07-001_Complete", (string) entry.m_entryName);
        Assert.False(entry.m_applyNOT);
        Assert.False(entry.m_isQuestRegistry);
    }

    [Fact]
    public void TryDecode_ClientReqHasEntry_ReadsNotAndDisplayName() {
        Assert.True(RequirementQuery.TryDecode(Convert.FromHexString(NotCompletedUnicornWay), out var requirement));

        var entry = Assert.IsType<ReqHasEntry>(requirement);
        Assert.Equal("WC-COMMONS-MAIN-001_Complete", (string) entry.m_entryName);
        Assert.Equal("Unicorn Way", (string) entry.m_displayName);
        Assert.True(entry.m_applyNOT);
    }

    [Fact]
    public void TryDecode_ClientReqHasEntry_ReadsOperator() {
        Assert.True(RequirementQuery.TryDecode(Convert.FromHexString(OrCompletedGrimTales), out var requirement));

        var entry = Assert.IsType<ReqHasEntry>(requirement);
        Assert.Equal("WC-UNICORN-MAIN-008_Complete", (string) entry.m_entryName);
        Assert.Equal(Operator.ROP_OR, entry.m_operator);
    }

    [Fact]
    public void TryDecode_Garbage_ReturnsFalse()
        => Assert.False(RequirementQuery.TryDecode([0x01, 0x02, 0x03], out _));

}
