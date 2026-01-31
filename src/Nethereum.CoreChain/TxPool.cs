using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain
{
    public class TxPool : ITxPool
    {
        private readonly ConcurrentDictionary<string, PendingTransaction> _pending = new();
        private readonly ITxPoolOrderingStrategy _orderingStrategy;
        private readonly object _lock = new();

        public TxPool(ITxPoolOrderingStrategy orderingStrategy = null)
        {
            _orderingStrategy = orderingStrategy ?? new FifoOrderingStrategy();
        }

        public int PendingCount => _pending.Count;

        public Task<byte[]> AddAsync(ISignedTransaction transaction)
        {
            var txHash = transaction.Hash;
            var hashKey = Convert.ToHexString(txHash).ToLowerInvariant();

            var pendingTx = new PendingTransaction
            {
                Transaction = transaction,
                TxHash = txHash,
                ReceivedAt = DateTime.UtcNow
            };

            _pending.TryAdd(hashKey, pendingTx);
            return Task.FromResult(txHash);
        }

        public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
        {
            var hashKey = Convert.ToHexString(txHash).ToLowerInvariant();
            if (_pending.TryGetValue(hashKey, out var pending))
            {
                return Task.FromResult(pending.Transaction);
            }
            return Task.FromResult<ISignedTransaction>(null);
        }

        public Task<bool> RemoveAsync(byte[] txHash)
        {
            var hashKey = Convert.ToHexString(txHash).ToLowerInvariant();
            return Task.FromResult(_pending.TryRemove(hashKey, out _));
        }

        public Task<IReadOnlyList<ISignedTransaction>> GetPendingAsync(int maxCount)
        {
            lock (_lock)
            {
                var orderedTransactions = _orderingStrategy.Order(_pending.Values);
                var transactions = orderedTransactions
                    .Take(maxCount)
                    .Select(p => p.Transaction)
                    .ToList();

                return Task.FromResult<IReadOnlyList<ISignedTransaction>>(transactions);
            }
        }

        public Task ClearAsync()
        {
            _pending.Clear();
            return Task.CompletedTask;
        }
    }

    public class FifoOrderingStrategy : ITxPoolOrderingStrategy
    {
        public IEnumerable<PendingTransaction> Order(IEnumerable<PendingTransaction> transactions)
        {
            return transactions.OrderBy(p => p.ReceivedAt);
        }
    }

    public class GasPriceOrderingStrategy : ITxPoolOrderingStrategy
    {
        public IEnumerable<PendingTransaction> Order(IEnumerable<PendingTransaction> transactions)
        {
            return transactions
                .OrderByDescending(p => GetGasPrice(p.Transaction))
                .ThenBy(p => p.ReceivedAt);
        }

        private System.Numerics.BigInteger GetGasPrice(ISignedTransaction tx)
        {
            return tx switch
            {
                Transaction1559 tx1559 => tx1559.MaxFeePerGas ?? 0,
                Transaction2930 tx2930 => tx2930.GasPrice ?? 0,
                LegacyTransaction legacyTx => legacyTx.GasPrice.ToBigIntegerFromRLPDecoded(),
                LegacyTransactionChainId legacyChainTx => legacyChainTx.GasPrice.ToBigIntegerFromRLPDecoded(),
                _ => 0
            };
        }
    }
}
