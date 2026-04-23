using System;
using System.Diagnostics;
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
