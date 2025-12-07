using System;
using System.IO;

namespace Nethereum.Wallet.Diagnostics;

public static class WalletDiagnosticsLogger
{
    private const string LogFileName = "TrezorPrompt.log";
    private static readonly object Sync = new();

    public static void Log(string category, string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:u} [{category}] [thread:{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(LogFileName, line);
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
