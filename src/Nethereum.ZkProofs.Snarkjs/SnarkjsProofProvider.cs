using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.ZkProofs.Snarkjs
{
    public class SnarkjsProofProvider : IZkProofProvider
    {
        private readonly ISnarkjsBackend _backend;

        public ZkProofScheme Scheme => ZkProofScheme.Groth16;

        public SnarkjsProofProvider()
            : this(new NodeJsSnarkjsBackend())
        {
        }

        public SnarkjsProofProvider(ISnarkjsBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public static SnarkjsProofProvider CreateNodeJs(string? nodePath = null, string? snarkjsPath = null)
        {
            return new SnarkjsProofProvider(new NodeJsSnarkjsBackend(nodePath, snarkjsPath));
        }

        public async Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var tempDir = Path.Combine(Path.GetTempPath(), "nethereum_zkproofs_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);

            try
            {
                var wasmPath = Path.Combine(tempDir, "circuit.wasm");
                var zkeyPath = Path.Combine(tempDir, "circuit.zkey");
                var inputPath = Path.Combine(tempDir, "input.json");

                await File.WriteAllBytesAsync(wasmPath, request.CircuitWasm, cancellationToken);
                await File.WriteAllBytesAsync(zkeyPath, request.CircuitZkey, cancellationToken);
                await File.WriteAllTextAsync(inputPath, request.InputJson, cancellationToken);

                var (proofJson, publicJson) = await _backend.FullProveAsync(
                    wasmPath, zkeyPath, inputPath, cancellationToken);

                return ZkProofResult.BuildFromJson(ZkProofScheme.Groth16, proofJson, publicJson);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
