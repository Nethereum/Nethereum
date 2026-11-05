using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Nethereum.EVM.Witness;
using Nethereum.Testing;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public sealed class ZiskGuestResult
    {
        public bool Success { get; set; }
        public bool BadVersion { get; set; }
        public long Gas { get; set; }
        public string StateRoot { get; set; }
        public string BlockHash { get; set; }
        public string AnchorCommitment { get; set; }
        public string Output { get; set; }
    }

    public static class ZiskGuestRunner
    {
        public static string FindElf()
        {
            var elfPath = Path.Combine(TestEnvironment.ExternalPath("ZiskOutput"), "nethereum_evm_elf");
            return File.Exists(elfPath) ? elfPath : null;
        }

        public static ZiskGuestResult Run(BlockWitnessData block, int timeoutMs = 300000)
        {
            var elfPath = FindElf();
            if (elfPath == null)
                throw new FileNotFoundException(
                    "Zisk guest ELF not found; ExternalPaths:ZiskOutput must contain nethereum_evm_elf " +
                    "(bash zisk/scripts/build.sh).");

            var witnessFile = WriteLegacyInput(BinaryBlockWitness.Serialize(block.AddRequestPredeploys()));
            try
            {
                var psi = ZiskHost.Command(
                    $"{TestEnvironment.Tool("Ziskemu")} -e {ZiskHost.Path(elfPath)} --legacy-inputs {ZiskHost.Path(witnessFile)} -n 500000000");
                using (var process = Process.Start(psi))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("ziskemu timed out");
                    }
                    return Parse(output);
                }
            }
            finally
            {
                try { File.Delete(witnessFile); } catch { }
            }
        }

        private static ZiskGuestResult Parse(string output)
        {
            var result = new ZiskGuestResult { Output = output };
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("BIN:OK gas="))
                {
                    result.Success = true;
                    long.TryParse(trimmed.Substring("BIN:OK gas=".Length), out var gas);
                    result.Gas = gas;
                }
                else if (trimmed.StartsWith("BIN:state_root="))
                    result.StateRoot = trimmed.Substring("BIN:state_root=".Length);
                else if (trimmed.StartsWith("BIN:anchor_commitment="))
                    result.AnchorCommitment = trimmed.Substring("BIN:anchor_commitment=".Length);
                else if (trimmed.StartsWith("BIN:block_hash="))
                    result.BlockHash = trimmed.Substring("BIN:block_hash=".Length);
                else if (trimmed.Contains("bad version"))
                    result.BadVersion = true;
            }
            return result;
        }

        private static string WriteLegacyInput(byte[] witnessBytes)
        {
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

            var path = Path.Combine(Path.GetTempPath(), $"zisk-guest-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(path, legacyInput);
            return path;
        }
    }
}
