using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.RPC.TxPool.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    internal static class PendingTransactionInfoBuilder
    {
        public static List<(string From, string Nonce, PendingTransactionInfo Info)> Build(
            IEnumerable<ISignedTransaction> pendingTransactions)
        {
            var entries = new List<(string From, string Nonce, PendingTransactionInfo Info)>();

            foreach (var signedTx in pendingTransactions)
            {
                var info = TransactionRpcBuilder.Populate(new PendingTransactionInfo(), signedTx, null, null, null, null);
                var from = info.From.ToLowerInvariant();
                var nonce = info.Nonce.Value.ToString();

                entries.Add((from, nonce, info));
            }

            return entries;
        }
    }
}
