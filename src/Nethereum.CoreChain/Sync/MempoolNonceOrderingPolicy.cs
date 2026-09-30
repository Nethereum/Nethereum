using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
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
                var sender = GetTransactionSender(tx);
                var key = sender ?? $"_unknown_{i}";

                if (!grouped.TryGetValue(key, out var list))
                {
                    list = new List<(int, BigInteger, ISignedTransaction, string?)>();
                    grouped[key] = list;
                    senderOrder.Add(key);
                }
                list.Add((i, tx.GetNonce().ToBigInteger(), tx, sender));
            }

            var result = new List<TxEntry>(transactions.Count);
            BigInteger gasBudget = 0;
            foreach (var senderKey in senderOrder)
            {
                var list = grouped[senderKey];
                list.Sort((a, b) => a.nonce.CompareTo(b.nonce));
                foreach (var entry in list)
                {
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
