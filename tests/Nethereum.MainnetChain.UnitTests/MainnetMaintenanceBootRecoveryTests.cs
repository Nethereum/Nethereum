using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Model;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection("Sequential")]
    public class MainnetMaintenanceBootRecoveryTests : IDisposable
    {
        private const string Address = "0x8888888888888888888888888888888888888888";
        private const ulong CheckpointBlock = 10;
        private const ulong PostCheckpointBodyCursor = 99;

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"mainnet_maint_boot_{Guid.NewGuid():N}");
        private readonly RecordingLogger _logger = new();

        public MainnetMaintenanceBootRecoveryTests() => Directory.CreateDirectory(_root);

        private string DataDir => Path.Combine(_root, "data");

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
        }

        [Fact]
        public async Task Given_APendingCheckpointRestore_When_WipeStateRuns_Then_TheRestoreIsAppliedFirstAndTheNextStartDoesNotUndoTheWipe()
        {
            await SeedCheckpointThenAdvanceAsync();
            new BootRecoveryGate(NullLogger.Instance).RecordRestoreRequest(DataDir, CheckpointBlock);

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(
                new MainnetChainServerConfig { DataDir = DataDir, WipeState = true }, _logger, CancellationToken.None);

            Assert.Equal(0, exitCode);
            new BootRecoveryGate(NullLogger.Instance).ApplyPendingRestore(DataDir);
            await using var reopened = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());
            Assert.Equal(CheckpointBlock, reopened.Bundle.Metadata.GetLastFetchedBody());
            Assert.Null(await reopened.Bundle.State.GetAccountAsync(Address));
        }

        [Fact]
        public async Task Given_NoPendingCheckpointRestore_When_WipeStateRuns_Then_TheStoreIsWipedInPlaceWithoutRestoring()
        {
            await SeedCheckpointThenAdvanceAsync();

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(
                new MainnetChainServerConfig { DataDir = DataDir, WipeState = true }, _logger, CancellationToken.None);

            Assert.Equal(0, exitCode);
            await using var reopened = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());
            Assert.Equal(PostCheckpointBodyCursor, reopened.Bundle.Metadata.GetLastFetchedBody());
            Assert.Null(await reopened.Bundle.State.GetAccountAsync(Address));
        }

        [Fact]
        public async Task Given_ATornHead_When_RebuildStateFromFlatRuns_Then_TheHeadIsRepairedFirstAndTheRebuildVerifiesAtTheRecoveredHead()
        {
            await SeedCleanChainAsync(3, tearHeadAt: 4);

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(
                new MainnetChainServerConfig { DataDir = DataDir, RebuildStateFromFlat = true }, _logger, CancellationToken.None);

            Assert.Equal(0, exitCode);
            await using var reopened = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());
            Assert.Equal(3UL, reopened.Bundle.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Given_ATornHead_When_VerifyFlatRuns_Then_ItRefusesWithABootRecoveryMessageAndWritesNothing()
        {
            await SeedCleanChainAsync(3, tearHeadAt: 4);

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(
                new MainnetChainServerConfig { DataDir = DataDir, VerifyFlat = true }, _logger, CancellationToken.None);

            Assert.NotEqual(0, exitCode);
            Assert.Contains(_logger.Lines, line => line.Contains("store needs boot recovery", StringComparison.Ordinal));
            await using var reopened = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());
            Assert.Equal(4UL, reopened.Bundle.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Given_ACleanHead_When_VerifyFlatRuns_Then_ItVerifiesAndExitsZeroWithoutAskingForRecovery()
        {
            await SeedCleanChainAsync(3, tearHeadAt: null);

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(
                new MainnetChainServerConfig { DataDir = DataDir, VerifyFlat = true }, _logger, CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.DoesNotContain(_logger.Lines, line => line.Contains("store needs boot recovery", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Given_APendingCheckpointRestore_When_VerifyFlatRuns_Then_ItRefusesWithABootRecoveryMessageAndLeavesTheRestorePending()
        {
            await SeedCheckpointThenAdvanceAsync();
            var gate = new BootRecoveryGate(NullLogger.Instance);
            gate.RecordRestoreRequest(DataDir, CheckpointBlock);

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(
                new MainnetChainServerConfig { DataDir = DataDir, VerifyFlat = true }, _logger, CancellationToken.None);

            Assert.NotEqual(0, exitCode);
            Assert.Contains(_logger.Lines, line => line.Contains("store needs boot recovery", StringComparison.Ordinal));
            Assert.True(gate.HasPendingRestore(DataDir));
            await using var reopened = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());
            Assert.Equal(PostCheckpointBodyCursor, reopened.Bundle.Metadata.GetLastFetchedBody());
        }

        [Fact]
        public async Task Given_TheStoreIsHeldOpenByAnotherProcess_When_MaintenanceRuns_Then_ItLogsTheOpenFailureAndReturnsNonZero()
        {
            await using var held = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(
                new MainnetChainServerConfig { DataDir = DataDir, WipeState = true }, _logger, CancellationToken.None);

            Assert.NotEqual(0, exitCode);
            Assert.Contains(_logger.Lines, line => line.Contains("could not open", StringComparison.OrdinalIgnoreCase));
        }

        private async Task SeedCheckpointThenAdvanceAsync()
        {
            await using var storage = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());
            var bundle = storage.Bundle;
            await bundle.State.SaveAccountAsync(Address, new Account { Balance = 9, Nonce = 1 });
            bundle.Metadata.SetLastFetchedBody(CheckpointBlock);
            await bundle.SaveCheckpointAsync(CheckpointBlock, Fill(0xAA), Fill(0xBB));
            bundle.Metadata.SetLastFetchedBody(PostCheckpointBodyCursor);
        }

        private async Task SeedCleanChainAsync(ulong cleanBlocks, ulong? tearHeadAt)
        {
            await using var storage = ChainNodeStorage.Open(new MainnetChainServerConfig { DataDir = DataDir }.ToChainNodeConfig());
            var bundle = (RocksDbChainStoreBundle)storage.Bundle;
            byte[]? parentRoot = null;
            for (ulong b = 1; b <= cleanBlocks; b++)
                parentRoot = await CommitCleanBlockAsync(bundle, b, parentRoot);

            if (tearHeadAt is ulong torn)
            {
                var tornHash = Fill(0xEE);
                var tornRoot = Fill(0xFA);
                await SaveHeaderAsync(bundle.Blocks, torn, Fill((byte)(torn - 1)), tornHash, tornRoot);
                bundle.Metadata.Commit(torn, tornHash);
                Assert.False(bundle.StateTrieNodes.ContainsKey(tornRoot));
            }
        }

        private static async Task<byte[]> CommitCleanBlockAsync(RocksDbChainStoreBundle bundle, ulong blockNumber, byte[]? parentRoot)
        {
            var hist = (HistoricalStateStore)bundle.State;
            bundle.NodeCommitBlockSource?.Arm(blockNumber);
            hist.SetCurrentBlockNumber((int)blockNumber);
            await hist.SaveAccountAsync(Address, new Account { Balance = 1000 * (long)blockNumber, Nonce = (long)blockNumber });
            var root = await new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes).ComputeStateRootAsync(parentRoot);
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource?.Clear();

            var hash = Fill((byte)blockNumber);
            if (bundle.NodeCommitBlockSource != null)
                await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, blockNumber, hash);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var parentHash = blockNumber == 1 ? new byte[32] : Fill((byte)(blockNumber - 1));
            await SaveHeaderAsync(bundle.Blocks, blockNumber, parentHash, hash, root);
            bundle.Metadata.Commit(blockNumber, hash);
            bundle.Metadata.CommitDurableState(blockNumber, hash);
            Assert.True(bundle.StateTrieNodes.ContainsKey(root));
            return root;
        }

        private static Task SaveHeaderAsync(IBlockStore blocks, ulong number, byte[] parentHash, byte[] hash, byte[] stateRoot) =>
            blocks.SaveAsync(new BlockHeader
            {
                BlockNumber = number,
                ParentHash = parentHash,
                StateRoot = stateRoot,
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                MixHash = new byte[32],
                Nonce = new byte[8],
            }, hash);

        private static byte[] Fill(byte value) => Enumerable.Repeat(value, 32).ToArray();

        private sealed class RecordingLogger : ILogger
        {
            public ConcurrentQueue<string> Lines { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Lines.Enqueue($"{logLevel}: {formatter(state, exception)} {exception?.Message}");
        }
    }
}
