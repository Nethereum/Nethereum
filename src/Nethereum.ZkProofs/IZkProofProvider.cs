using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.ZkProofs
{
    public interface IZkProofProvider
    {
        Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default);

        ZkProofScheme Scheme { get; }
    }
}
