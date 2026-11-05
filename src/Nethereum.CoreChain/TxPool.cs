using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Threading;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.CoreChain
{
    public class TxPool : ITxPool
    {
        private const int DefaultMaxPoolSize = 10_000;

        private readonly ConcurrentDictionary<string, PendingTransaction> _pending = new();
        private NonceTracking _tracking = new NonceTracking();

        private sealed class NonceTracking
        {
            public readonly ConcurrentDictionary<string, BigInteger> Nonces = new(StringComparer.OrdinalIgnoreCase);
            public readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.OrdinalIgnoreCase);
        }
        private readonly ITxPoolOrderingStrategy _orderingStrategy;
        private readonly int _maxPoolSize;
        private readonly int _maxTxsPerSender;

        public int MaxTxsPerSender => _maxTxsPerSender;

        public TxPool(ITxPoolOrderingStrategy orderingStrategy = null, int maxPoolSize = DefaultMaxPoolSize, int maxTxsPerSender = 1_000)
        {
            _orderingStrategy = orderingStrategy ?? new FifoOrderingStrategy();
            _maxPoolSize = maxPoolSize;
            _maxTxsPerSender = maxTxsPerSender;
        }

        public int PendingCount => _pending.Count;

        public Task<byte[]> AddAsync(ISignedTransaction transaction)
        {
            if (_pending.Count >= _maxPoolSize)
                throw new InvalidOperationException("Transaction pool is full");

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

        public Task<int> GetPendingCountAsync() => Task.FromResult(_pending.Count);

        public Task<bool> RemoveAsync(byte[] txHash)
        {
            var hashKey = Convert.ToHexString(txHash).ToLowerInvariant();
            return Task.FromResult(_pending.TryRemove(hashKey, out _));
        }

        public Task<int> RemoveBatchAsync(IEnumerable<byte[]> txHashes)
        {
            int removed = 0;
            foreach (var txHash in txHashes)
            {
                var hashKey = Convert.ToHexString(txHash).ToLowerInvariant();
                if (_pending.TryRemove(hashKey, out _)) removed++;
            }
            return Task.FromResult(removed);
        }

        public Task<bool> ContainsAsync(byte[] txHash)
        {
            var hashKey = Convert.ToHexString(txHash).ToLowerInvariant();
            return Task.FromResult(_pending.ContainsKey(hashKey));
        }

        public Task<IReadOnlyList<ISignedTransaction>> GetPendingAsync(int maxCount)
        {
            var snapshot = _pending.Values.ToList();
            var transactions = _orderingStrategy.Order(snapshot)
                .Take(maxCount)
                .Select(p => p.Transaction)
                .ToList();

            return Task.FromResult<IReadOnlyList<ISignedTransaction>>(transactions);
        }

        public Task ClearAsync()
        {
            _pending.Clear();
            Interlocked.Exchange(ref _tracking, new NonceTracking());
            return Task.CompletedTask;
        }

        public Task<BigInteger> GetPendingNonceAsync(string senderAddress, BigInteger confirmedNonce)
        {
            if (_tracking.Nonces.TryGetValue(senderAddress, out var pendingNonce) && pendingNonce > confirmedNonce)
            {
                return Task.FromResult(pendingNonce);
            }
            return Task.FromResult(confirmedNonce);
        }

        public void TrackPendingNonce(string senderAddress, BigInteger nonce)
        {
            var nextNonce = nonce + 1;
            _tracking.Nonces.AddOrUpdate(senderAddress, nextNonce, (_, existing) =>
                nextNonce > existing ? nextNonce : existing);
        }

        public void ResetPendingNonces()
        {
            var rebuilt = new NonceTracking();
            var verifier = new TransactionVerificationAndRecoveryImp();
            foreach (var pending in _pending.Values)
            {
                var sender = verifier.GetSenderAddress(pending.Transaction);
                var next = pending.Transaction.GetNonce().ToBigInteger() + BigInteger.One;
                rebuilt.Nonces.AddOrUpdate(sender, next, (_, current) => next > current ? next : current);
                rebuilt.Counts.AddOrUpdate(sender, 1, (_, current) => current + 1);
            }
            Interlocked.Exchange(ref _tracking, rebuilt);
        }

        public int GetSenderTxCount(string senderAddress)
        {
            return _tracking.Counts.TryGetValue(senderAddress, out var count) ? count : 0;
        }

        public void IncrementSenderTxCount(string senderAddress)
        {
            _tracking.Counts.AddOrUpdate(senderAddress, 1, (_, existing) => existing + 1);
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
                Transaction7702 tx7702 => tx7702.MaxFeePerGas ?? 0,
                Transaction2930 tx2930 => tx2930.GasPrice ?? 0,
                LegacyTransaction legacyTx => legacyTx.GasPrice.ToBigIntegerFromRLPDecoded(),
                LegacyTransactionChainId legacyChainTx => legacyChainTx.GasPrice.ToBigIntegerFromRLPDecoded(),
                _ => 0
            };
        }
    }
}
