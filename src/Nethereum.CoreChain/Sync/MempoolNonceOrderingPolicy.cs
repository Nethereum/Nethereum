using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    public sealed class MempoolNonceOrderingPolicy : ITransactionOrderingPolicy
    {
        public static readonly MempoolNonceOrderingPolicy Instance = new MempoolNonceOrderingPolicy(null);

        private readonly SenderRecoveryStep _senderRecovery;

        public MempoolNonceOrderingPolicy(ITransactionVerificationAndRecovery? verifier)
        {
            _senderRecovery = new SenderRecoveryStep(verifier);
        }

        public IReadOnlyList<TxEntry> Order(
            IEnumerable<ISignedTransaction> pool,
            BlockContext blockContext,
            CancellationToken ct)
        {
            if (pool == null) return Array.Empty<TxEntry>();
            var transactions = pool as IList<ISignedTransaction> ?? new List<ISignedTransaction>(pool);
            if (transactions.Count == 0) return Array.Empty<TxEntry>();
            var senders = _senderRecovery.Begin(transactions.ToArray());
            if (transactions.Count == 1)
                return new[] { new TxEntry(transactions[0], senders.WaitForSender(0, transactions[0])) };

            var grouped = new Dictionary<string, List<(int originalIndex, BigInteger nonce, ISignedTransaction tx, string? sender)>>(StringComparer.OrdinalIgnoreCase);
            var senderOrder = new List<string>();

            for (int i = 0; i < transactions.Count; i++)
            {
                var tx = transactions[i];
                var sender = senders.WaitForSender(i, tx);
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
            foreach (var senderKey in senderOrder)
            {
                var list = grouped[senderKey];
                list.Sort((a, b) => a.nonce.CompareTo(b.nonce));
                foreach (var entry in list)
                {
                    result.Add(new TxEntry(entry.tx, entry.sender));
                }
            }

            return result;
        }
    }
}
