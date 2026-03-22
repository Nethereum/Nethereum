using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.ZkProofs.Snarkjs
{
    public class NodeJsSnarkjsBackend : ISnarkjsBackend
    {
        private readonly string _nodePath;
        private readonly string _snarkjsPath;

        public NodeJsSnarkjsBackend(string nodePath = null, string snarkjsPath = null)
        {
            _nodePath = nodePath ?? FindExecutable("node");
            _snarkjsPath = snarkjsPath;
        }

        public async Task<(string proofJson, string publicJson)> FullProveAsync(
            string wasmPath, string zkeyPath, string inputJsonPath, CancellationToken cancellationToken = default)
        {
            ValidateFileExists(wasmPath, "WASM circuit");
            ValidateFileExists(zkeyPath, "zkey");
            ValidateFileExists(inputJsonPath, "input JSON");

            var tempDir = Path.GetDirectoryName(inputJsonPath)!;
            var witnessPath = Path.Combine(tempDir, "witness.wtns");
            var proofPath = Path.Combine(tempDir, "proof.json");
            var publicPath = Path.Combine(tempDir, "public.json");

            await RunSnarkjsAsync($"wtns calculate \"{wasmPath}\" \"{inputJsonPath}\" \"{witnessPath}\"", cancellationToken);
            await RunSnarkjsAsync($"groth16 prove \"{zkeyPath}\" \"{witnessPath}\" \"{proofPath}\" \"{publicPath}\"", cancellationToken);

            var proofJson = await File.ReadAllTextAsync(proofPath, cancellationToken);
            var publicJson = await File.ReadAllTextAsync(publicPath, cancellationToken);

            return (proofJson, publicJson);
        }

        private async Task RunSnarkjsAsync(string snarkjsCommand, CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo;

            if (_snarkjsPath != null)
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = _nodePath,
                    Arguments = $"\"{_snarkjsPath}\" {snarkjsCommand}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                var npxPath = FindExecutable("npx");
                startInfo = new ProcessStartInfo
                {
                    FileName = npxPath,
                    Arguments = $"snarkjs {snarkjsCommand}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"snarkjs failed with exit code {process.ExitCode}. stderr: {stderr}. stdout: {stdout}");
            }
        }

        private static string FindExecutable(string name)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var separator = OperatingSystem.IsWindows() ? ';' : ':';
            var extensions = OperatingSystem.IsWindows()
                ? new[] { ".exe", ".cmd", ".bat" }
                : new[] { "" };

            foreach (var dir in pathEnv.Split(separator))
            {
                foreach (var ext in extensions)
                {
                    var fullPath = Path.Combine(dir, name + ext);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }

            return name;
        }

        private static void ValidateFileExists(string path, string description)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"{description} file not found: {path}", path);
        }
    }
}
