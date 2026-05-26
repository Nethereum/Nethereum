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
        private readonly bool _convertPathsForWsl;
        private readonly byte[] _elfHash;

        public ZiskEmuBlockProver(string elfPath, string outputDir = null,
            string command = "ziskemu",
            string argsTemplate = "-e {elf} --legacy-inputs {witness} -n 100000000",
            bool convertPathsForWsl = false)
        {
            _elfPath = elfPath ?? throw new ArgumentNullException(nameof(elfPath));
            if (!File.Exists(_elfPath))
                throw new FileNotFoundException($"ELF not found: {_elfPath}");
            _outputDir = outputDir ?? Path.Combine(Path.GetTempPath(), "zisk-prover");
            _command = command;
            _argsTemplate = argsTemplate;
            _convertPathsForWsl = convertPathsForWsl;
            Directory.CreateDirectory(_outputDir);

            using (var sha = SHA256.Create())
                _elfHash = sha.ComputeHash(File.ReadAllBytes(_elfPath));
        }

        public Task<BlockProofResult> ProveBlockAsync(byte[] witnessBytes,
            byte[] preStateRoot, byte[] postStateRoot, long blockNumber)
        {
            var witnessFile = WriteWitnessInput(witnessBytes, blockNumber);

            try
            {
                var elfForCmd = _convertPathsForWsl ? ToWslPath(_elfPath) : _elfPath;
                var witnessForCmd = _convertPathsForWsl ? ToWslPath(witnessFile) : witnessFile;
                var emuResult = RunZiskEmu(_command, _argsTemplate, elfForCmd, witnessForCmd);

                byte[] witnessHash;
                using (var sha = SHA256.Create())
                    witnessHash = sha.ComputeHash(witnessBytes);

                byte[] proverComputedRoot = ParseHexBytes(emuResult.StateRootHex);
                byte[] proverComputedBlockHash = ParseHexBytes(emuResult.BlockHashHex);

                bool stateRootVerified = BytesMatch(proverComputedRoot, postStateRoot);

                if (proverComputedRoot != null && postStateRoot != null && !stateRootVerified)
                {
                    return Task.FromResult(new BlockProofResult
                    {
                        ProofBytes = null,
                        PreStateRoot = preStateRoot,
                        PostStateRoot = postStateRoot,
                        ProverComputedStateRoot = proverComputedRoot,
                        ProverComputedBlockHash = proverComputedBlockHash,
                        StateRootVerified = false,
                        BlockHashVerified = false,
                        BlockNumber = blockNumber,
                        WitnessHash = witnessHash,
                        ElfHash = _elfHash,
                        GasUsed = emuResult.GasUsed,
                        ProverMode = "Emulate"
                    });
                }

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
                    ProverComputedStateRoot = proverComputedRoot,
                    ProverComputedBlockHash = proverComputedBlockHash,
                    StateRootVerified = stateRootVerified,
                    BlockHashVerified = proverComputedBlockHash != null,
                    BlockNumber = blockNumber,
                    WitnessHash = witnessHash,
                    ElfHash = _elfHash,
                    GasUsed = emuResult.GasUsed,
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
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
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
                else if (trimmed.StartsWith("BIN:block_hash="))
                {
                    result.BlockHashHex = trimmed.Substring("BIN:block_hash=".Length);
                }
            }

            if (!result.Success && string.IsNullOrEmpty(result.Error))
                result.Error = !string.IsNullOrEmpty(stderr) ? stderr.Trim() : $"exit code {process.ExitCode}";

            return result;
        }

        private static byte[] ParseHexBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            if (hex.StartsWith("0x")) hex = hex.Substring(2);
            if (hex.Length == 0) return null;
            return Nethereum.Hex.HexConvertors.Extensions
                .HexByteConvertorExtensions.HexToByteArray(hex);
        }

        private static bool BytesMatch(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == null && b == null;
            return Nethereum.Util.ByteUtil.AreEqual(a, b);
        }

        private static string ToWslPath(string windowsPath)
        {
            if (string.IsNullOrEmpty(windowsPath)) return windowsPath;
            var p = windowsPath.Replace('\\', '/');
            if (p.Length >= 2 && p[1] == ':')
                p = "/mnt/" + char.ToLowerInvariant(p[0]) + p.Substring(2);
            return p;
        }
    }

    internal class ZiskEmuResult
    {
        public bool Success { get; set; }
        public long GasUsed { get; set; }
        public string StateRootHex { get; set; }
        public string BlockHashHex { get; set; }
        public string Error { get; set; }
        public string RawOutput { get; set; }
    }
}
