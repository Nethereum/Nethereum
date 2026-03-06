using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface INonCanonicalBlockRepository
    {
        Task MarkNonCanonicalAsync(BigInteger blockNumber);
    }
}
