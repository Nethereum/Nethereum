using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.ZkProofs.Snarkjs
{
    public interface ISnarkjsBackend
    {
        Task<(string proofJson, string publicJson)> FullProveAsync(
            string wasmPath, string zkeyPath, string inputJsonPath, CancellationToken cancellationToken = default);
    }
}
