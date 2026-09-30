using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Publish
{
    public class DevP2PBlockPublisher : IBlockPublisher
    {
        private readonly Eth68PeerPool _pool;

        public DevP2PBlockPublisher(Eth68PeerPool pool)
        {
            _pool = pool;
        }

        public int ConnectedPeerCount => _pool.Count;

        public async Task BroadcastNewBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<Withdrawal>? withdrawals,
            BigInteger totalDifficulty,
            CancellationToken cancellationToken = default)
        {
            var msg = new NewBlockMessage
            {
                Header = header,
                Transactions = new List<ISignedTransaction>(transactions),
                Uncles = new List<BlockHeader>(uncles),
                Withdrawals = withdrawals != null ? new List<Withdrawal>(withdrawals) : null,
                TotalDifficulty = totalDifficulty
            };
            var payload = NewBlockMessageEncoder.Encode(msg);

            await _pool.BroadcastAsync(Eth68MessageIds.NewBlock, payload, cancellationToken: cancellationToken);
        }
    }
}
