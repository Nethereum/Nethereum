using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Proving;

namespace Nethereum.Zisk.Prover.Server
{
    public class ZiskProveBlockProver : IBlockProver
    {
        private readonly string _elfPath;
        private readonly string _outputDir;
        private readonly string _cargoZiskPath;
        private readonly string _provingKeySnarkPath;
        private readonly bool _convertPathsForWsl;
        private readonly bool _useWsl;
        private readonly byte[] _elfHash;
        private readonly int _timeoutMs;
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _environmentVariables;

        public ZiskProveBlockProver(
            string elfPath,
            string outputDir = null,
            string cargoZiskPath = "cargo-zisk",
            string provingKeySnarkPath = null,
            bool convertPathsForWsl = false,
            bool useWsl = false,
            int timeoutMs = 1800000,
            ILogger logger = null,
            Dictionary<string, string> environmentVariables = null)
        {
            _elfPath = elfPath ?? throw new ArgumentNullException(nameof(elfPath));
            if (!File.Exists(_elfPath))
                throw new FileNotFoundException($"ELF not found: {_elfPath}");
            _outputDir = outputDir ?? Path.Combine(Path.GetTempPath(), "zisk-prover");
            _cargoZiskPath = cargoZiskPath;
            _provingKeySnarkPath = provingKeySnarkPath;
            _convertPathsForWsl = convertPathsForWsl;
            _useWsl = useWsl;
            _timeoutMs = timeoutMs;
            _logger = logger;
            _environmentVariables = environmentVariables ?? new Dictionary<string, string>();
            Directory.CreateDirectory(_outputDir);

            using var sha = SHA256.Create();
            _elfHash = sha.ComputeHash(File.ReadAllBytes(_elfPath));
            _logger?.LogInformation("ZiskProveBlockProver initialized: elf={ElfPath}, cargoZisk={CargoZisk}, elfHash={ElfHash}",
                _elfPath, _cargoZiskPath, Convert.ToHexString(_elfHash).Substring(0, 16));
        }

