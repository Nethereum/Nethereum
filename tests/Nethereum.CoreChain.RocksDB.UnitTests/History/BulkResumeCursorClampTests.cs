using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.History
{
    public class BulkResumeCursorClampTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"bulkclamp_{Guid.NewGuid():N}");

        public BulkResumeCursorClampTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        [Fact]
        public async Task Open_BulkMode_ClampsBothCursors_DownToBulkCheckpoint()
        {
            using (var bundle = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                await bundle.PersistBlocksAsync(new[] { MakeBlock(1), MakeBlock(2) });
                bundle.Metadata.SetLastFetchedBody(50);
                bundle.Metadata.SetLastFetchedHeader(50);
            }

            using (var reopened = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                Assert.Equal(2UL, reopened.Metadata.GetLastFetchedBody());
                Assert.Equal(2UL, reopened.Metadata.GetLastFetchedHeader());
            }
        }

        [Fact]
        public async Task Open_BulkMode_WalkerHeaderFrontier_IsNotClamped()
        {
            using (var bundle = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                await bundle.PersistBlocksAsync(new[] { MakeBlock(1), MakeBlock(2) });
                bundle.Metadata.SetLastFetchedBody(50);
                bundle.Metadata.SetLastFetchedHeader(70);
            }

            using (var reopened = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                Assert.Equal(2UL, reopened.Metadata.GetLastFetchedBody());
                Assert.Equal(70UL, reopened.Metadata.GetLastFetchedHeader());
            }
        }

        [Fact]
        public async Task Open_BulkMode_AfterExecutionStarted_LeavesCursorsAlone()
        {
            using (var bundle = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                await bundle.PersistBlocksAsync(new[] { MakeBlock(1), MakeBlock(2) });
                bundle.Metadata.SetLastFetchedBody(50);
                bundle.Metadata.Commit(10, new byte[32]);
            }

            using (var reopened = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                Assert.Equal(50UL, reopened.Metadata.GetLastFetchedBody());
            }
        }

        [Fact]
        public void Open_BulkMode_NoCheckpoint_LeavesCursorAlone()
        {
            using (var bundle = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                bundle.Metadata.SetLastFetchedBody(7);
            }

            using (var reopened = RocksDbChainStoreBundle.Open(_dir, bulkSync: true))
            {
                Assert.Equal(7UL, reopened.Metadata.GetLastFetchedBody());
            }
        }

        private static PersistableBlock MakeBlock(long n)
        {
            var txs = new List<ISignedTransaction> { MakeTx((byte)n) };
            var receipts = new List<ReceiptSaveItem>
            {
                new ReceiptSaveItem(
                    new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = new EvmUInt256(21000UL), Logs = new List<Log>() },
                    txs[0].Hash, 0, 21000, null, 1_000_000_000),
            };
            return new PersistableBlock(
                MakeHeader(n), Hash(n), new List<BlockHeader>(), null,
                txs, receipts, new List<(List<Log>, byte[], int)>(), new byte[256]);
        }

        private static byte[] Hash(long n) { var h = new byte[32]; h[0] = (byte)(0xB0 + n); return h; }

        private static ISignedTransaction MakeTx(byte seed)
        {
            var r = new byte[32]; r[0] = 1;
            var s = new byte[32]; s[0] = 1;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(seed + 1), gasPrice: new byte[] { 1 }, gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20], value: Array.Empty<byte>(), data: Array.Empty<byte>(), r: r, s: s, v: 27);
        }

        private static BlockHeader MakeHeader(long n) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)n),
            ParentHash = new byte[32],
            TransactionsHash = new byte[32],
            UnclesHash = new byte[32],
            ReceiptHash = new byte[32],
            StateRoot = new byte[32],
            Difficulty = new EvmUInt256(1UL),
            GasLimit = 1,
            Timestamp = 1,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };
    }
}
