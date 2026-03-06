using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class InMemoryTransactionStoreTests
    {
        private static byte[] MakeHash(int seed)
        {
            var hash = new byte[32];
            hash[0] = (byte)(seed & 0xFF);
            hash[1] = (byte)((seed >> 8) & 0xFF);
            return hash;
        }

        private static ISignedTransaction MakeTx(byte[] hash)
        {
            var tx = new LegacyTransaction(
                nonce: new byte[] { 0x00 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { 0x00 },
                data: Array.Empty<byte>()
            );
            return new TestSignedTransaction(tx, hash);
        }

        [Fact]
        public async Task SaveAsync_GetByHash_RoundTrips()
        {
            var store = new InMemoryTransactionStore();
            var txHash = MakeHash(1);
            var tx = MakeTx(txHash);

            await store.SaveAsync(tx, MakeHash(100), 0, 42);

            var result = await store.GetByHashAsync(txHash);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetLocation_ReturnsBlockNumber()
        {
            var store = new InMemoryTransactionStore();
            var txHash = MakeHash(2);
            var blockHash = MakeHash(101);
            var blockNumber = new BigInteger(99);

            await store.SaveAsync(MakeTx(txHash), blockHash, 3, blockNumber);

            var location = await store.GetLocationAsync(txHash);
            Assert.NotNull(location);
            Assert.Equal(blockNumber, location.BlockNumber);
            Assert.Equal(3, location.TransactionIndex);
            Assert.Equal(blockHash, location.BlockHash);
        }

        [Fact]
        public async Task GetLocation_MissingTx_ReturnsNull()
        {
            var store = new InMemoryTransactionStore();
            var result = await store.GetLocationAsync(MakeHash(999));
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByBlockHash_ReturnsTxsInOrder()
        {
            var store = new InMemoryTransactionStore();
            var blockHash = MakeHash(200);

            await store.SaveAsync(MakeTx(MakeHash(10)), blockHash, 2, 5);
            await store.SaveAsync(MakeTx(MakeHash(11)), blockHash, 0, 5);
            await store.SaveAsync(MakeTx(MakeHash(12)), blockHash, 1, 5);

            var txs = await store.GetByBlockHashAsync(blockHash);
            Assert.Equal(3, txs.Count);
        }

        [Fact]
        public async Task GetByBlockHash_DoesNotReturnOtherBlocks()
        {
            var store = new InMemoryTransactionStore();
            var block1 = MakeHash(201);
            var block2 = MakeHash(202);

            await store.SaveAsync(MakeTx(MakeHash(20)), block1, 0, 1);
            await store.SaveAsync(MakeTx(MakeHash(21)), block2, 0, 2);

            var txs1 = await store.GetByBlockHashAsync(block1);
            Assert.Single(txs1);

            var txs2 = await store.GetByBlockHashAsync(block2);
            Assert.Single(txs2);
        }

        [Fact]
        public async Task Clear_RemovesAll()
        {
            var store = new InMemoryTransactionStore();
            var txHash = MakeHash(30);
            await store.SaveAsync(MakeTx(txHash), MakeHash(300), 0, 10);

            store.Clear();

            var result = await store.GetByHashAsync(txHash);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByBlockNumber_RequiresBlockStore()
        {
            var blockStore = new InMemoryBlockStore();
            var store = new InMemoryTransactionStore(blockStore);

            var blockHash = MakeHash(400);
            var header = new BlockHeader
            {
                BlockNumber = 7,
                ParentHash = new byte[32],
                UnclesHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                GasLimit = 30_000_000,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
                Coinbase = AddressUtil.ZERO_ADDRESS
            };

            await blockStore.SaveAsync(header, blockHash);
            await store.SaveAsync(MakeTx(MakeHash(40)), blockHash, 0, 7);

            var txs = await store.GetByBlockNumberAsync(7);
            Assert.Single(txs);
        }

        private class TestSignedTransaction : ISignedTransaction
        {
            private readonly ISignedTransaction _inner;
            private readonly byte[] _hash;

            public TestSignedTransaction(ISignedTransaction inner, byte[] hash)
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
