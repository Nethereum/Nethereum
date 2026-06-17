using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Broadcasts NewBlock over eth/68 to every peer in the pool. Used by an
    /// active sequencer node (target #4 publish role). Followers/passive nodes
    /// inject a NoopBlockPublisher.
    /// </summary>
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

            foreach (var peer in _pool.Peers)
            {
                var msgId = peer.EthOffset + Eth68MessageIds.NewBlock;
                try
                {
                    await peer.Connection.SendMessageAsync(msgId, payload, cancellationToken);
                }
                catch
                {
                    _pool.Remove(peer.Id);
                }
            }
        }
    }
}