        public async Task<BlockProofResult> ProveBlockAsync(
            byte[] witnessBytes, byte[] preStateRoot, byte[] postStateRoot, long blockNumber)
        {
            var witnessFile = WriteStandardInput(witnessBytes, blockNumber);
            var proofOutputFile = Path.Combine(_outputDir, $"proof_block_{blockNumber}");

            byte[] witnessHash;
            using (var sha = SHA256.Create())
                witnessHash = sha.ComputeHash(witnessBytes);

            _logger?.LogInformation("Block {BlockNumber}: starting STARK proof. Witness={WitnessSize} bytes, hash={WitnessHash}",
                blockNumber, witnessBytes.Length, Convert.ToHexString(witnessHash).Substring(0, 16));

            try
            {
                var elfCmd = ResolvePath(_elfPath);
                var witnessCmd = ResolvePath(witnessFile);
                var proofCmd = ResolvePath(proofOutputFile);

                if (File.Exists(proofOutputFile))
                    File.Delete(proofOutputFile);

                var sw = Stopwatch.StartNew();

                var starkResult = RunCommand(
                    $"prove -e {elfCmd} -i {witnessCmd} -l -n -o {proofCmd}",
                    _timeoutMs);

                sw.Stop();

                if (!starkResult.Success)
                {
                    _logger?.LogError("Block {BlockNumber}: STARK prove FAILED in {Duration:F1}s. Exit={ExitCode}\nStdout: {Stdout}\nStderr: {Stderr}",
                        blockNumber, sw.Elapsed.TotalSeconds, starkResult.ExitCode, starkResult.Stdout, starkResult.Stderr);
                    return FailResult(preStateRoot, postStateRoot, blockNumber, witnessHash,
                        $"STARK prove failed (exit {starkResult.ExitCode}): {starkResult.Stderr}");
                }

                byte[] proofBytes = null;
                if (File.Exists(proofOutputFile))
                {
                    proofBytes = File.ReadAllBytes(proofOutputFile);
                    _logger?.LogInformation("Block {BlockNumber}: STARK proof generated in {Duration:F1}s. Proof={ProofSize} bytes",
                        blockNumber, sw.Elapsed.TotalSeconds, proofBytes.Length);
                }
                else
                {
                    _logger?.LogWarning("Block {BlockNumber}: cargo-zisk prove succeeded but no output file at {Path}",
                        blockNumber, proofOutputFile);
                }

                byte[] computedStateRoot = ParseOutputField(starkResult.Stdout, "state_root");
                byte[] computedBlockHash = ParseOutputField(starkResult.Stdout, "block_hash");
                long gasUsed = ParseGasUsed(starkResult.Stdout);

                if (computedStateRoot != null)
                    _logger?.LogInformation("Block {BlockNumber}: stateRoot={StateRoot}, blockHash={BlockHash}, gas={Gas}",
                        blockNumber,
                        "0x" + Convert.ToHexString(computedStateRoot).Substring(0, 16),
                        computedBlockHash != null ? "0x" + Convert.ToHexString(computedBlockHash).Substring(0, 16) : "null",
                        gasUsed);

                bool stateRootVerified = BytesMatch(computedStateRoot, postStateRoot);
                bool blockHashVerified = computedBlockHash != null;

                if (!stateRootVerified && postStateRoot != null && computedStateRoot != null)
                    _logger?.LogWarning("Block {BlockNumber}: state root MISMATCH. Prover={ProverRoot}, Expected={ExpectedRoot}",
                        blockNumber,
                        "0x" + Convert.ToHexString(computedStateRoot),
                        "0x" + Convert.ToHexString(postStateRoot));

                // SNARK compression (optional, when provingKeySnark is configured)
                if (proofBytes != null && !string.IsNullOrEmpty(_provingKeySnarkPath))
                {
                    _logger?.LogInformation("Block {BlockNumber}: attempting SNARK wrap...", blockNumber);
                    var snarkSw = Stopwatch.StartNew();
                    var snarkOutputFile = Path.Combine(_outputDir, $"snark_block_{blockNumber}");
                    var snarkResult = RunCommand(
                        $"wrap-proof -p {proofCmd} --plonk -o {ResolvePath(snarkOutputFile)}",
                        _timeoutMs);
                    snarkSw.Stop();

                    if (snarkResult.Success && File.Exists(snarkOutputFile))
                    {
                        var snarkBytes = File.ReadAllBytes(snarkOutputFile);
                        _logger?.LogInformation("Block {BlockNumber}: SNARK wrap succeeded in {Duration:F1}s. SNARK={SnarkSize} bytes",
                            blockNumber, snarkSw.Elapsed.TotalSeconds, snarkBytes.Length);
                        proofBytes = snarkBytes;
                    }
                    else
                    {
                        _logger?.LogWarning("Block {BlockNumber}: SNARK wrap failed, using STARK proof. Error: {Error}",
                            blockNumber, snarkResult.Stderr);
                    }
                }

                return new BlockProofResult
                {
                    ProofBytes = proofBytes,
                    PreStateRoot = preStateRoot,
                    PostStateRoot = postStateRoot,
                    ProverComputedStateRoot = computedStateRoot,
                    ProverComputedBlockHash = computedBlockHash,
                    StateRootVerified = stateRootVerified,
                    BlockHashVerified = blockHashVerified,
                    BlockNumber = blockNumber,
                    WitnessHash = witnessHash,
                    ElfHash = _elfHash,
                    GasUsed = gasUsed,
                    ProverMode = proofBytes != null ? "Zisk" : "ZiskNoProof"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Block {BlockNumber}: unhandled exception during proving", blockNumber);
                return FailResult(preStateRoot, postStateRoot, blockNumber, witnessHash, ex.Message);
            }
            finally
            {
                try { File.Delete(witnessFile); } catch { }
            }
        }

        private string WriteStandardInput(byte[] witnessBytes, long blockNumber)
        {
            var path = Path.Combine(_outputDir, $"witness_block_{blockNumber}.bin");

            int padLen = (8 - witnessBytes.Length % 8) % 8;
            if (padLen > 0)
            {
                var padded = new byte[witnessBytes.Length + padLen];
                Array.Copy(witnessBytes, padded, witnessBytes.Length);
                witnessBytes = padded;
            }

            int dataLen = witnessBytes.Length;
            int legacySize = 16 + dataLen;
            var standardInput = new byte[24 + dataLen];
            BitConverter.GetBytes((long)legacySize).CopyTo(standardInput, 0);
            BitConverter.GetBytes((long)dataLen).CopyTo(standardInput, 16);
            Array.Copy(witnessBytes, 0, standardInput, 24, dataLen);

            File.WriteAllBytes(path, standardInput);
            _logger?.LogDebug("Block {BlockNumber}: wrote standard input {Size} bytes to {Path}",
                blockNumber, standardInput.Length, path);
            return path;
        }

        private CommandResult RunCommand(string args, int timeoutMs)
        {
            _logger?.LogDebug("Running: {Command} {Args}", _cargoZiskPath, args);

            ProcessStartInfo psi;
            if (_useWsl)
            {
                psi = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = $"-d Ubuntu -e sh -c \"{_cargoZiskPath} {args} 2>&1\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = _cargoZiskPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

            foreach (var env in _environmentVariables)
                psi.Environment[env.Key] = env.Value;

            if (!psi.Environment.ContainsKey("HWLOC_COMPONENTS"))
                psi.Environment["HWLOC_COMPONENTS"] = "-gl";

            var process = Process.Start(psi);
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                _logger?.LogError("Command timed out after {Timeout}ms: {Command} {Args}",
                    timeoutMs, _cargoZiskPath, args);
                return new CommandResult { ExitCode = -1, Error = "timeout", Stdout = stdout, Stderr = stderr };
            }

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Stdout = stdout,
                Stderr = stderr,
                Error = process.ExitCode != 0 ? stderr : null
            };
        }

