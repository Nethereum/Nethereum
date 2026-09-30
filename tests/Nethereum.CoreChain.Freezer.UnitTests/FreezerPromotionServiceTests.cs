using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class FreezerPromotionServiceTests
    {

        private sealed class Harness : IDisposable
        {
            public string Directory { get; }
            public FreezerCore Freezer { get; }
            public FreezerCodecSet Codecs { get; }

            public Harness()
            {
                Directory = Path.Combine(Path.GetTempPath(), "freezer-promotion-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(Directory);
                Freezer = FreezerCore.Open(new FreezerLayout(Directory), FreezerOpenMode.Append);
                Codecs = new FreezerCodecSet();
            }

            public void Dispose()
            {
                Freezer.Dispose();
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }

        private sealed class FakeFinalitySource : IFinalitySource
        {
            public long FinalizedBlockNumber { get; set; }
        }

        private sealed class FakeHotBlockWindowSource : IHotBlockWindowSource
        {
            private readonly Dictionary<long, HotBlock> _blocks = new();
            public long HotTipNumber { get; private set; }

            public void Add(long number, HotBlock block)
            {
                _blocks[number] = block;
                if (number > HotTipNumber)
                    HotTipNumber = number;
            }

            public HotBlock ReadHotBlock(long blockNumber) => _blocks[blockNumber];
        }


        private static byte[] BlockHashFor(long number)
        {
            var hash = new byte[32];
            hash[0] = (byte)(number + 1);
            hash[1] = (byte)(number >> 8);
            return hash;
        }

        private static BlockHeader SyntheticHeader(long blockNumber, byte[] parentHash) => new()
        {
            ParentHash = parentHash,
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            BlockNumber = new EvmUInt256((ulong)blockNumber),
            LogsBloom = new byte[256],
            Difficulty = EvmUInt256.Zero,
            Timestamp = 0,
            GasLimit = 30_000_000,
            GasUsed = 0,
            MixHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            Nonce = new byte[8],
        };

        private static HotBlock EmptyHotBlock(long number, byte[] parentHash) =>
            new(SyntheticHeader(number, parentHash), BlockHashFor(number),
                new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null),
                new List<Receipt>(), Array.Empty<byte>());

        private static FakeHotBlockWindowSource BuildChain(int count)
        {
            var hot = new FakeHotBlockWindowSource();
            var parentHash = new byte[32];
            for (var n = 0; n < count; n++)
            {
                hot.Add(n, EmptyHotBlock(n, parentHash));
                parentHash = BlockHashFor(n);
            }
            return hot;
        }

        private static (ISignedTransaction Tx, string Sender) CreateSignedLegacyTx(ulong nonce)
        {
            var key = new EthECKey("0x" + new string('0', 63) + "1");
            var to = new byte[20];
            to[19] = 0x02;

            var rawTx = new LegacyTransaction(
                new EvmUInt256(nonce).ToBytesForRLPEncoding(),
                new EvmUInt256(1_000_000_000UL).ToBytesForRLPEncoding(),
                new EvmUInt256(21_000UL).ToBytesForRLPEncoding(),
                to,
                EvmUInt256.Zero.ToBytesForRLPEncoding(),
                Array.Empty<byte>());

            var signature = key.SignAndCalculateV(rawTx.RawHash);
            rawTx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
            return (rawTx, key.GetPublicAddress());
        }

        private static byte[] ComputeBloom(IReadOnlyList<Log> logs)
        {
            var bloom = new LogBloomFilter();
            foreach (var log in logs)
                bloom.AddLog(log);
            return bloom.Data;
        }


        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Promote finalized hot blocks into the freezer")]
        public void Given_HotBlocksUpToFinalized_When_Promote_Then_FrozenUpToFinalityOnly()
        {
            using var h = new Harness();
            var hot = BuildChain(11);
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 5 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            var result = service.PromoteFinalizedBlocks();

            Assert.Equal(6, result.PromotedCount);
            Assert.Equal(6, result.NewFreezerItems);
            Assert.Equal(6, h.Freezer.Items);
        }

        [Fact]
        public void Given_NotYetFinalizedBlock_When_Promote_Then_NotFrozen()
        {
            using var h = new Harness();
            var hot = BuildChain(11);
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 5 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            service.PromoteFinalizedBlocks();

            Assert.NotEqual(11, h.Freezer.Items);
            Assert.False(indexes.TryGetBlockNumberByHash(BlockHashFor(10), out _));
        }


        [Fact]
        public void Given_ParentNotFreezerHead_When_Promote_Then_ThrowsFreezerConsistencyException()
        {
            using var h = new Harness();
            var hot = BuildChain(6);
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 2 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            service.PromoteFinalizedBlocks();
            Assert.Equal(3, h.Freezer.Items);

            var wrongParent = new byte[32];
            wrongParent[0] = 0xFF;
            hot.Add(3, EmptyHotBlock(3, wrongParent));
            finality.FinalizedBlockNumber = 5;

            Assert.Throws<FreezerConsistencyException>(() => service.PromoteFinalizedBlocks());
        }


        [Fact]
        public void Given_FinalityRegresses_When_Promote_Then_ThrowsFreezerConsistencyException()
        {
            using var h = new Harness();
            var hot = BuildChain(11);
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 5 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            service.PromoteFinalizedBlocks();
            Assert.Equal(6, h.Freezer.Items);

            finality.FinalizedBlockNumber = 3;

            Assert.Throws<FreezerConsistencyException>(() => service.PromoteFinalizedBlocks());
            Assert.Equal(6, h.Freezer.Items);
        }


        private sealed class CommitOrderSpyIndexStore : IRandomKeyIndexStore
        {
            private readonly InMemoryRandomKeyIndexStore _inner = new();
            private readonly FreezerCore _freezer;
            private readonly long _expectedItemsAtIndexTime;

            public CommitOrderSpyIndexStore(FreezerCore freezer, long expectedItemsAtIndexTime)
            {
                _freezer = freezer;
                _expectedItemsAtIndexTime = expectedItemsAtIndexTime;
            }

            public bool TryGetBlockNumberByHash(byte[] blockHash, out long number) =>
                _inner.TryGetBlockNumberByHash(blockHash, out number);

            public bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex) =>
                _inner.TryGetTxLocation(txHash, out blockNumber, out txIndex);

            public void PutBlockHash(byte[] hash, long number)
            {
                Assert.Equal(_expectedItemsAtIndexTime, _freezer.Items);
                _inner.PutBlockHash(hash, number);
            }

            public void PutTxLocation(byte[] txHash, long blockNumber, int txIndex)
            {
                Assert.Equal(_expectedItemsAtIndexTime, _freezer.Items);
                _inner.PutTxLocation(txHash, blockNumber, txIndex);
            }

            public void RemoveBlock(long number) => _inner.RemoveBlock(number);
        }

        [Fact]
        public void Given_SuccessfulPromotion_When_Index_Then_PopulatedOnlyAfterCommit()
        {
            using var h = new Harness();
            var hot = BuildChain(4);
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 3 };
            var indexes = new CommitOrderSpyIndexStore(h.Freezer, expectedItemsAtIndexTime: 4);
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            service.PromoteFinalizedBlocks();

            Assert.True(indexes.TryGetBlockNumberByHash(BlockHashFor(0), out var block0));
            Assert.Equal(0, block0);
            Assert.True(indexes.TryGetBlockNumberByHash(BlockHashFor(3), out var block3));
            Assert.Equal(3, block3);
        }

        [Fact]
        public void Given_MidRunConsistencyFailure_When_Promote_Then_FreezerStateUnchangedAndIndexEmpty()
        {
            using var h = new Harness();
            var hot = BuildChain(4);
            var wrongParent = new byte[32];
            wrongParent[0] = 0xFF;
            hot.Add(2, EmptyHotBlock(2, wrongParent));

            var finality = new FakeFinalitySource { FinalizedBlockNumber = 3 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            var itemsBeforeCall = h.Freezer.Items;

            Assert.Throws<FreezerConsistencyException>(() => service.PromoteFinalizedBlocks());

            Assert.Equal(itemsBeforeCall, h.Freezer.Items);
            Assert.False(indexes.TryGetBlockNumberByHash(BlockHashFor(0), out _));
            Assert.False(indexes.TryGetBlockNumberByHash(BlockHashFor(1), out _));
        }


        [Fact]
        public void Given_Promote_Then_IndexPopulated()
        {
            using var h = new Harness();
            var hot = new FakeHotBlockWindowSource();
            hot.Add(0, EmptyHotBlock(0, new byte[32]));

            var (tx, _) = CreateSignedLegacyTx(nonce: 0);
            var body = new BlockBodyCluster(new List<ISignedTransaction> { tx }, new List<BlockHeader>(), null);
            var receipt = Receipt.CreateStatusReceipt(true, new EvmUInt256(21_000UL), new byte[256], new List<Log>());
            hot.Add(1, new HotBlock(SyntheticHeader(1, BlockHashFor(0)), BlockHashFor(1), body, new List<Receipt> { receipt }, Array.Empty<byte>()));

            var finality = new FakeFinalitySource { FinalizedBlockNumber = 1 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            service.PromoteFinalizedBlocks();

            Assert.True(indexes.TryGetBlockNumberByHash(BlockHashFor(0), out var block0));
            Assert.Equal(0, block0);
            Assert.True(indexes.TryGetBlockNumberByHash(BlockHashFor(1), out var block1));
            Assert.Equal(1, block1);

            Assert.True(indexes.TryGetTxLocation(tx.Hash, out var txBlockNumber, out var txIndex));
            Assert.Equal(1, txBlockNumber);
            Assert.Equal(0, txIndex);
        }


        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Strip on write, derive on read — lossless receipt round-trip")]
        public async Task Given_PromotedBlockWithTxsAndReceipts_When_ReadBackViaFreezerHistoryStore_Then_MatchesOriginal()
        {
            using var h = new Harness();

            var (tx, _) = CreateSignedLegacyTx(nonce: 0);
            var log = Log.Create(new byte[] { 0xAA, 0xBB }, "0x0000000000000000000000000000000000000003", new byte[32]);
            var logs = new List<Log> { log };
            var bloom = ComputeBloom(logs);
            var originalReceipt = Receipt.CreateStatusReceipt(true, new EvmUInt256(21_000UL), bloom, logs);

            var header = SyntheticHeader(0, new byte[32]);
            var blockHash = BlockHashFor(0);
            var body = new BlockBodyCluster(new List<ISignedTransaction> { tx }, new List<BlockHeader>(), null);
            var hotBlock = new HotBlock(header, blockHash, body, new List<Receipt> { originalReceipt }, Array.Empty<byte>());

            var hot = new FakeHotBlockWindowSource();
            hot.Add(0, hotBlock);
            var finality = new FakeFinalitySource { FinalizedBlockNumber = 0 };
            var indexes = new InMemoryRandomKeyIndexStore();
            var service = new FreezerPromotionService(h.Freezer, h.Codecs, hot, finality, indexes);

            var result = service.PromoteFinalizedBlocks();
            Assert.Equal(1, result.PromotedCount);
            Assert.Equal(1, result.NewFreezerItems);

            var signer = new TransactionVerificationAndRecoveryImp();
            var deriver = new ReceiptFieldDeriver(signer, new CancunBlobBaseFeeFractionResolver());
            var cache = new DecodedClusterCache(8);
            var store = new FreezerHistoryStore(h.Freezer, h.Codecs, deriver, signer, indexes, cache);

            var readHeader = await store.GetByNumberAsync(0);
            Assert.Equal(header.BlockNumber.ToLong(), readHeader.BlockNumber.ToLong());
            Assert.Equal(header.StateRoot, readHeader.StateRoot);
            Assert.Equal(header.ReceiptHash, readHeader.ReceiptHash);
            Assert.Equal(header.ParentHash, readHeader.ParentHash);
            Assert.Equal(header.GasLimit, readHeader.GasLimit);

            var readTxs = await store.GetByBlockNumberAsync(0);
            Assert.Single(readTxs);
            Assert.Equal(tx.Hash, readTxs[0].Hash);

            var readReceipts = await ((IReceiptStore)store).GetByBlockNumberAsync(0);
            Assert.Single(readReceipts);
            var readReceipt = readReceipts[0];

            Assert.Equal(originalReceipt.PostStateOrStatus, readReceipt.PostStateOrStatus);
            Assert.Equal(originalReceipt.CumulativeGasUsed, readReceipt.CumulativeGasUsed);
            Assert.Equal(originalReceipt.Bloom, readReceipt.Bloom);
            Assert.Equal(originalReceipt.Logs.Count, readReceipt.Logs.Count);
            Assert.Equal(originalReceipt.Logs[0].Address, readReceipt.Logs[0].Address);
            Assert.Equal(originalReceipt.Logs[0].Data, readReceipt.Logs[0].Data);
            Assert.Equal(originalReceipt.Logs[0].Topics, readReceipt.Logs[0].Topics);
        }
    }
}
