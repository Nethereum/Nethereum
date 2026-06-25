using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Default <see cref="ITransactionOrderingPolicy"/> for AppChain /
    /// DevChain sequencers: groups by recovered sender (first-seen sender
    /// order preserved), then within each sender sorts ascending by nonce.
    /// Pre-recovers senders so <see cref="TxEntry.CachedSender"/> is
    /// populated for the engine — saves one ECDSA per tx during execution.
    /// </summary>
    public sealed class MempoolNonceOrderingPolicy : ITransactionOrderingPolicy
    {
        public static readonly MempoolNonceOrderingPolicy Instance = new();

        public IReadOnlyList<TxEntry> Order(
            IEnumerable<ISignedTransaction> pool,
            BlockContext blockContext,
            BigInteger gasLimit,
            CancellationToken ct)
        {
            if (pool == null) return Array.Empty<TxEntry>();
            var transactions = pool as IList<ISignedTransaction> ?? new List<ISignedTransaction>(pool);
            if (transactions.Count == 0) return Array.Empty<TxEntry>();
            if (transactions.Count == 1)
            {
                var sender = GetTransactionSender(transactions[0]);
                return new[] { new TxEntry(transactions[0], sender) };
            }

            var grouped = new Dictionary<string, List<(int originalIndex, BigInteger nonce, ISignedTransaction tx, string? sender)>>(StringComparer.OrdinalIgnoreCase);
            var senderOrder = new List<string>();

            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                var txData = TransactionProcessor.GetTransactionData(tx);
                var sender = GetTransactionSender(tx);
                var key = sender ?? $"_unknown_{i}";

                if (!grouped.TryGetValue(key, out var list))
                {
                    list = new List<(int, BigInteger, ISignedTransaction, string?)>();
                    grouped[key] = list;
                    senderOrder.Add(key);
                }
                list.Add((i, txData.Nonce, tx, sender));
            }

            var result = new List<TxEntry>(transactions.Count);
            BigInteger gasBudget = 0;
            foreach (var senderKey in senderOrder)
            {
                var list = grouped[senderKey];
                list.Sort((a, b) => a.nonce.CompareTo(b.nonce));
                foreach (var entry in list)
                {
                    // Engine executes every tx the policy hands it (no skip
                    // logic at the engine layer per Pass 2 architectural
                    // decision 7). Filter declared-gas-limit overruns here
                    // so the engine never sees a tx whose stated gas would
                    // blow the block budget.
                    var txGasLimit = (BigInteger)entry.tx.GetGasLimit();
                    if (gasLimit > 0 && gasBudget + txGasLimit > gasLimit)
                        continue;
                    gasBudget += txGasLimit;
                    result.Add(new TxEntry(entry.tx, entry.sender));
                }
            }

            return result;
        }

        private static string? GetTransactionSender(ISignedTransaction tx)
        {
            try
            {
                var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return key?.GetPublicAddress();
            }
            catch
            {
                return null;
            }
        }
    }
}
