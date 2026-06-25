using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Strategy for serving eth/68 read requests from incoming peers.
    /// Implementations:
    /// - StorageBackedEth68Handler: serves blocks/bodies/receipts from local stores
    /// - DelegatingEth68Handler: forwards to another peer (proxy/relay scenarios)
    /// </summary>
    public interface IEth68RequestHandler
    {
        Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default);
        Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default);
        Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Serve eth/68 message 0x09 GetPooledTransactions. Returns the
        /// signed transaction bodies for each known hash; unknown hashes
        /// are silently omitted per EIP-5793. May return fewer txs than
        /// requested when the response would exceed the 2 MiB soft cap.
        /// Powers both regular DevP2P mempool propagation and the
        /// AppChain HA mempool replication path: standby sequencer
        /// receives NewPooledTransactionHashes from the active, fires
        /// GetPooledTransactions, ingests the returned txs into its own
        /// TxPool — so on L1-fenced failover the mempool is hot.
        /// </summary>
        Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default);
    }
}
