using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class TxPoolTests
    {
        private static ISignedTransaction CreateMockTransaction(byte[] hash = null)
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x00 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { 0x00 },
                data: Array.Empty<byte>()
            );

            if (hash != null)
            {
                return new TestTransaction(tx, hash);
            }

            return tx;
        }

        private static byte[] MakeHash(int seed)
        {
            var hash = new byte[32];
            hash[0] = (byte)(seed & 0xFF);
            hash[1] = (byte)((seed >> 8) & 0xFF);
            return hash;
        }

        [Fact]
        public async Task AddAsync_ReturnsTxHash()
        {
            var pool = new TxPool();
            var tx = CreateMockTransaction(MakeHash(1));
            var hash = await pool.AddAsync(tx);
            Assert.NotNull(hash);
        }

        [Fact]
        public async Task AddAsync_IncrementsPendingCount()
        {
            var pool = new TxPool();
            Assert.Equal(0, pool.PendingCount);

            await pool.AddAsync(CreateMockTransaction(MakeHash(1)));
            Assert.Equal(1, pool.PendingCount);

            await pool.AddAsync(CreateMockTransaction(MakeHash(2)));
            Assert.Equal(2, pool.PendingCount);
        }

        [Fact]
        public async Task AddAsync_ThrowsWhenPoolFull()
        {
            var pool = new TxPool(maxPoolSize: 2);

            await pool.AddAsync(CreateMockTransaction(MakeHash(1)));
            await pool.AddAsync(CreateMockTransaction(MakeHash(2)));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => pool.AddAsync(CreateMockTransaction(MakeHash(3))));
            Assert.Contains("full", ex.Message);
            Assert.Equal(2, pool.PendingCount);
        }

        [Fact]
        public async Task AddAsync_DuplicateHashDoesNotIncrement()
        {
            var pool = new TxPool();
            var hash = MakeHash(1);

            await pool.AddAsync(CreateMockTransaction(hash));
            await pool.AddAsync(CreateMockTransaction(hash));

            Assert.Equal(1, pool.PendingCount);
        }

        [Fact]
        public async Task GetByHashAsync_ReturnsTransaction()
        {
            var pool = new TxPool();
            var hash = MakeHash(1);
            var tx = CreateMockTransaction(hash);
            await pool.AddAsync(tx);

            var result = await pool.GetByHashAsync(hash);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetByHashAsync_ReturnsNullForMissing()
        {
            var pool = new TxPool();
            var result = await pool.GetByHashAsync(MakeHash(99));
            Assert.Null(result);
        }

        [Fact]
        public async Task RemoveAsync_RemovesTransaction()
        {
            var pool = new TxPool();
            var hash = MakeHash(1);
            await pool.AddAsync(CreateMockTransaction(hash));

            var removed = await pool.RemoveAsync(hash);
            Assert.True(removed);
            Assert.Equal(0, pool.PendingCount);
        }

        [Fact]
        public async Task RemoveAsync_ReturnsFalseForMissing()
        {
            var pool = new TxPool();
            var removed = await pool.RemoveAsync(MakeHash(99));
            Assert.False(removed);
        }

        [Fact]
        public async Task RemoveBatchAsync_RemovesMultiple()
        {
            var pool = new TxPool();
            var hash1 = MakeHash(1);
            var hash2 = MakeHash(2);
            var hash3 = MakeHash(3);

            await pool.AddAsync(CreateMockTransaction(hash1));
            await pool.AddAsync(CreateMockTransaction(hash2));
            await pool.AddAsync(CreateMockTransaction(hash3));

            var removed = await pool.RemoveBatchAsync(new[] { hash1, hash3 });
            Assert.Equal(2, removed);
            Assert.Equal(1, pool.PendingCount);
            Assert.True(await pool.ContainsAsync(hash2));
        }

        [Fact]
        public async Task ContainsAsync_ReturnsTrueForExisting()
        {
            var pool = new TxPool();
            var hash = MakeHash(1);
            await pool.AddAsync(CreateMockTransaction(hash));
            Assert.True(await pool.ContainsAsync(hash));
        }

        [Fact]
        public async Task ContainsAsync_ReturnsFalseForMissing()
        {
            var pool = new TxPool();
            Assert.False(await pool.ContainsAsync(MakeHash(99)));
        }

        [Fact]
        public async Task GetPendingAsync_ReturnsUpToMaxCount()
        {
            var pool = new TxPool();
            for (int i = 0; i < 5; i++)
            {
                await pool.AddAsync(CreateMockTransaction(MakeHash(i)));
            }

            var pending = await pool.GetPendingAsync(3);
            Assert.Equal(3, pending.Count);
        }

        [Fact]
        public async Task GetPendingAsync_ReturnsAllWhenLessThanMax()
        {
            var pool = new TxPool();
            for (int i = 0; i < 3; i++)
            {
                await pool.AddAsync(CreateMockTransaction(MakeHash(i)));
            }

            var pending = await pool.GetPendingAsync(100);
            Assert.Equal(3, pending.Count);
        }

        [Fact]
        public async Task ClearAsync_RemovesAllTransactionsAndNonces()
        {
            var pool = new TxPool();
            await pool.AddAsync(CreateMockTransaction(MakeHash(1)));
            pool.TrackPendingNonce("0xabc", 5);
            pool.IncrementSenderTxCount("0xabc");

            await pool.ClearAsync();

            Assert.Equal(0, pool.PendingCount);
            Assert.Equal(0, pool.GetSenderTxCount("0xabc"));
            var nonce = await pool.GetPendingNonceAsync("0xabc", 0);
            Assert.Equal(BigInteger.Zero, nonce);
        }

        [Fact]
        public async Task GetPendingNonceAsync_ReturnsConfirmedWhenNoPending()
        {
            var pool = new TxPool();
            var nonce = await pool.GetPendingNonceAsync("0xabc", 5);
            Assert.Equal(new BigInteger(5), nonce);
        }

        [Fact]
        public async Task GetPendingNonceAsync_ReturnsPendingWhenHigher()
        {
            var pool = new TxPool();
            pool.TrackPendingNonce("0xabc", 10);

            var nonce = await pool.GetPendingNonceAsync("0xabc", 5);
            Assert.Equal(new BigInteger(11), nonce);
        }

        [Fact]
        public async Task GetPendingNonceAsync_ReturnsConfirmedWhenHigherThanPending()
        {
            var pool = new TxPool();
            pool.TrackPendingNonce("0xabc", 3);

            var nonce = await pool.GetPendingNonceAsync("0xabc", 10);
            Assert.Equal(new BigInteger(10), nonce);
        }

        [Fact]
        public void TrackPendingNonce_TracksHighestNonce()
        {
            var pool = new TxPool();
            pool.TrackPendingNonce("0xabc", 5);
            pool.TrackPendingNonce("0xabc", 3);
            pool.TrackPendingNonce("0xabc", 8);

            var nonce = pool.GetPendingNonceAsync("0xabc", 0).Result;
            Assert.Equal(new BigInteger(9), nonce);
        }

        [Fact]
        public void GetSenderTxCount_ReturnsZeroForUnknown()
        {
            var pool = new TxPool();
            Assert.Equal(0, pool.GetSenderTxCount("0xunknown"));
        }

        [Fact]
        public void IncrementSenderTxCount_IncrementsCorrectly()
        {
            var pool = new TxPool();
            pool.IncrementSenderTxCount("0xabc");
            pool.IncrementSenderTxCount("0xabc");
            pool.IncrementSenderTxCount("0xabc");

            Assert.Equal(3, pool.GetSenderTxCount("0xabc"));
        }

        [Fact]
        public void ResetPendingNonces_ClearsNoncesAndCounts()
        {
            var pool = new TxPool();
            pool.TrackPendingNonce("0xabc", 10);
            pool.IncrementSenderTxCount("0xabc");

            pool.ResetPendingNonces();

            var nonce = pool.GetPendingNonceAsync("0xabc", 0).Result;
            Assert.Equal(BigInteger.Zero, nonce);
            Assert.Equal(0, pool.GetSenderTxCount("0xabc"));
        }

        [Fact]
        public async Task ConcurrentAdd_ThreadSafe()
        {
            var pool = new TxPool(maxPoolSize: 1000);

            var tasks = Enumerable.Range(0, 100).Select(i =>
                Task.Run(() => pool.AddAsync(CreateMockTransaction(MakeHash(i)))));

            await Task.WhenAll(tasks);

            Assert.Equal(100, pool.PendingCount);
        }

        [Fact]
        public async Task ConcurrentAddAndRemove_ThreadSafe()
        {
            var pool = new TxPool(maxPoolSize: 1000);
            var hashes = Enumerable.Range(0, 50).Select(MakeHash).ToArray();

            foreach (var hash in hashes)
            {
                await pool.AddAsync(CreateMockTransaction(hash));
            }

            var addTasks = Enumerable.Range(50, 50).Select(i =>
                Task.Run(async () => { await pool.AddAsync(CreateMockTransaction(MakeHash(i))); }));

            var removeTasks = Enumerable.Range(0, 25).Select(i =>
                Task.Run(async () => { await pool.RemoveAsync(hashes[i]); }));

            await Task.WhenAll(addTasks.Concat(removeTasks));

            Assert.True(pool.PendingCount >= 25);
            Assert.True(pool.PendingCount <= 100);
        }

        [Fact]
        public void ConcurrentNonceTracking_ThreadSafe()
        {
            var pool = new TxPool();

            var tasks = Enumerable.Range(0, 100).Select(i =>
                Task.Run(() => pool.TrackPendingNonce("0xabc", i)));

            Task.WaitAll(tasks.ToArray());

            var nonce = pool.GetPendingNonceAsync("0xabc", 0).Result;
            Assert.Equal(new BigInteger(100), nonce);
        }

        [Fact]
        public async Task GetPendingNonceAsync_CaseInsensitive()
        {
            var pool = new TxPool();
            pool.TrackPendingNonce("0xABC", 10);

            var nonce = await pool.GetPendingNonceAsync("0xabc", 0);
            Assert.Equal(new BigInteger(11), nonce);
        }

        private class TestTransaction : ISignedTransaction
        {
            private readonly ISignedTransaction _inner;
            private readonly byte[] _hash;

            public TestTransaction(ISignedTransaction inner, byte[] hash)
            {
                _inner = inner;
                _hash = hash;
            }

            public byte[] Hash => _hash;
            public byte[] RawHash => _inner.RawHash;
            public ISignature Signature => _inner.Signature;
            public TransactionType TransactionType => _inner.TransactionType;
            public byte[] GetRLPEncoded() => _inner.GetRLPEncoded();
            public byte[] GetRLPEncodedRaw() => _inner.GetRLPEncodedRaw();
            public void SetSignature(ISignature signature) => _inner.SetSignature(signature);
        }
    }
}
