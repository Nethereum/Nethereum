using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.ZkProofs.Snarkjs
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.Protocols, "snarkjs-proof-provider", "ISnarkjsBackend")]
    public interface ISnarkjsBackend
    {
        Task<(string proofJson, string publicJson)> FullProveAsync(
            string wasmPath, string zkeyPath, string inputJsonPath, CancellationToken cancellationToken = default);
    }
}
