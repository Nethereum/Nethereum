using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Strategy for broadcasting newly produced/imported blocks to connected peers.
    /// Implementations:
    /// - DevP2PBlockPublisher: broadcasts NewBlock + NewBlockHashes over eth/68
    /// - NoopBlockPublisher: no broadcast (follower-only nodes)
    /// </summary>
    public interface IBlockPublisher
    {
        Task BroadcastNewBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<Withdrawal>? withdrawals,
            BigInteger totalDifficulty,
            CancellationToken cancellationToken = default);

        int ConnectedPeerCount { get; }
    }
}
