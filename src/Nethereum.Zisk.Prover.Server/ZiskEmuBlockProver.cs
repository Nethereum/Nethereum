using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;

namespace Nethereum.Zisk.Prover.Server
{
    public class ZiskEmuBlockProver : IBlockProver
    {
        private readonly string _elfPath;
        private readonly string _outputDir;
        private readonly string _command;
        private readonly string _argsTemplate;

        public ZiskEmuBlockProver(string elfPath, string outputDir = null,
            string command = "ziskemu",
            string argsTemplate = "-e {elf} --legacy-inputs {witness} -n 100000000")
        {
            _elfPath = elfPath ?? throw new ArgumentNullException(nameof(elfPath));
            if (!File.Exists(_elfPath))
                throw new FileNotFoundException($"ELF not found: {_elfPath}");
            _outputDir = outputDir ?? Path.Combine(Path.GetTempPath(), "zisk-prover");
            _command = command;
            _argsTemplate = argsTemplate;
            Directory.CreateDirectory(_outputDir);
        }

        public Task<BlockProofResult> ProveBlockAsync(byte[] witnessBytes,
            byte[] preStateRoot, byte[] postStateRoot, long blockNumber)
        {
            var witnessFile = WriteWitnessInput(witnessBytes, blockNumber);

            try
            {
                var emuResult = RunZiskEmu(_command, _argsTemplate, _elfPath, witnessFile);

                byte[] witnessHash;
                using (var sha = SHA256.Create())
                    witnessHash = sha.ComputeHash(witnessBytes);

                byte[] proofBytes = null;
                if (emuResult.Success)
                {
                    using (var sha = SHA256.Create())
                    {
                        var combined = new byte[(witnessHash?.Length ?? 0) + (preStateRoot?.Length ?? 0) + (postStateRoot?.Length ?? 0)];
                        int offset = 0;
                        if (preStateRoot != null) { Array.Copy(preStateRoot, 0, combined, offset, preStateRoot.Length); offset += preStateRoot.Length; }
                        if (postStateRoot != null) { Array.Copy(postStateRoot, 0, combined, offset, postStateRoot.Length); offset += postStateRoot.Length; }
                        if (witnessHash != null) Array.Copy(witnessHash, 0, combined, offset, witnessHash.Length);
                        proofBytes = sha.ComputeHash(combined);
                    }
                }

                return Task.FromResult(new BlockProofResult
                {
                    ProofBytes = proofBytes,
                    PreStateRoot = preStateRoot,
                    PostStateRoot = postStateRoot,
                    BlockNumber = blockNumber,
                    WitnessHash = witnessHash,
                    ProverMode = "Emulate"
                });
            }
            finally
            {
                try { File.Delete(witnessFile); } catch { }
            }
        }

        private string WriteWitnessInput(byte[] witnessBytes, long blockNumber)
        {
            var path = Path.Combine(_outputDir, $"witness_block_{blockNumber}.bin");

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

        private static ZiskEmuResult RunZiskEmu(string command, string argsTemplate,
            string elfPath, string witnessFile, int timeoutMs = 120000)
        {
            var args = argsTemplate
                .Replace("{elf}", elfPath)
                .Replace("{witness}", witnessFile);

            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                return new ZiskEmuResult { Error = "ziskemu timeout", RawOutput = output };
            }

            var result = new ZiskEmuResult { RawOutput = output };
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
            }

            if (!result.Success && string.IsNullOrEmpty(result.Error))
                result.Error = $"exit code {process.ExitCode}";

            return result;
        }
    }

    internal class ZiskEmuResult
    {
        public bool Success { get; set; }
        public long GasUsed { get; set; }
        public string StateRootHex { get; set; }
        public string Error { get; set; }
        public string RawOutput { get; set; }
    }
}
