using System.IO;
using System.Runtime.CompilerServices;
using Imlight.Common;

namespace Imlight.Tests;

internal static class TestConfiguration {

    [ModuleInitializer]
    internal static void InitializeBeforeAnyLogger() {
        var iniFilePath = Path.GetTempFileName();
        try {
            File.WriteAllText(iniFilePath, "[Logging]\nLogPath = ./logs/log.txt\nLogLevel = ERROR\nLogFormat = {Message:lj}{NewLine}\nSeqSinkUrl = http://localhost:5341\n");
            ConfigurationManager.Initialize(iniFilePath);
        } finally {
            File.Delete(iniFilePath);
        }
    }

}
