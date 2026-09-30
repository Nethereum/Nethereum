using System;
using Microsoft.Extensions.Logging;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Model.P2P;

namespace Nethereum.ChainNode.Hosting
{
    public sealed class TrustedTransactionAdmission
    {
        private readonly RelayMempool _mempool;
        private readonly ILogger _logger;

        public TrustedTransactionAdmission(RelayMempool mempool, ILogger logger = null)
        {
            _mempool = mempool ?? throw new ArgumentNullException(nameof(mempool));
            _logger = logger;
        }

        public void Admit(TransactionsMessage message) => Admit(message, sourcePeer: null);

        public void AdmitFrom(TransactionsMessage message, Guid sourcePeer) =>
            Admit(message, sourcePeer == Guid.Empty ? (Guid?)null : sourcePeer);

        private void Admit(TransactionsMessage message, Guid? sourcePeer)
        {
            if (message?.Transactions == null) return;

            _ = AdmitAsync(message, sourcePeer);
        }

        private async System.Threading.Tasks.Task AdmitAsync(TransactionsMessage message, Guid? sourcePeer)
        {
            try
            {
                await _mempool.AdmitFromTrustedPeerAsync(message.Transactions, sourcePeer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Rejected {Count} transactions pushed by a trusted peer",
                    message.Transactions.Count);
            }
        }
    }
}
