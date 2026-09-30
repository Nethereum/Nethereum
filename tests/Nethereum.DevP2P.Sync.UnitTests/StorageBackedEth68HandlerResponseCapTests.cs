using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Serving.Strategies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Serving.Strategies;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class StorageBackedEth68HandlerResponseCapTests
    {
        private const int ExpectedResponseSoftCapBytes = 2 * 1024 * 1024;

        private static byte[] HashOf(int seed)
        {
            var bytes = new byte[32];
            bytes[31] = (byte)(seed & 0xFF);
            bytes[30] = (byte)((seed >> 8) & 0xFF);
            return bytes;
        }

        private static BlockHeader MakeHeader(int blockNumber)
        {
            return new BlockHeader
            {
                BlockNumber = (EvmUInt256)(ulong)blockNumber,
                ParentHash = blockNumber == 0 ? new byte[32] : HashOf(blockNumber - 1),
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                MixHash = new byte[32],
                ExtraData = new byte[0],
                Nonce = new byte[8],
                Coinbase = "0x0000000000000000000000000000000000000000"
            };
        }

        private static ISignedTransaction MakeTxWithData(int seed, int dataLength)
        {
            var receiver = new byte[20];
            receiver[0] = (byte)seed;
            return new LegacyTransaction(
                nonce: new byte[] { (byte)seed }.TrimZeroBytes(),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: receiver,
                value: new byte[] { },
                data: new byte[dataLength]);
        }

        private static Receipt MakeReceiptWithLogData(int logDataLength)
        {
            var log = Log.Create(new byte[logDataLength], "0x" + new string('0', 40));
            return new Receipt
            {
                PostStateOrStatus = new byte[] { 1 },
                CumulativeGasUsed = new EvmUInt256(21000UL),
                Bloom = new byte[256],
                Logs = new List<Log> { log }
            };
        }

        private static async Task<(StorageBackedEth68Handler handler, byte[][] hashes)> PrimeBodiesAsync(
            int blockCount, int dataLengthPerTx)
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();

            var hashes = new byte[blockCount][];
            for (int n = 0; n < blockCount; n++)
            {
                var hash = HashOf(n);
                hashes[n] = hash;
                await blockStore.SaveAsync(MakeHeader(n), hash);
                var tx = MakeTxWithData(n, dataLengthPerTx);
                await txStore.SaveAsync(tx, hash, 0, n);
            }

            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);
            return (handler, hashes);
        }

        private static async Task<(StorageBackedEth68Handler handler, byte[][] hashes)> PrimeReceiptsAsync(
            int blockCount, int logDataLengthPerReceipt)
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();

            var hashes = new byte[blockCount][];
            for (int n = 0; n < blockCount; n++)
            {
                var hash = HashOf(n);
                hashes[n] = hash;
                await blockStore.SaveAsync(MakeHeader(n), hash);
                var receipt = MakeReceiptWithLogData(logDataLengthPerReceipt);
                var txHash = HashOf(1000 + n);
                await receiptStore.SaveAsync(receipt, txHash, hash, n, 0, 21000, null, 1);
            }

            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);
            return (handler, hashes);
        }


        [Fact]
        public async Task Given_CombinedBodySizeExceedsCap_When_BodiesRequested_Then_ResponseTruncated()
        {
            var (handler, hashes) = await PrimeBodiesAsync(blockCount: 6, dataLengthPerTx: 600_000);

            var bodies = await handler.GetBodiesAsync(hashes, CancellationToken.None);

            Assert.True(bodies.Count < hashes.Length,
                $"expected truncation: got {bodies.Count} of {hashes.Length} requested bodies");
            Assert.True(bodies.Count >= 1, "at least one body must always be returned when one fits");

            var totalReturnedBytes = BlockBodiesMessageEncoder.Encode(
                new BlockBodiesMessage { RequestId = 1, Bodies = bodies.ToList() }).Length;
            Assert.True(totalReturnedBytes <= ExpectedResponseSoftCapBytes,
                $"returned {totalReturnedBytes} bytes across {bodies.Count} bodies, exceeding the {ExpectedResponseSoftCapBytes}-byte cap");
        }

        [Fact]
        public async Task Given_CombinedBodySizeUnderCap_When_BodiesRequested_Then_AllBodiesReturned()
        {
            var (handler, hashes) = await PrimeBodiesAsync(blockCount: 5, dataLengthPerTx: 100);

            var bodies = await handler.GetBodiesAsync(hashes, CancellationToken.None);

            Assert.Equal(hashes.Length, bodies.Count);
        }

        [Fact]
        public async Task Given_FirstBodyAloneExceedsCap_When_BodiesRequested_Then_AtLeastOneBodyReturned()
        {
            var (handler, hashes) = await PrimeBodiesAsync(blockCount: 1, dataLengthPerTx: 3_000_000);

            var bodies = await handler.GetBodiesAsync(hashes, CancellationToken.None);

            Assert.Equal(1, bodies.Count);
        }


        [Fact]
        public async Task Given_CombinedReceiptSizeExceedsCap_When_ReceiptsRequested_Then_ResponseTruncated()
        {
            var (handler, hashes) = await PrimeReceiptsAsync(blockCount: 6, logDataLengthPerReceipt: 600_000);

            var receipts = await handler.GetReceiptsAsync(hashes, CancellationToken.None);

            Assert.True(receipts.Count < hashes.Length,
                $"expected truncation: got {receipts.Count} of {hashes.Length} requested receipt lists");
            Assert.True(receipts.Count >= 1, "at least one receipt list must always be returned when one fits");

            var totalReturnedBytes = ReceiptsMessageEncoder.Encode(
                new ReceiptsMessage { RequestId = 1, ReceiptsByBlock = receipts }).Length;
            Assert.True(totalReturnedBytes <= ExpectedResponseSoftCapBytes,
                $"returned {totalReturnedBytes} bytes across {receipts.Count} receipt lists, exceeding the {ExpectedResponseSoftCapBytes}-byte cap");
        }

        [Fact]
        public async Task Given_CombinedReceiptSizeUnderCap_When_ReceiptsRequested_Then_AllReceiptsReturned()
        {
            var (handler, hashes) = await PrimeReceiptsAsync(blockCount: 5, logDataLengthPerReceipt: 100);

            var receipts = await handler.GetReceiptsAsync(hashes, CancellationToken.None);

            Assert.Equal(hashes.Length, receipts.Count);
        }

        [Fact]
        public async Task Given_FirstReceiptListAloneExceedsCap_When_ReceiptsRequested_Then_AtLeastOneReceiptListReturned()
        {
            var (handler, hashes) = await PrimeReceiptsAsync(blockCount: 1, logDataLengthPerReceipt: 3_000_000);

            var receipts = await handler.GetReceiptsAsync(hashes, CancellationToken.None);

            Assert.Equal(1, receipts.Count);
        }


        private sealed class FakeTxPool : ITxPool
        {
            private readonly Dictionary<string, ISignedTransaction> _byHash = new();

            public void Add(byte[] hash, ISignedTransaction tx) =>
                _byHash[hash.ToHex()] = tx;

            public int PendingCount => _byHash.Count;
            public Task<int> GetPendingCountAsync() => Task.FromResult(_byHash.Count);
            public Task<byte[]> AddAsync(ISignedTransaction transaction) => throw new NotSupportedException();
            public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
            {
                _byHash.TryGetValue(txHash.ToHex(), out var tx);
                return Task.FromResult(tx);
            }
            public Task<bool> RemoveAsync(byte[] txHash) => throw new NotSupportedException();
            public Task<int> RemoveBatchAsync(IEnumerable<byte[]> txHashes) => throw new NotSupportedException();
            public Task<bool> ContainsAsync(byte[] txHash) => throw new NotSupportedException();
            public Task<IReadOnlyList<ISignedTransaction>> GetPendingAsync(int maxCount) => throw new NotSupportedException();
            public Task ClearAsync() => throw new NotSupportedException();
            public Task<BigInteger> GetPendingNonceAsync(string senderAddress, BigInteger confirmedNonce) => throw new NotSupportedException();
            public void TrackPendingNonce(string senderAddress, BigInteger nonce) { }
            public void ResetPendingNonces() { }
            public int GetSenderTxCount(string senderAddress) => 0;
            public void IncrementSenderTxCount(string senderAddress) { }
            public int MaxTxsPerSender => int.MaxValue;
        }

        [Fact]
        public async Task Given_CombinedPooledTxSizeExceedsCap_When_PooledTransactionsRequested_Then_TruncatedAtSameTwoMebibyteCap()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var txPool = new FakeTxPool();

            var hashes = new byte[6][];
            for (int i = 0; i < 6; i++)
            {
                var tx = MakeTxWithData(i, 600_000);
                var hash = HashOf(i);
                hashes[i] = hash;
                txPool.Add(hash, tx);
            }

            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore, txPool: txPool);

            var result = await handler.GetPooledTransactionsAsync(hashes, CancellationToken.None);

            Assert.True(result.Count < hashes.Length,
                $"expected truncation at the 2 MiB cap: got {result.Count} of {hashes.Length}");
            Assert.True(result.Count >= 1);

            var totalReturnedBytes = result.Sum(tx => tx.GetRLPEncoded().Length);
            Assert.True(totalReturnedBytes <= ExpectedResponseSoftCapBytes,
                $"returned {totalReturnedBytes} bytes, exceeding the {ExpectedResponseSoftCapBytes}-byte cap");
        }
    }
}
