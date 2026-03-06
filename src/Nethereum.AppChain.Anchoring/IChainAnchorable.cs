using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.Anchoring
{
    public interface IChainAnchorable
    {
        Task<BigInteger> GetBlockNumberAsync();
        Task<BlockHeader?> GetBlockByNumberAsync(BigInteger blockNumber);
        Task<byte[]?> GetBlockHashByNumberAsync(BigInteger blockNumber);
    }
}
