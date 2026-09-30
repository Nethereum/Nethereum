using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerHashIndexTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerhashidx_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string DataDir => Path.Combine(_root, "data");
        private string FreezerDir => Path.Combine(_root, "freezer");

        private RocksDbStorageOptions FreezerOptions() => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
        };

        private RocksDbManager OpenReaderManager()
            => new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = Path.Combine(DataDir, RocksDbChainStoreBundle.FreezerHistorySubDir) },
                CatalogueScope.FreezerHistory);

        [Fact]
        public async Task Given_FrozenBlocks_When_ResolveByBlockHash_Then_ReturnsNumber()
        {
            var blocks = MakeBlocks(0, 5);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);
                await bundle.PersistBlocksAsync(blocks);
            }

            using var manager = OpenReaderManager();
            var index = new RocksDbRandomKeyIndexStore(manager);

            foreach (var b in blocks)
            {
                Assert.True(index.TryGetBlockNumberByHash(b.Hash, out var number));
                Assert.Equal(b.Header.BlockNumber.ToBigInteger(), (BigInteger)number);
            }
        }

        [Fact]
        public async Task Given_FrozenBlockTxs_When_ResolveByTxHash_Then_ReturnsBlockAndIndex()
        {
            var blocks = MakeBlocks(0, 5);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);
                await bundle.PersistBlocksAsync(blocks);
            }

            using var manager = OpenReaderManager();
            var index = new RocksDbRandomKeyIndexStore(manager);

            foreach (var b in blocks)
            {
                var expectedNumber = (long)b.Header.BlockNumber.ToBigInteger();
                for (var i = 0; i < b.Transactions.Count; i++)
                {
                    Assert.True(index.TryGetTxLocation(b.Transactions[i].Hash, out var blockNumber, out var txIndex));
                    Assert.Equal(expectedNumber, blockNumber);
                    Assert.Equal(i, txIndex);
                }
            }
        }

        [Fact]
        public async Task Given_UnknownHash_When_Resolve_Then_False()
        {
            var blocks = MakeBlocks(0, 3);

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 100_000);
                await bundle.PersistBlocksAsync(blocks);
            }

            using var manager = OpenReaderManager();
            var index = new RocksDbRandomKeyIndexStore(manager);

            var unknownHash = Fill(0x99, 32);
            Assert.False(index.TryGetBlockNumberByHash(unknownHash, out var number));
            Assert.Equal(0, number);
            Assert.False(index.TryGetTxLocation(unknownHash, out var blockNumber, out var txIndex));
            Assert.Equal(0, blockNumber);
            Assert.Equal(0, txIndex);
        }

        [Fact]
        public async Task Given_FrozenAndUnfrozenBlocks_When_ResolveByHash_Then_BothResolveIdentically()
        {
            var blocks = MakeBlocks(0, 16);
            const long freezeBoundary = 10;

            using (var bundle = RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer))
            {
                await SetTipHeightAsync(bundle, 90_010);
                await bundle.PersistBlocksAsync(blocks);
            }

            using var frozenManager = OpenReaderManager();
            var frozenIndex = new RocksDbRandomKeyIndexStore(frozenManager);
            using var recentManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = DataDir });
            var recentIndex = new RocksDbRandomKeyIndexStore(recentManager);

            var frozen = blocks[3];
            var unfrozen = blocks[12];

            Assert.True(frozenIndex.TryGetBlockNumberByHash(frozen.Hash, out var frozenNumber));
            Assert.Equal(frozen.Header.BlockNumber.ToBigInteger(), (BigInteger)frozenNumber);

            Assert.True(recentIndex.TryGetBlockNumberByHash(unfrozen.Hash, out var unfrozenNumber));
            Assert.Equal(unfrozen.Header.BlockNumber.ToBigInteger(), (BigInteger)unfrozenNumber);

            Assert.True(frozenIndex.TryGetTxLocation(frozen.Transactions[0].Hash, out var frozenTxBlock, out var frozenTxIndex));
            Assert.Equal((long)frozen.Header.BlockNumber.ToBigInteger(), frozenTxBlock);
            Assert.Equal(0, frozenTxIndex);

            Assert.True(recentIndex.TryGetTxLocation(unfrozen.Transactions[1].Hash, out var unfrozenTxBlock, out var unfrozenTxIndex));
            Assert.Equal((long)unfrozen.Header.BlockNumber.ToBigInteger(), unfrozenTxBlock);
            Assert.Equal(1, unfrozenTxIndex);
        }

        private static async Task SetTipHeightAsync(RocksDbChainStoreBundle bundle, long height)
        {
            var hash = Fill(0xEE, 32);
            await bundle.Blocks.SaveAsync(MakeHeader(height, hash), hash);
        }

        private static List<PersistableBlock> MakeBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        private static PersistableBlock MakeBlock(long number)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var header = MakeHeader(number, hash);

            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };

            var receipts = new List<ReceiptSaveItem>();
            var logs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>();
            BigInteger cumulative = 0;
            for (var j = 0; j < txs.Count; j++)
            {
                cumulative += 21000;
                var rcptLogs = new List<Log>
                {
                    new Log { Address = "0x" + new string('a', 40), Topics = new List<byte[]> { Fill(0x11, 32) }, Data = new byte[] { (byte)j } }
                };
                var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = cumulative, Logs = rcptLogs };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[j].Hash, j, 21000, null, 1_000_000_000));
                logs.Add((rcptLogs, txs[j].Hash, j));
            }

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: txs,
                receipts: receipts,
                logs: logs,
                bloom: Fill(0x00, 256));
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
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

        private static ISignedTransaction MakeTx(long number, int index)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((number + 1) * 1000 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>(),
                r: r, s: s, v: 27);
        }

        private static byte[] Fill(byte v, int len)
        {
            var b = new byte[len];
            for (var i = 0; i < len; i++) b[i] = v;
            return b;
        }
    }
}
