using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PersistBuildParallelismTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public void BuildPersistableBlock_ParallelMap_MatchesSequential_AndIsStable()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new StubScheduler(), new StubPool(), new StubWorker(), bundle);

            var tasks = BuildDeliveredTasks(blockCount: 64, txsPerBlock: 4);

            var sequential = tasks.Select(t => backfiller.BuildPersistableBlock(t, BigInteger.MinusOne)).ToArray();

            for (int run = 0; run < 50; run++)
            {
                var parallel = new PersistableBlock[tasks.Count];
                Parallel.For(0, tasks.Count, i => parallel[i] = backfiller.BuildPersistableBlock(tasks[i], BigInteger.MinusOne));
                for (int i = 0; i < tasks.Count; i++)
                    AssertSameRows(sequential[i], parallel[i]);
            }
        }

        [Fact]
        public void BuildPersistableBlock_NothingFreezeEligible_DerivesFullReceiptRows()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new StubScheduler(), new StubPool(), new StubWorker(), bundle);
            var task = BuildDeliveredTasks(blockCount: 1, txsPerBlock: 3)[0];

            var pb = backfiller.BuildPersistableBlock(task, BigInteger.MinusOne);

            Assert.NotNull(pb.Bloom);
            Assert.NotNull(pb.Logs);
            for (int i = 0; i < pb.Receipts.Count; i++)
            {
                Assert.NotNull(pb.Receipts[i].TxHash);
                Assert.Equal(task.Body.Transactions[i].Hash, pb.Receipts[i].TxHash);
                Assert.NotEqual(BigInteger.Zero, pb.Receipts[i].GasUsed);
            }
        }

        [Fact]
        public void BuildPersistableBlock_FreezeEligible_StripsDerivedReceiptFields_KeepingRawReceipt()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new StubScheduler(), new StubPool(), new StubWorker(), bundle);
            var task = BuildDeliveredTasks(blockCount: 1, txsPerBlock: 3)[0];

            var pb = backfiller.BuildPersistableBlock(task, freezeBoundary: 10);

            Assert.Null(pb.Bloom);
            Assert.Null(pb.Logs);
            Assert.Equal(task.Receipts.Count, pb.Receipts.Count);
            for (int i = 0; i < pb.Receipts.Count; i++)
            {
                Assert.Null(pb.Receipts[i].TxHash);
                Assert.Equal(BigInteger.Zero, pb.Receipts[i].GasUsed);
                Assert.Null(pb.Receipts[i].ContractAddress);
                Assert.Equal(BigInteger.Zero, pb.Receipts[i].EffectiveGasPrice);
                Assert.Same(task.Receipts[i], pb.Receipts[i].Receipt);
            }
        }

        private static void AssertSameRows(PersistableBlock a, PersistableBlock b)
        {
            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal(a.Transactions?.Count ?? 0, b.Transactions?.Count ?? 0);
            Assert.Equal(a.Bloom, b.Bloom);

            var ra = a.Receipts;
            var rb = b.Receipts;
            Assert.Equal(ra?.Count ?? 0, rb?.Count ?? 0);
            if (ra != null)
                for (int i = 0; i < ra.Count; i++)
                {
                    Assert.Equal(ra[i].TxHash, rb[i].TxHash);
                    Assert.Equal(ra[i].TxIndex, rb[i].TxIndex);
                    Assert.Equal(ra[i].GasUsed, rb[i].GasUsed);
                    Assert.Equal(ra[i].ContractAddress, rb[i].ContractAddress);
                    Assert.Equal(ra[i].EffectiveGasPrice, rb[i].EffectiveGasPrice);
                }
        }

        private static List<BlockTaskQueue.BlockTask> BuildDeliveredTasks(int blockCount, int txsPerBlock)
        {
            var keccak = new Sha3Keccack();
            var tasks = new List<BlockTaskQueue.BlockTask>(blockCount);
            byte[] prevHash = new byte[32];

            for (int n = 0; n < blockCount; n++)
            {
                var txs = new List<ISignedTransaction>(txsPerBlock);
                var rcpts = new List<Receipt>(txsPerBlock);
                BigInteger cumulative = 0;
                for (int i = 0; i < txsPerBlock; i++)
                {
                    txs.Add(MakeTx(n, i));
                    cumulative += 21000;
                    rcpts.Add(new Receipt { CumulativeGasUsed = new EvmUInt256((ulong)cumulative) });
                }

                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prevHash.Clone(),
                    TransactionsHash = new byte[32],
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
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

                var hash = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                prevHash = hash;

                tasks.Add(new BlockTaskQueue.BlockTask
                {
                    Header = header,
                    Hash = hash,
                    BlockNumber = (ulong)n,
                    Body = new BlockBody { Transactions = txs, Uncles = new List<BlockHeader>() },
                    Receipts = rcpts,
                });
            }
            return tasks;
        }

        private static ISignedTransaction MakeTx(int blockN, int i)
        {
            var receiver = new byte[20];
            receiver[0] = (byte)blockN;
            receiver[1] = (byte)i;
            return new LegacyTransaction(
                nonce: new byte[] { (byte)(i + 1) }.TrimZeroBytes(),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: receiver,
                value: new byte[] { },
                data: new byte[0]);
        }

        private sealed class StubPool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public bool IsPeerActive(Guid id) => false;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class StubWorker : IPeerRequestWorker
        {
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class StubScheduler : IFetchRequestScheduler
        {
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
