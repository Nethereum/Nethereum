using System;
using System.Diagnostics;
using System.IO;
using Nethereum.Testing;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class ZiskEmuResult
    {
        public bool Success { get; set; }
            public int TransactionsFailed { get; set; }
        public long GasUsed { get; set; }
        public string StateRootHex { get; set; }
        public string BlockHashHex { get; set; }
        public string Error { get; set; }
        public string RawOutput { get; set; }
    }

    public static class ZiskEmulatorRunner
    {
        public static string WriteLegacyInput(string outputDir, string baseName, byte[] witnessBytes)
        {
            Directory.CreateDirectory(outputDir);
            var path = Path.Combine(outputDir, baseName + ".bin");

            int padLen = (8 - witnessBytes.Length % 8) % 8;
            if (padLen > 0)
            {
                var padded = new byte[witnessBytes.Length + padLen];
                Array.Copy(witnessBytes, padded, witnessBytes.Length);
                witnessBytes = padded;
            }

            var legacyInput = new byte[16 + witnessBytes.Length];
            BitConverter.GetBytes((long)0).CopyTo(legacyInput, 0);
            BitConverter.GetBytes((long)witnessBytes.Length).CopyTo(legacyInput, 8);
            Array.Copy(witnessBytes, 0, legacyInput, 16, witnessBytes.Length);
            File.WriteAllBytes(path, legacyInput);
            return path;
        }

        public static ZiskEmuResult RunZiskEmu(string elfPath, string witnessFile, int timeoutMs = 120000, int maxSteps = 100000000)
        {
            var psi = ZiskHost.Command(
                $"{TestEnvironment.Tool("Ziskemu")} -e {ZiskHost.Path(elfPath)} --legacy-inputs {ZiskHost.Path(witnessFile)} -n {maxSteps}");

            var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                return new ZiskEmuResult { Error = "ziskemu timeout", RawOutput = output };
            }

            var result = new ZiskEmuResult { RawOutput = output };
            var transactionsFailed = 0;
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("BIN:OK gas="))
                {
                    result.Success = true;
                    long.TryParse(trimmed.Substring("BIN:OK gas=".Length), out var gas);
                    result.GasUsed = gas;
                }
                else if (trimmed.StartsWith("BIN:FAIL"))
                {
                    result.Success = false;
                    result.Error = trimmed.Length > 9 ? trimmed.Substring(9) : "unknown";
                }
                else if (trimmed.StartsWith("BIN:state_root="))
                {
                    result.StateRootHex = trimmed.Substring("BIN:state_root=".Length);
                }
                else if (trimmed.StartsWith("BIN:block_hash="))
                {
                    result.BlockHashHex = trimmed.Substring("BIN:block_hash=".Length);
                }
                else if (trimmed.StartsWith("BIN:executed ok="))
                {
                    var failedAt = trimmed.IndexOf(" fail=", StringComparison.Ordinal);
                    if (failedAt >= 0 && int.TryParse(trimmed.Substring(failedAt + " fail=".Length), out var failedTransactions))
                        transactionsFailed = failedTransactions;
                }
                else if (trimmed.Contains("bad version"))
                {
                    result.Success = false;
                    result.Error = "bad witness version";
                }
            }

            result.TransactionsFailed = transactionsFailed;

            if (!result.Success && string.IsNullOrEmpty(result.Error))
                result.Error = $"exit code {process.ExitCode}";

            return result;
        }

        public static ProveResult RunCargoZiskProve(string elfPath, string witnessFile, int timeoutMs = 900000)
        {
            var psi = ZiskHost.Command(
                $"cargo-zisk prove -e {ZiskHost.Path(elfPath)} -i {ZiskHost.Path(witnessFile)} -m 2>&1");

            var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                return new ProveResult { Success = false, Error = "cargo-zisk prove timeout", RawOutput = output };
            }

            return new ProveResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                RawOutput = output
            };
        }

        public static string FindDefaultElfPath()
        {
            var elfPath = Path.Combine(TestEnvironment.ExternalPath("ZiskOutput"), "nethereum_evm_elf");
            return File.Exists(elfPath) ? elfPath : null;
        }
    }

    public class ProveResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Error { get; set; }
        public string RawOutput { get; set; }
    }
}
