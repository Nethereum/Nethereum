using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Abstractions
{
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
