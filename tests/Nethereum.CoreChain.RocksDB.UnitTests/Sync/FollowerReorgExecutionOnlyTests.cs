using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class FollowerReorgExecutionOnlyTests : IDisposable
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new LegacyTransactionSigner();

        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "w2_exec_only_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
        }

        private static byte[] HashOf(ulong block, int generation)
        {
            var hash = new byte[32];
            hash[0] = (byte)(generation + 1);
            for (var i = 0; i < 8; i++) hash[1 + i] = (byte)(block >> (8 * (7 - i)));
            return hash;
        }

        private static ISignedTransaction Transfer(int nonce) =>
            TransactionFactory.CreateTransaction(
                Signer.SignTransaction(PrivateKey.HexToByteArray(), ChainId, Recipient, 100, nonce, 2_000_000_000, 21_000, ""));

        private static string LogAddress(int generation) => "0x" + new string(generation == 0 ? 'a' : 'b', 40);

        private sealed class BranchBlock
        {
            public ulong Number { get; init; }
            public int Generation { get; init; }
            public List<ISignedTransaction> Transactions { get; init; }
            public byte[] Hash => HashOf(Number, Generation);
        }

        private static BranchBlock Block(ulong number, int generation, params int[] nonces) =>
            new BranchBlock { Number = number, Generation = generation, Transactions = nonces.Select(Transfer).ToList() };

        private static BlockHeader HeaderOf(BranchBlock block, int parentGeneration)
        {
            var bloom = new LogBloomFilter();
            return new BlockHeader
            {
                ParentHash = block.Number == 0 ? new byte[32] : HashOf(block.Number - 1, parentGeneration),
                UnclesHash = new byte[32],
                Coinbase = "0x" + new string('0', 40),
                StateRoot = HashOf(block.Number, block.Generation + 10),
                TransactionsHash = PatriciaBlockRootsProvider.Instance.CalculateTransactionsRoot(block.Transactions),
                ReceiptHash = new byte[32],
                LogsBloom = bloom.Data,
                Difficulty = 0,
                BlockNumber = block.Number,
                GasLimit = 30_000_000,
                GasUsed = 0,
                Timestamp = (long)block.Number,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
        }

        private static async Task SaveHeaderAsync(IChainStoreBundle bundle, BranchBlock block, int parentGeneration)
        {
            await bundle.Blocks.SaveAsync(HeaderOf(block, parentGeneration), block.Hash);
            await bundle.Uncles.SaveAsync(block.Hash, new List<BlockHeader>());
        }

        private static async Task SaveBodyAsync(IChainStoreBundle bundle, BranchBlock block)
        {
            await bundle.Transactions.SaveManyAsync(block.Hash, block.Number, block.Transactions);
        }

        private sealed class ExecutionRecorder : IBlockExecutor
        {
            private readonly IChainStoreBundle _bundle;
            public List<(ulong Number, byte[] Hash)> Executed { get; } = new List<(ulong, byte[])>();

            public ExecutionRecorder(IChainStoreBundle bundle) => _bundle = bundle;

            public async Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
                IList<Withdrawal> withdrawals, CancellationToken ct)
            {
                var number = (ulong)header.BlockNumber;
                var hash = await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(number));
                Executed.Add((number, hash));
                var generation = hash[0] - 1;
                var bloom = new LogBloomFilter();
                var items = new List<ReceiptSaveItem>();
                var logs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>();
                for (var i = 0; i < transactions.Count; i++)
                {
                    var log = new Log { Address = LogAddress(generation), Topics = new List<byte[]> { new byte[32] }, Data = new byte[] { 1 } };
                    bloom.AddLog(log);
                    var txLogs = new List<Log> { log };
                    items.Add(new ReceiptSaveItem(Receipt.CreateStatusReceipt(true, 0, null, txLogs), transactions[i].Hash, i, 0, null, 0));
                    logs.Add((txLogs, transactions[i].Hash, i));
                }
                if (items.Count > 0)
                {
                    await _bundle.Receipts.SaveManyAsync(hash, number, items);
                    await _bundle.Logs.SaveManyLogsAsync(logs, hash, number);
                    await _bundle.Logs.SaveBlockBloomAsync(number, bloom.Data);
                }
                return new BlockImporterResult
                {
                    ComputedStateRoot = header.StateRoot,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = false,
                };
            }
        }

        private sealed class ScriptedTips : ICanonicalStateRootSource
        {
            private readonly Queue<CanonicalTip> _tips;
            private readonly Func<int, Task> _onPoll;
            private int _polls;
            public string Name => "scripted";

            public ScriptedTips(IEnumerable<CanonicalTip> tips, Func<int, Task> onPoll)
            {
                _tips = new Queue<CanonicalTip>(tips);
                _onPoll = onPoll;
            }

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[], byte[])>((null, null));

            public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            {
                await _onPoll(++_polls);
                return _tips.Count == 0 ? null : _tips.Dequeue();
            }
        }

        private sealed class CapturingLogger : ILogger
        {
            public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new ConcurrentQueue<(LogLevel, string)>();
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter) => Entries.Enqueue((logLevel, formatter(state, exception)));

            public bool Has(LogLevel level, string fragment) => Entries.Any(e => e.Level == level && e.Message.Contains(fragment));
            public string Dump() => string.Join(Environment.NewLine, Entries.Select(e => e.Level + " " + e.Message));
        }

        private sealed class NoopPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private static BackwardWalkerDelegate NeverWalks() =>
            (fromBlock, fromHash, toBlock, bundle, ct, noShortCircuitAboveBlock) =>
                throw new InvalidOperationException("the external-header-follow path must not walk");

        private static FollowerOptions Options(ulong endBlock) =>
            new FollowerOptions(
                StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0, EndBlock: endBlock,
                TipPollInterval: TimeSpan.FromMilliseconds(10), WalkerInvocationThreshold: 1UL)
            { ExternalHeaderFollow = true };

        private static async Task SeedOldBranchAsync(IChainStoreBundle bundle, ulong head, bool writeExecutedHeadOnly)
        {
            for (ulong n = 0; n <= head; n++)
            {
                var block = Block(n, 0, n == 0 ? Array.Empty<int>() : new[] { (int)n });
                await SaveHeaderAsync(bundle, block, 0);
                if (n > 0) await SaveBodyAsync(bundle, block);
            }
            for (ulong n = 1; n <= head; n++)
                await bundle.Diffs.SaveBlockDiffAsync(new BlockStateDiff { BlockNumber = n });
            bundle.Metadata.Commit(writeExecutedHeadOnly ? head : 0UL, HashOf(writeExecutedHeadOnly ? head : 0UL, 0));
        }

        private async Task<FollowerRunResult> RunAsync(
            RocksDbChainStoreBundle bundle, ExecutionRecorder executor, ICanonicalStateRootSource tips,
            FollowerOptions options, BodyRepairDelegate requeue, ILogger logger) =>
            await new FollowerService(NeverWalks(), null, requeue).RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                () => bundle,
                _ => executor,
                new NoopPolicy(),
                tips,
                options,
                new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token,
                logger);

        [Fact]
        public async Task W2_T8_Given_AnExecutionOnlyRewindOverRocksDb_When_TheClHasRelaidTheTop_Then_ItsHeadersSurviveOldBranchLookupsAreGoneAndTheBandRefillsTheBodies()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive);
            await SeedOldBranchAsync(bundle, 20, writeExecutedHeadOnly: false);

            var oldOnly = Transfer(19);
            var reincluded = Transfer(20);
            var newBranch = new[] { Block(19, 1, 20), Block(20, 1, 100), Block(21, 1) };
            var relaid = false;

            var tips = new ScriptedTips(new[]
            {
                new CanonicalTip { BlockNumber = 20, BlockHash = HashOf(20, 0) },
                new CanonicalTip { BlockNumber = 21, BlockHash = HashOf(21, 1) },
                new CanonicalTip { BlockNumber = 21, BlockHash = HashOf(21, 1) },
                new CanonicalTip { BlockNumber = 21, BlockHash = HashOf(21, 1) },
            }, async poll =>
            {
                if (poll != 2 || relaid) return;
                relaid = true;
                foreach (var block in newBranch) await SaveHeaderAsync(bundle, block, block.Number == 19 ? 0 : 1);
            });

            var window = new WindowObservation();
            using var services = new ServiceCollection().AddSingleton<IChainStoreBundle>(bundle).BuildServiceProvider();
            BodyRepairDelegate requeue = async (from, to, b, ct) =>
            {
                await window.CaptureAsync(bundle, services, oldOnly.Hash, reincluded.Hash);
                foreach (var block in newBranch.Where(x => x.Number >= from)) await SaveBodyAsync(bundle, block);
                return true;
            };

            var executor = new ExecutionRecorder(bundle);
            var logger = new CapturingLogger();
            var result = await RunAsync(bundle, executor, tips, Options(21), requeue, logger);

            Assert.True(result.ExitReason == FollowerExitReason.SourceCompleted, result.ExitReason + " " + result.Detail + Environment.NewLine + logger.Dump());
            Assert.True(window.Captured);
            Assert.Equal(HashOf(19, 1), window.HeaderHashAt19);
            Assert.Equal(HashOf(21, 1), window.HeaderHashAt21);
            Assert.Null(window.OldOnlyTransaction);
            Assert.Null(window.ReincludedTransactionBeforeRefill);
            Assert.Empty(window.LogsInRewoundRange);
            Assert.Null(window.BlockByNumberAboveExecutedHead);
            Assert.Null(window.FinalityBlockByNumberAboveExecutedHead);
            Assert.Equal(18UL, window.ExecutedHeadDuringWindow);

            Assert.Null(await bundle.Transactions.GetByHashAsync(oldOnly.Hash));
            var location = await bundle.Transactions.GetLocationAsync(reincluded.Hash);
            Assert.Equal(new BigInteger(19), location.BlockNumber);
            Assert.Equal(HashOf(19, 1), location.BlockHash);
            Assert.NotNull(await bundle.Transactions.GetByHashAsync(reincluded.Hash));
            Assert.Equal(new[] { 19UL, 20UL, 21UL }, executor.Executed.Skip(20).Select(e => e.Number).ToArray());
            var rebuilt = await bundle.Logs.GetLogsAsync(new LogFilter { FromBlock = 19, ToBlock = 21 });
            Assert.All(rebuilt, l => Assert.Equal(LogAddress(1), l.Address));
            Assert.Equal(2, rebuilt.Count);
        }

        [Fact]
        public async Task W2_T7_Given_ARestartBetweenExecutionAndTheReorg_When_TheClHasRelaidTheTopDuringTheDowntime_Then_TheAncestorComesFromThePersistedRecordAndTheRewindUsesTheJournal()
        {
            var newBranch = new[] { Block(19, 1, 20), Block(20, 1, 100), Block(21, 1) };
            using (var first = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive))
            {
                await SeedOldBranchAsync(first, 20, writeExecutedHeadOnly: false);
                var executed = await RunAsync(
                    first, new ExecutionRecorder(first),
                    new ScriptedTips(new[] { new CanonicalTip { BlockNumber = 20, BlockHash = HashOf(20, 0) } }, _ => Task.CompletedTask),
                    Options(20), (f, t, b, ct) => Task.FromResult(true), new CapturingLogger());
                Assert.Equal(FollowerExitReason.SourceCompleted, executed.ExitReason);
                Assert.Equal(20UL, first.Metadata.GetLastBlock());
            }

            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive);
            foreach (var block in newBranch) await SaveHeaderAsync(reopened, block, block.Number == 19 ? 0 : 1);
            var executor = new ExecutionRecorder(reopened);
            var logger = new CapturingLogger();
            var tips = new ScriptedTips(
                Enumerable.Repeat(new CanonicalTip { BlockNumber = 21, BlockHash = HashOf(21, 1) }, 4), _ => Task.CompletedTask);

            var result = await RunAsync(reopened, executor, tips, Options(21), async (from, to, b, ct) =>
            {
                foreach (var block in newBranch.Where(x => x.Number >= from)) await SaveBodyAsync(reopened, block);
                return true;
            }, logger);

            Assert.True(result.ExitReason == FollowerExitReason.SourceCompleted, result.ExitReason + " " + result.Detail + Environment.NewLine + logger.Dump());
            Assert.Null(result.SnapshotRestoreTarget);
            Assert.True(logger.Has(LogLevel.Information, "undone=2"), logger.Dump());
            Assert.False(logger.Has(LogLevel.Error, "reorg.disaster_recovery"));
            Assert.Equal(new[] { 19UL, 20UL, 21UL }, executor.Executed.Select(e => e.Number).ToArray());
            Assert.Equal(HashOf(21, 1), reopened.Metadata.GetLastBlockHash());
        }

        [Fact]
        public async Task W2_C4_Given_AStoreWrittenByAnOlderBuildWithNoExecutedHashRecords_When_TheClReorgsTwoBlocks_Then_TheNodeSeededItsRecordAtStartupAndRecoversThroughTheJournal()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive);
            await SeedOldBranchAsync(bundle, 20, writeExecutedHeadOnly: true);
            Assert.Null(bundle.Metadata.GetExecutedHash(10));

            var newBranch = new[] { Block(19, 1, 20), Block(20, 1, 100), Block(21, 1) };
            var tips = new ScriptedTips(new[]
            {
                new CanonicalTip { BlockNumber = 20, BlockHash = HashOf(20, 0) },
                new CanonicalTip { BlockNumber = 21, BlockHash = HashOf(21, 1) },
                new CanonicalTip { BlockNumber = 21, BlockHash = HashOf(21, 1) },
                new CanonicalTip { BlockNumber = 21, BlockHash = HashOf(21, 1) },
            }, async poll =>
            {
                if (poll != 2) return;
                foreach (var block in newBranch) await SaveHeaderAsync(bundle, block, block.Number == 19 ? 0 : 1);
            });
            var executor = new ExecutionRecorder(bundle);
            var logger = new CapturingLogger();

            var result = await RunAsync(bundle, executor, tips, Options(21), async (from, to, b, ct) =>
            {
                foreach (var block in newBranch.Where(x => x.Number >= from)) await SaveBodyAsync(bundle, block);
                return true;
            }, logger);

            Assert.True(result.ExitReason == FollowerExitReason.SourceCompleted, result.ExitReason + " " + result.Detail + Environment.NewLine + logger.Dump());
            Assert.NotNull(bundle.Metadata.GetExecutedHash(10));
            Assert.True(logger.Has(LogLevel.Information, "undone=2"), logger.Dump());
            Assert.False(logger.Has(LogLevel.Error, "reorg.disaster_recovery"));
            Assert.Equal(new[] { 19UL, 20UL, 21UL }, executor.Executed.Select(e => e.Number).ToArray());
        }

        private sealed class BoundaryFlushingExecutor : IBlockExecutor
        {
            private readonly IAtomicBlockFlush _flush;
            private readonly ulong _k;

            public BoundaryFlushingExecutor(IAtomicBlockFlush flush, ulong k)
            {
                _flush = flush;
                _k = k;
            }

            public async Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
                IList<Withdrawal> withdrawals, CancellationToken ct)
            {
                var number = (ulong)header.BlockNumber;
                if (number % _k == 0) await _flush.FlushBlockAsync(null, number, HashOf(number, 0));
                return new BlockImporterResult
                {
                    ComputedStateRoot = header.StateRoot,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = false,
                };
            }
        }

        [Fact]
        public async Task W2_K24_Given_AFollowerFlushingEvery24BlocksOver2100Blocks_When_TheRecordIsRead_Then_EveryHeightInTheLast1024HasOneAndNoneIsOlderThanHeadMinus1025()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive);
            for (ulong n = 0; n <= 2100; n++) await SaveHeaderAsync(bundle, Block(n, 0), 0);
            bundle.Metadata.Commit(0, HashOf(0, 0));

            var executor = new BoundaryFlushingExecutor(bundle, 24);
            var tips = new ScriptedTips(
                Enumerable.Repeat(new CanonicalTip { BlockNumber = 2100, BlockHash = HashOf(2100, 0) }, 3), _ => Task.CompletedTask);
            var logger = new CapturingLogger();
            var result = await new FollowerService(NeverWalks(), null, null).RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()), () => bundle, _ => executor, new NoopPolicy(), tips,
                Options(2100), new CancellationTokenSource(TimeSpan.FromSeconds(120)).Token, logger);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            Assert.True(result.ExitReason == FollowerExitReason.SourceCompleted, result.ExitReason + " " + result.Detail);
            var head = bundle.Metadata.GetLastBlock();
            Assert.Equal(2100UL, head);
            for (var n = head - 1023; n <= head; n++)
                Assert.True(bundle.Metadata.GetExecutedHash(n) != null, "missing record at " + n);
            for (ulong n = 1; n + 1025 < head; n++)
                Assert.True(bundle.Metadata.GetExecutedHash(n) == null, "leaked record at " + n);
        }

        [Fact]
        public async Task W2_K24_Given_TheClRelaysHeightsBetweenExecutionAndTheBoundary_When_TheStagedFlushWrites_Then_TheRecordHoldsTheExecutedHashesAndTheAncestorIsTheTrueDivergence()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive);
            for (ulong n = 0; n <= 48; n++) await SaveHeaderAsync(bundle, Block(n, 0), 0);
            for (ulong n = 1; n <= 48; n++) await bundle.Diffs.SaveBlockDiffAsync(new BlockStateDiff { BlockNumber = n });
            for (ulong n = 1; n <= 47; n++) bundle.Metadata.SaveExecutedHash(n, HashOf(n, 0));
            var newBranch = new[] { Block(45, 1), Block(46, 1), Block(47, 1), Block(48, 1), Block(49, 1) };
            foreach (var block in newBranch) await SaveHeaderAsync(bundle, block, block.Number == 45 ? 0 : 1);

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 48, HashOf(48, 0));
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            for (ulong n = 45; n <= 48; n++)
                Assert.Equal(HashOf(n, 0), bundle.Metadata.GetExecutedHash(n));

            var executor = new ExecutionRecorder(bundle);
            var logger = new CapturingLogger();
            var tips = new ScriptedTips(
                Enumerable.Repeat(new CanonicalTip { BlockNumber = 49, BlockHash = HashOf(49, 1) }, 4), _ => Task.CompletedTask);

            var result = await RunAsync(bundle, executor, tips, Options(49), (f, t, b, ct) => Task.FromResult(true), logger);

            Assert.True(result.ExitReason == FollowerExitReason.SourceCompleted, result.ExitReason + " " + result.Detail + Environment.NewLine + logger.Dump());
            Assert.True(logger.Has(LogLevel.Information, "undone=4"), logger.Dump());
            Assert.Equal(new[] { 45UL, 46UL, 47UL, 48UL, 49UL }, executor.Executed.Select(e => e.Number).ToArray());
        }

        [Fact]
        public async Task W2_K24_Given_ACrashBeforeTheBoundary_When_TheNodeRestartsAndReExecutes_Then_TheRecordMatchesWhatIsReExecuted()
        {
            using (var first = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive))
            {
                for (ulong n = 0; n <= 60; n++) await SaveHeaderAsync(first, Block(n, 0), 0);
                first.Metadata.Commit(0, HashOf(0, 0));
                await ((IAtomicBlockFlush)first).FlushBlockAsync(null, 48, HashOf(48, 0));
                await ((IAtomicBlockFlush)first).DrainAsync();
                for (ulong n = 49; n <= 55; n++) first.Metadata.SaveExecutedHash(n, HashOf(n, 7));
            }

            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive);
            Assert.Equal(48UL, reopened.Metadata.GetLastBlock());
            Assert.Null(reopened.Metadata.GetExecutedHash(52));

            var executor = new ExecutionRecorder(reopened);
            var tips = new ScriptedTips(
                Enumerable.Repeat(new CanonicalTip { BlockNumber = 60, BlockHash = HashOf(60, 0) }, 3), _ => Task.CompletedTask);
            var result = await RunAsync(reopened, executor, tips, Options(60), (f, t, b, ct) => Task.FromResult(true), new CapturingLogger());

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(49UL, executor.Executed[0].Number);
            for (ulong n = 49; n <= 60; n++)
                Assert.Equal(HashOf(n, 0), reopened.Metadata.GetExecutedHash(n));
        }

        private static FollowerChainNode CreateNode(RocksDbChainStoreBundle bundle)
        {
            var chainConfig = new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var hardforkConfig = chainConfig.GetHardforkConfig();
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            return new FollowerChainNode(
                bundle: bundle,
                source: new LocalReplayBlockSource(new List<BlockBundle>()),
                executorFactory: b => new ExecutionRecorder(b),
                policy: new NoopPolicy(),
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                chainConfig: chainConfig,
                hardforkConfig: hardforkConfig,
                txProcessor: new TransactionProcessor(bundle.State, bundle.Blocks, chainConfig, txVerifier, hardforkConfig),
                txVerifier: txVerifier);
        }

        private sealed class WindowObservation
        {
            public bool Captured { get; private set; }
            public byte[] HeaderHashAt19 { get; private set; }
            public byte[] HeaderHashAt21 { get; private set; }
            public ISignedTransaction OldOnlyTransaction { get; private set; }
            public ISignedTransaction ReincludedTransactionBeforeRefill { get; private set; }
            public List<FilteredLog> LogsInRewoundRange { get; private set; }
            public object BlockByNumberAboveExecutedHead { get; private set; }
            public object FinalityBlockByNumberAboveExecutedHead { get; private set; }
            public ulong ExecutedHeadDuringWindow { get; private set; }

            public async Task CaptureAsync(RocksDbChainStoreBundle bundle, IServiceProvider services, byte[] oldOnlyHash, byte[] reincludedHash)
            {
                if (Captured) return;
                Captured = true;
                HeaderHashAt19 = await bundle.Blocks.GetHashByNumberAsync(19);
                HeaderHashAt21 = await bundle.Blocks.GetHashByNumberAsync(21);
                OldOnlyTransaction = await bundle.Transactions.GetByHashAsync(oldOnlyHash);
                ReincludedTransactionBeforeRefill = await bundle.Transactions.GetByHashAsync(reincludedHash);
                LogsInRewoundRange = await bundle.Logs.GetLogsAsync(new LogFilter { FromBlock = 19, ToBlock = 20 });
                ExecutedHeadDuringWindow = bundle.Metadata.GetLastBlock();
                var node = CreateNode(bundle);
                var context = new RpcContext(node, ChainId, services);
                var response = await new EthGetBlockByNumberHandler().HandleAsync(
                    new RpcRequestMessage(1, "eth_getBlockByNumber", "0x13", false), context);
                BlockByNumberAboveExecutedHead = response.Result;
                var finality = await new FinalityLabelledEthGetBlockByNumberHandler().HandleAsync(
                    new RpcRequestMessage(2, "eth_getBlockByNumber", "0x13", false), context);
                FinalityBlockByNumberAboveExecutedHead = finality.Result;
            }
        }
    }
}
