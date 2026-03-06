using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface INonCanonicalTransactionLogRepository
    {
        Task MarkNonCanonicalAsync(BigInteger blockNumber);
    }
}
