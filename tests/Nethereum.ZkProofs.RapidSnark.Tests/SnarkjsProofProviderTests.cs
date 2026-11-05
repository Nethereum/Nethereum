using System.Text;
using Nethereum.Documentation;
using Nethereum.ZkProofs.Snarkjs;
using Xunit;

namespace Nethereum.ZkProofs.RapidSnark.Tests
{
    public class SnarkjsProofProviderTests
    {
        private sealed class RecordingBackend : ISnarkjsBackend
        {
            public string? WasmPath { get; private set; }
            public string? ZkeyPath { get; private set; }
            public string? InputJsonPath { get; private set; }
            public byte[]? Wasm { get; private set; }
            public byte[]? Zkey { get; private set; }
            public string? Input { get; private set; }

            public async Task<(string proofJson, string publicJson)> FullProveAsync(
                string wasmPath, string zkeyPath, string inputJsonPath, CancellationToken cancellationToken = default)
            {
                WasmPath = wasmPath;
                ZkeyPath = zkeyPath;
                InputJsonPath = inputJsonPath;
                Wasm = await File.ReadAllBytesAsync(wasmPath, cancellationToken);
                Zkey = await File.ReadAllBytesAsync(zkeyPath, cancellationToken);
                Input = await File.ReadAllTextAsync(inputJsonPath, cancellationToken);
                return ("{\"pi_a\":[\"1\",\"2\",\"1\"]}", "[\"7\",\"11\"]");
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.Protocols, "snarkjs-proof-provider", "Prove through an ISnarkjsBackend and read the Groth16 result", Order = 1)]
        public async Task Given_ABackend_When_FullProveAsyncRuns_Then_TheCircuitFilesAreWrittenAndTheResultIsParsed()
        {
            var backend = new RecordingBackend();
            IZkProofProvider provider = new SnarkjsProofProvider(backend);

            var result = await provider.FullProveAsync(new ZkProofRequest
            {
                CircuitWasm = new byte[] { 1, 2, 3 },
                CircuitZkey = new byte[] { 4, 5, 6 },
                InputJson = "{\"a\":\"3\"}"
            });

            Assert.Equal(ZkProofScheme.Groth16, provider.Scheme);
            Assert.Equal(ZkProofScheme.Groth16, result.Scheme);
            Assert.Equal("{\"pi_a\":[\"1\",\"2\",\"1\"]}", result.ProofJson);
            Assert.Equal("[\"7\",\"11\"]", result.PublicSignalsJson);
            Assert.Equal(new System.Numerics.BigInteger[] { 7, 11 }, result.PublicSignals);
            Assert.Equal(new byte[] { 1, 2, 3 }, backend.Wasm);
            Assert.Equal(new byte[] { 4, 5, 6 }, backend.Zkey);
            Assert.Equal("{\"a\":\"3\"}", backend.Input);
            Assert.EndsWith("circuit.wasm", backend.WasmPath);
            Assert.EndsWith("circuit.zkey", backend.ZkeyPath);
            Assert.EndsWith("input.json", backend.InputJsonPath);
            Assert.False(File.Exists(backend.WasmPath));
        }

        [Fact]
        [NethereumDocExample(DocSection.Protocols, "snarkjs-proof-provider", "Guard clauses and missing circuit files", Order = 2)]
        public async Task Given_InvalidArguments_When_TheProviderOrBackendIsUsed_Then_ItRefuses()
        {
            Assert.Throws<ArgumentNullException>(() => new SnarkjsProofProvider(null!));

            var provider = new SnarkjsProofProvider(new RecordingBackend());
            await Assert.ThrowsAsync<ArgumentNullException>(() => provider.FullProveAsync(null!));

            var nodeBackend = new NodeJsSnarkjsBackend();
            var missing = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N"));
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => nodeBackend.FullProveAsync(missing + ".wasm", missing + ".zkey", missing + ".json"));
        }

        [Fact]
        [NethereumDocExample(DocSection.Protocols, "snarkjs-proof-provider", "Create a Node.js snarkjs provider", Order = 3)]
        public void Given_NodeAndSnarkjsPaths_When_CreateNodeJsRuns_Then_AGroth16ProviderIsReturnedWithoutStartingAnyProcess()
        {
            var provider = SnarkjsProofProvider.CreateNodeJs(nodePath: "node", snarkjsPath: "snarkjs/cli.js");
            var defaultProvider = SnarkjsProofProvider.CreateNodeJs();

            Assert.Equal(ZkProofScheme.Groth16, provider.Scheme);
            Assert.Equal(ZkProofScheme.Groth16, defaultProvider.Scheme);
        }
    }
}