        private string ResolvePath(string path)
        {
            if (!_convertPathsForWsl) return path;
            var p = path.Replace('\\', '/');
            if (p.Length >= 2 && p[1] == ':')
                p = "/mnt/" + char.ToLowerInvariant(p[0]) + p.Substring(2);
            return p;
        }

        private static byte[] ParseOutputField(string output, string fieldName)
        {
            if (string.IsNullOrEmpty(output)) return null;
            var prefix = $"BIN:{fieldName}=";
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(prefix))
                {
                    var hex = trimmed.Substring(prefix.Length);
                    if (hex.StartsWith("0x")) hex = hex.Substring(2);
                    if (hex.Length > 0)
                        return Nethereum.Hex.HexConvertors.Extensions
                            .HexByteConvertorExtensions.HexToByteArray(hex);
                }
            }
            return null;
        }

        private static long ParseGasUsed(string output)
        {
            if (string.IsNullOrEmpty(output)) return 0;
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("BIN:OK gas="))
                {
                    long.TryParse(trimmed.Substring("BIN:OK gas=".Length), out var gas);
                    return gas;
                }
            }
            return 0;
        }

        private BlockProofResult FailResult(byte[] preStateRoot, byte[] postStateRoot,
            long blockNumber, byte[] witnessHash, string error)
        {
            return new BlockProofResult
            {
                ProofBytes = null,
                PreStateRoot = preStateRoot,
                PostStateRoot = postStateRoot,
                StateRootVerified = false,
                BlockHashVerified = false,
                BlockNumber = blockNumber,
                WitnessHash = witnessHash,
                ElfHash = _elfHash,
                ProverMode = "ZiskFailed"
            };
        }

        private static bool BytesMatch(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == null && b == null;
            return Nethereum.Util.ByteUtil.AreEqual(a, b);
        }

        private class CommandResult
        {
            public bool Success { get; set; }
            public int ExitCode { get; set; }
            public string Stdout { get; set; }
            public string Stderr { get; set; }
            public string Error { get; set; }
        }
    }
}
