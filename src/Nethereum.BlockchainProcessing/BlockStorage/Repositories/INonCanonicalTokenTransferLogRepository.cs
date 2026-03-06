using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface INonCanonicalTokenTransferLogRepository
    {
        Task MarkNonCanonicalAsync(BigInteger blockNumber);
    }
}
