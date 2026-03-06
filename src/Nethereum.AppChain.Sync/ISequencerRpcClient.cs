using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.Sync
{
    public interface ISequencerRpcClient
    {
        Task<BigInteger> GetBlockNumberAsync(CancellationToken cancellationToken = default);
        Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, CancellationToken cancellationToken = default);
        Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, CancellationToken cancellationToken = default);
        Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, CancellationToken cancellationToken = default);
    }
}
