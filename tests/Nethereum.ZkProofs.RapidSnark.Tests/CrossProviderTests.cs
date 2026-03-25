using System.Diagnostics;
using System.Numerics;
using Nethereum.PrivacyPools.Circuits;
using Nethereum.ZkProofs.RapidSnark;
using Nethereum.ZkProofs.Snarkjs;
using Nethereum.ZkProofsVerifier.Circom;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.ZkProofs.RapidSnark.Tests
{
    public class CrossProviderTests
    {
        private readonly ITestOutputHelper _output;

        public CrossProviderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static SnarkjsProofProvider CreateSnarkjsProvider()
        {
            var globalSnarkjs = FindGlobalSnarkjsCli();
            return globalSnarkjs != null
                ? SnarkjsProofProvider.CreateNodeJs(snarkjsPath: globalSnarkjs)
                : new SnarkjsProofProvider();
        }

        private static string? FindGlobalSnarkjsCli()
        {
            // Search PATH directories for node_modules/snarkjs/cli.js
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var sep = OperatingSystem.IsWindows() ? ';' : ':';
            foreach (var dir in pathEnv.Split(sep))
            {
                var cli = Path.Combine(dir, "node_modules", "snarkjs", "cli.js");
                if (File.Exists(cli)) return cli;
            }

            // NVM on Windows: node binary and global modules live in same dir
            var nvmSymlink = Environment.GetEnvironmentVariable("NVM_SYMLINK");
            if (nvmSymlink != null)
            {
                var cli = Path.Combine(nvmSymlink, "node_modules", "snarkjs", "cli.js");
                if (File.Exists(cli)) return cli;
            }

            // Standard npm global prefix
            var npmRoot = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm")
                : "/usr/local/lib";
            var npmCli = Path.Combine(npmRoot, "node_modules", "snarkjs", "cli.js");
            if (File.Exists(npmCli)) return npmCli;

            // Resolve via `npm root -g` which always works
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "root -g",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                    var globalRoot = proc.StandardOutput.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(globalRoot))
                    {
                        var cli = Path.Combine(globalRoot, "snarkjs", "cli.js");
                        if (File.Exists(cli)) return cli;
                    }
                }
            }
            catch { }

            return null;
        }

        [Fact]
        [Trait("Category", "Native-Integration")]
        public void RapidSnark_DllLoads_And_ProofSizeReturns()
        {
            ulong proofSize = 0;
            RapidSnarkBindings.groth16_proof_size(ref proofSize);

            Assert.True(proofSize > 0, "groth16_proof_size should return a positive value");
            _output.WriteLine($"groth16_proof_size = {proofSize}");
        }

        [Fact]
        [Trait("Category", "Native-Integration")]
        public async Task CrossProvider_SnarkjsWitness_RapidSnarkProof_BothVerify()
        {
            var circuitSource = new PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("commitment"))
            {
                _output.WriteLine("SKIP: Circuit artifacts not found (commitment circuit)");
                return;
            }

            var wasm = await circuitSource.GetWasmAsync("commitment");
            var zkey = await circuitSource.GetZkeyAsync("commitment");
            var vkJson = circuitSource.GetVerificationKeyJson("commitment");

            var inputJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                nullifier = "12345678901234567890",
                secret = "98765432109876543210",
                value = "1000000000000000000",
                label = "1"
            });

            _output.WriteLine("=== Step 1: Generate proof with snarkjs (Node.js) ===");
            var snarkjsProvider = CreateSnarkjsProvider();

            var sw = Stopwatch.StartNew();
            var snarkjsResult = await snarkjsProvider.FullProveAsync(new ZkProofRequest
            {
                CircuitWasm = wasm,
                CircuitZkey = zkey,
                InputJson = inputJson,
                Scheme = ZkProofScheme.Groth16
            });
            sw.Stop();
            _output.WriteLine($"Snarkjs proof generated in {sw.ElapsedMilliseconds}ms");
            Assert.False(string.IsNullOrEmpty(snarkjsResult.ProofJson));
            Assert.True(snarkjsResult.PublicSignals.Length > 0);

            _output.WriteLine("");
            _output.WriteLine("=== Step 2: Generate witness via snarkjs backend, prove with rapidsnark ===");

            var witnessBytes = await GenerateWitnessViaSnarkjsBackendAsync(wasm, zkey, inputJson);
            _output.WriteLine($"Witness generated: {witnessBytes.Length} bytes");

            var rapidsnarkProvider = new RapidSnarkProofProvider();

            sw.Restart();
            var rapidsnarkResult = await rapidsnarkProvider.FullProveAsync(new ZkProofRequest
            {
                CircuitZkey = zkey,
                WitnessBytes = witnessBytes,
                Scheme = ZkProofScheme.Groth16
            });
            sw.Stop();
            _output.WriteLine($"RapidSnark proof generated in {sw.ElapsedMilliseconds}ms");
            Assert.False(string.IsNullOrEmpty(rapidsnarkResult.ProofJson));
            Assert.True(rapidsnarkResult.PublicSignals.Length > 0);

            _output.WriteLine("");
            _output.WriteLine("=== Step 3: Verify public signals match ===");
            Assert.Equal(snarkjsResult.PublicSignals.Length, rapidsnarkResult.PublicSignals.Length);
            for (int i = 0; i < snarkjsResult.PublicSignals.Length; i++)
            {
                Assert.Equal(snarkjsResult.PublicSignals[i], rapidsnarkResult.PublicSignals[i]);
            }
            _output.WriteLine("Public signals match!");

            _output.WriteLine("");
            _output.WriteLine("=== Step 4: Verify both proofs with Groth16 verifier ===");

            var snarkjsVerifyResult = CircomGroth16Adapter.Verify(
                snarkjsResult.ProofJson, vkJson, snarkjsResult.PublicSignalsJson);
            _output.WriteLine($"Snarkjs proof valid: {snarkjsVerifyResult.IsValid}");
            Assert.True(snarkjsVerifyResult.IsValid, "Snarkjs proof should verify");

            var rapidsnarkVerifyResult = CircomGroth16Adapter.Verify(
                rapidsnarkResult.ProofJson, vkJson, rapidsnarkResult.PublicSignalsJson);
            _output.WriteLine($"RapidSnark proof valid: {rapidsnarkVerifyResult.IsValid}");
            Assert.True(rapidsnarkVerifyResult.IsValid, "RapidSnark proof should verify");

            _output.WriteLine("");
            _output.WriteLine("=== PASS: Both providers produce valid, equivalent proofs ===");
        }

        [Fact]
        [Trait("Category", "Native-Integration")]
        public async Task RapidSnark_ReusableProver_MultipleProofs()
        {
            var circuitSource = new PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("commitment"))
            {
                _output.WriteLine("SKIP: Circuit artifacts not found");
                return;
            }

            var wasm = await circuitSource.GetWasmAsync("commitment");
            var zkey = await circuitSource.GetZkeyAsync("commitment");

            using var prover = new RapidSnarkProver();
            prover.LoadZkey(zkey);
            _output.WriteLine("Zkey loaded into reusable prover");

            for (int i = 0; i < 3; i++)
            {
                var inputJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    nullifier = (1000 + i).ToString(),
                    secret = (2000 + i).ToString(),
                    value = "1000000000000000000",
                    label = "1"
                });

                var witness = await GenerateWitnessViaSnarkjsBackendAsync(wasm, zkey, inputJson);

                var sw = Stopwatch.StartNew();
                var (proofJson, publicJson) = prover.ProveWithLoadedZkey(witness);
                sw.Stop();

                Assert.False(string.IsNullOrEmpty(proofJson));
                Assert.False(string.IsNullOrEmpty(publicJson));
                _output.WriteLine($"Proof {i}: {sw.ElapsedMilliseconds}ms");
            }

            _output.WriteLine("All 3 proofs generated with reusable prover");
        }

        [Fact]
        [Trait("Category", "Native-Integration")]
        public async Task FullyNative_NativeProofProvider_NoJavaScript()
        {
            var circuitSource = new PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("commitment"))
            {
                _output.WriteLine("SKIP: Circuit artifacts not found");
                return;
            }
            if (!circuitSource.HasGraph("commitment"))
            {
                _output.WriteLine("SKIP: Circuit graph not found (commitment.graph.bin)");
                return;
            }

            var zkey = await circuitSource.GetZkeyAsync("commitment");
            var graphData = circuitSource.GetGraphData("commitment");
            var vkJson = circuitSource.GetVerificationKeyJson("commitment");

            var inputJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                nullifier = "12345678901234567890",
                secret = "98765432109876543210",
                value = "1000000000000000000",
                label = "1"
            });

            _output.WriteLine("=== Fully native proof (no JavaScript) ===");
            var nativeProvider = new NativeProofProvider();

            var sw = Stopwatch.StartNew();
            var result = await nativeProvider.FullProveAsync(new ZkProofRequest
            {
                CircuitZkey = zkey,
                CircuitGraph = graphData,
                InputJson = inputJson,
                Scheme = ZkProofScheme.Groth16
            });
            sw.Stop();

            _output.WriteLine($"Native proof generated in {sw.ElapsedMilliseconds}ms (witness + prove)");
            Assert.False(string.IsNullOrEmpty(result.ProofJson));
            Assert.True(result.PublicSignals.Length > 0);
            _output.WriteLine($"Public signals: [{string.Join(", ", result.PublicSignals)}]");

            var verifyResult = CircomGroth16Adapter.Verify(
                result.ProofJson, vkJson, result.PublicSignalsJson);
            _output.WriteLine($"Proof valid: {verifyResult.IsValid}");
            Assert.True(verifyResult.IsValid, "Native proof should verify");

            _output.WriteLine("=== PASS: Fully native proof verified ===");
        }

        [Fact]
        [Trait("Category", "Native-Integration")]
        public async Task CrossProvider_NativeVsSnarkjs_SamePublicSignals()
        {
            var circuitSource = new PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("commitment") || !circuitSource.HasGraph("commitment"))
            {
                _output.WriteLine("SKIP: Circuit artifacts or graph not found");
                return;
            }

            var wasm = await circuitSource.GetWasmAsync("commitment");
            var zkey = await circuitSource.GetZkeyAsync("commitment");
            var graphData = circuitSource.GetGraphData("commitment");

            var inputJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                nullifier = "55555555555",
                secret = "77777777777",
                value = "2000000000000000000",
                label = "1"
            });

            _output.WriteLine("=== Snarkjs (Node.js) ===");
            var snarkjsProvider = CreateSnarkjsProvider();
            var sw = Stopwatch.StartNew();
            var snarkjsResult = await snarkjsProvider.FullProveAsync(new ZkProofRequest
            {
                CircuitWasm = wasm, CircuitZkey = zkey, InputJson = inputJson
            });
            sw.Stop();
            _output.WriteLine($"Snarkjs: {sw.ElapsedMilliseconds}ms");

            _output.WriteLine("=== Native (circom-witnesscalc + rapidsnark) ===");
            var nativeProvider = new NativeProofProvider();
            sw.Restart();
            var nativeResult = await nativeProvider.FullProveAsync(new ZkProofRequest
            {
                CircuitZkey = zkey, CircuitGraph = graphData, InputJson = inputJson
            });
            sw.Stop();
            _output.WriteLine($"Native: {sw.ElapsedMilliseconds}ms");

            _output.WriteLine("=== Compare public signals ===");
            Assert.Equal(snarkjsResult.PublicSignals.Length, nativeResult.PublicSignals.Length);
            for (int i = 0; i < snarkjsResult.PublicSignals.Length; i++)
            {
                Assert.Equal(snarkjsResult.PublicSignals[i], nativeResult.PublicSignals[i]);
                _output.WriteLine($"  Signal[{i}]: {snarkjsResult.PublicSignals[i]} == {nativeResult.PublicSignals[i]}");
            }
            _output.WriteLine("=== PASS: Snarkjs and native produce identical public signals ===");
        }

        private static async Task<byte[]> GenerateWitnessViaSnarkjsBackendAsync(
            byte[] wasmBytes, byte[] zkeyBytes, string inputJson)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "rapidsnark_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            try
            {
                var wasmPath = Path.Combine(tempDir, "circuit.wasm");
                var zkeyPath = Path.Combine(tempDir, "circuit.zkey");
                var inputPath = Path.Combine(tempDir, "input.json");
                var wtnsPath = Path.Combine(tempDir, "witness.wtns");

                await File.WriteAllBytesAsync(wasmPath, wasmBytes);
                await File.WriteAllBytesAsync(zkeyPath, zkeyBytes);
                await File.WriteAllTextAsync(inputPath, inputJson);

                // Reuse the existing NodeJsSnarkjsBackend — it runs wtns calculate + groth16 prove.
                // We only need the witness file, but running the full prove is harmless.
                var snarkjsCli = FindGlobalSnarkjsCli();
                var backend = snarkjsCli != null
                    ? new NodeJsSnarkjsBackend(snarkjsPath: snarkjsCli)
                    : new NodeJsSnarkjsBackend();
                await backend.FullProveAsync(wasmPath, zkeyPath, inputPath);

                // The backend writes witness.wtns to the same temp dir
                if (!File.Exists(wtnsPath))
                    throw new FileNotFoundException("Witness file not generated by snarkjs backend", wtnsPath);

                return await File.ReadAllBytesAsync(wtnsPath);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
