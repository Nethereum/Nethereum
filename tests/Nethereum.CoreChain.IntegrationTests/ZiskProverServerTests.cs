using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class ZiskProverServerTests
    {
        private readonly ITestOutputHelper _output;

        public ZiskProverServerTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task ShouldProveViaHttpMockServer()
        {
            var port = 15100 + new Random().Next(900);
            var serverPath = FindServerProject();
            _output.WriteLine($"Server project: {serverPath}");
            _output.WriteLine($"Port: {port}");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --project \"{serverPath}\" --urls http://localhost:{port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["ProverMode"] = "Mock";
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
            var process = Process.Start(psi);

            try
            {
                await WaitForServerAsync($"http://localhost:{port}", 30);

                var client = new HttpBlockProverClient($"http://localhost:{port}");
                var witness = new byte[64];
                new Random(42).NextBytes(witness);
                var preRoot = new byte[32];
                preRoot[0] = 0xAA;
                var postRoot = new byte[32];
                postRoot[0] = 0xBB;

                var result = await client.ProveBlockAsync(witness, preRoot, postRoot, 42);

                Assert.NotNull(result);
                Assert.Equal("Mock", result.ProverMode);
                Assert.Equal(42, result.BlockNumber);
                Assert.NotNull(result.ProofBytes);
                Assert.Equal(32, result.ProofBytes.Length);
                Assert.Equal(preRoot, result.PreStateRoot);
                Assert.Equal(postRoot, result.PostStateRoot);
                Assert.NotNull(result.WitnessHash);

                _output.WriteLine($"Proof: {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(result.ProofBytes, true)}");
                _output.WriteLine($"Mode: {result.ProverMode}");
                _output.WriteLine("HTTP prover server mock test: SUCCESS");
            }
            finally
            {
                try { process?.Kill(true); } catch { }
                process?.Dispose();
            }
        }

        private static async Task WaitForServerAsync(string url, int timeoutSeconds)
        {
            using var http = new HttpClient();
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                try
                {
                    var resp = await http.GetAsync(url);
                    if (resp.IsSuccessStatusCode) return;
                }
                catch { }
                await Task.Delay(500);
            }
            throw new TimeoutException($"Server at {url} did not start within {timeoutSeconds}s");
        }

        [Fact]
        public async Task ShouldProveViaZiskEmuEmulateMode()
        {
            var witnessDir = FindWitnessDir();
            if (witnessDir == null)
            {
                _output.WriteLine("SKIP: no cached witnesses at /tmp/zisk-witnesses/ — run ZiskSyncTests first");
                return;
            }

            var witnessFile = Directory.GetFiles(witnessDir, "add11_0_0_0.bin").FirstOrDefault();
            if (witnessFile == null)
            {
                _output.WriteLine("SKIP: add11_0_0_0.bin not found in witness cache");
                return;
            }

            var elfPath = FindElfPath();
            if (elfPath == null)
            {
                _output.WriteLine("SKIP: ELF not found at zisk/output/nethereum_evm_elf");
                return;
            }

            var port = 15100 + new Random().Next(900);
            var serverPath = FindServerProject();
            _output.WriteLine($"Server: {serverPath}");
            _output.WriteLine($"Port: {port}, ELF: {elfPath}");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --project \"{serverPath}\" --urls http://localhost:{port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["ProverMode"] = "Emulate";
            psi.EnvironmentVariables["ElfPath"] = elfPath;
            psi.EnvironmentVariables["ProverCommand"] = "wsl";
            psi.EnvironmentVariables["ProverArgs"] = "-d Ubuntu -- /home/juan/.zisk/bin/ziskemu -e {elf} --legacy-inputs {witness} -n 100000000";
            psi.EnvironmentVariables["ConvertPathsForWsl"] = "true";
            psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
            var process = Process.Start(psi);

            try
            {
                await WaitForServerAsync($"http://localhost:{port}", 30);

                var rawFile = File.ReadAllBytes(witnessFile);
                var innerWitness = new byte[rawFile.Length - 16];
                Array.Copy(rawFile, 16, innerWitness, 0, innerWitness.Length);

                var client = new HttpBlockProverClient($"http://localhost:{port}");
                var result = await client.ProveBlockAsync(innerWitness, null, null, 1);

                Assert.NotNull(result);
                Assert.Equal("Emulate", result.ProverMode);
                Assert.Equal(1, result.BlockNumber);
                Assert.NotNull(result.ProofBytes);
                Assert.True(result.ProofBytes.Length > 0, "ProofBytes should be non-empty on successful emulation");
                Assert.NotNull(result.WitnessHash);

                _output.WriteLine($"Proof: {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(result.ProofBytes, true)}");
                _output.WriteLine($"WitnessHash: {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(result.WitnessHash, true)}");
                _output.WriteLine($"Mode: {result.ProverMode}");
                _output.WriteLine("Zisk emulate mode test: SUCCESS");
            }
            finally
            {
                try { process?.Kill(true); } catch { }
                process?.Dispose();
            }
        }

        private static string FindWitnessDir()
        {
            var tempPath = Path.GetTempPath();
            var dir = Path.Combine(tempPath, "zisk-witnesses");
            return Directory.Exists(dir) ? dir : null;
        }

        private static string FindElfPath()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var candidate = Path.Combine(dir, "zisk", "output", "nethereum_evm_elf");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static string FindServerProject()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var candidate = System.IO.Path.Combine(dir, "src", "Nethereum.Zisk.Prover.Server",
                    "Nethereum.Zisk.Prover.Server.csproj");
                if (System.IO.File.Exists(candidate)) return candidate;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            throw new System.IO.FileNotFoundException("Cannot find Nethereum.Zisk.Prover.Server.csproj");
        }
    }
}
