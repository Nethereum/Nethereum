using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface ISequencerRpcClient
    {
        Task<BigInteger> GetBlockNumberAsync(CancellationToken cancellationToken = default);
        Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, CancellationToken cancellationToken = default);

        async Task<IList<LiveBlockData>> GetBlocksWithReceiptsAsync(BigInteger fromBlock, int count, CancellationToken cancellationToken = default)
        {
            var blocks = new List<LiveBlockData>();
            for (var i = 0; i < count; i++)
            {
                var data = await GetBlockWithReceiptsAsync(fromBlock + i, cancellationToken);
                if (data == null) break;
                blocks.Add(data);
            }
            return blocks;
        }

        Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, CancellationToken cancellationToken = default);
        Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, CancellationToken cancellationToken = default);
    }
}
