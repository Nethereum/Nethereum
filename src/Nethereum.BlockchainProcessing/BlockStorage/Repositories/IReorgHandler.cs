using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface IReorgHandler
    {
        Task MarkBlockRangeNonCanonicalAsync(BigInteger fromBlock, BigInteger toBlock);
    }
}
