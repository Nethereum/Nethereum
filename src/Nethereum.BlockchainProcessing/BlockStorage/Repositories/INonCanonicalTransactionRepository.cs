using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface INonCanonicalTransactionRepository
    {
        Task MarkNonCanonicalAsync(BigInteger blockNumber);
    }
}
