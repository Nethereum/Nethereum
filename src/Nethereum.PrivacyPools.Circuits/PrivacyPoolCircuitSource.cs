using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ZkProofs;

namespace Nethereum.PrivacyPools.Circuits
{
    public class PrivacyPoolCircuitSource : ICircuitArtifactSource, ICircuitGraphSource
    {
        private static readonly Assembly ResourceAssembly = typeof(PrivacyPoolCircuitSource).Assembly;
        private const string ResourcePrefix = "Nethereum.PrivacyPools.Circuits.circuits.";

        public const string CommitmentCircuit = "commitment";
        public const string WithdrawalCircuit = "withdrawal";

        public Task<byte[]> GetWasmAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LoadResource(circuitName, $"{circuitName}.wasm"));
        }

        public Task<byte[]> GetZkeyAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LoadResource(circuitName, $"{circuitName}.zkey"));
        }

        public byte[] GetVerificationKey(string circuitName)
        {
            return LoadResource(circuitName, $"{circuitName}_vk.json");
        }

        public string GetVerificationKeyJson(string circuitName)
        {
            var bytes = GetVerificationKey(circuitName);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public bool HasCircuit(string circuitName)
        {
            var resourceName = $"{ResourcePrefix}{circuitName}.{circuitName}.wasm";
            using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
            return stream != null;
        }

        public byte[] GetGraphData(string circuitName)
        {
            return LoadResource(circuitName, $"{circuitName}.graph.bin");
        }

        public bool HasGraph(string circuitName)
        {
            var resourceName = $"{ResourcePrefix}{circuitName}.{circuitName}.graph.bin";
            using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
            return stream != null;
        }

        private static byte[] LoadResource(string circuitName, string fileName)
        {
            var resourceName = $"{ResourcePrefix}{circuitName}.{fileName}";
            using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException(
                    $"Circuit artifact '{fileName}' not found as embedded resource. " +
                    $"Expected resource: {resourceName}",
                    resourceName);

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
