using System.Numerics;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Sync;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class CoordinatedSyncTests
    {
        [Fact]
        public async Task SyncAsync_PerformsBatchThenLiveSync()
        {
            var finalityTracker = new InMemoryFinalityTracker();
            var batchStore = new InMemoryBatchStore();
            var mockBatchSync = new MockBatchSyncService(endBlock: 99, blocksSynced: 100);
            var mockLiveSync = new MockLiveSyncService(endBlock: 120, blocksSynced: 21, finalityTracker);
            var mockAnchorService = new MockAnchorService(latestAnchoredBlock: 99);

            var config = new CoordinatedSyncConfig { AnchorCheckIntervalMs = 100000 };

            var coordinatedSync = new CoordinatedSyncService(
                config,
                mockBatchSync,
                mockLiveSync,
                finalityTracker,
                mockAnchorService,
                batchStore);

            var result = await coordinatedSync.SyncAsync();

            Assert.True(result.Success);
            Assert.Equal(1, result.BatchesSynced);
            Assert.Equal(100, result.FinalizedBlocks);
            Assert.Equal(21, result.SoftBlocks);
            Assert.Equal(99, result.FinalizedTip);
            Assert.Equal(120, result.SoftTip);

            Assert.True(mockBatchSync.SyncToLatestCalled);
            Assert.True(mockLiveSync.SyncToLatestCalled);
        }

        [Fact]
        public async Task SyncAsync_MarksBatchBlocksAsFinalized()
        {
            var finalityTracker = new InMemoryFinalityTracker();
            var batchStore = new InMemoryBatchStore();
            var mockBatchSync = new MockBatchSyncService(endBlock: 49, blocksSynced: 50);
            var mockLiveSync = new MockLiveSyncService(endBlock: 60, blocksSynced: 11, finalityTracker);
            var mockAnchorService = new MockAnchorService(latestAnchoredBlock: 49);

            var config = new CoordinatedSyncConfig();

            var coordinatedSync = new CoordinatedSyncService(
                config,
                mockBatchSync,
                mockLiveSync,
                finalityTracker,
                mockAnchorService,
                batchStore);

            await coordinatedSync.SyncAsync();

            Assert.True(await finalityTracker.IsFinalizedAsync(0));
            Assert.True(await finalityTracker.IsFinalizedAsync(49));
            Assert.False(await finalityTracker.IsFinalizedAsync(50));
            Assert.True(await finalityTracker.IsSoftAsync(50));
        }

        [Fact]
        public async Task Mode_TransitionsDuringSync()
        {
            var finalityTracker = new InMemoryFinalityTracker();
            var batchStore = new InMemoryBatchStore();
            var mockBatchSync = new MockBatchSyncService(endBlock: 99, blocksSynced: 100);
            var mockLiveSync = new MockLiveSyncService(endBlock: 120, blocksSynced: 21, finalityTracker);
            var mockAnchorService = new MockAnchorService(latestAnchoredBlock: 99);

            var config = new CoordinatedSyncConfig();

            var coordinatedSync = new CoordinatedSyncService(
                config,
                mockBatchSync,
                mockLiveSync,
                finalityTracker,
                mockAnchorService,
                batchStore);

            Assert.Equal(SyncMode.Idle, coordinatedSync.Mode);

            var modesObserved = new List<SyncMode>();
            coordinatedSync.SyncProgressChanged += (sender, args) => modesObserved.Add(args.Mode);

            await coordinatedSync.SyncAsync();

            Assert.Equal(SyncMode.Following, coordinatedSync.Mode);
        }

        [Fact]
        public async Task CheckAndHandleNewAnchorAsync_FinalizesBatchWhenNewAnchor()
        {
            var finalityTracker = new InMemoryFinalityTracker();
            await finalityTracker.MarkRangeAsFinalizedAsync(0, 99);

            var batchStore = new InMemoryBatchStore();
            await batchStore.SaveBatchAsync(new BatchInfo
            {
                FromBlock = 100,
                ToBlock = 199,
                ChainId = 420420,
                Status = BatchStatus.Imported
            });

            var mockBatchSync = new MockBatchSyncService(endBlock: 199, blocksSynced: 0);
            var mockLiveSync = new MockLiveSyncService(endBlock: 220, blocksSynced: 0);
            var mockAnchorService = new MockAnchorService(latestAnchoredBlock: 150);

            var config = new CoordinatedSyncConfig();

            var coordinatedSync = new CoordinatedSyncService(
                config,
                mockBatchSync,
                mockLiveSync,
                finalityTracker,
                mockAnchorService,
                batchStore);

            BatchFinalizedEventArgs? eventArgs = null;
            coordinatedSync.BatchFinalized += (sender, args) => eventArgs = args;

            var result = await coordinatedSync.CheckAndHandleNewAnchorAsync();

            Assert.True(result);
            Assert.NotNull(eventArgs);
            Assert.Equal(150, eventArgs!.FinalizedToBlock);
        }

        [Fact]
        public async Task CheckAndHandleNewAnchorAsync_ReturnsFalseWhenNoNewAnchor()
        {
            var finalityTracker = new InMemoryFinalityTracker();
            await finalityTracker.MarkRangeAsFinalizedAsync(0, 99);

            var batchStore = new InMemoryBatchStore();
            var mockBatchSync = new MockBatchSyncService(endBlock: 99, blocksSynced: 0);
            var mockLiveSync = new MockLiveSyncService(endBlock: 120, blocksSynced: 0);
            var mockAnchorService = new MockAnchorService(latestAnchoredBlock: 50);

            var config = new CoordinatedSyncConfig();

            var coordinatedSync = new CoordinatedSyncService(
                config,
                mockBatchSync,
                mockLiveSync,
                finalityTracker,
                mockAnchorService,
                batchStore);

            var result = await coordinatedSync.CheckAndHandleNewAnchorAsync();

            Assert.False(result);
        }

        [Fact]
        public void CoordinatedSyncConfig_Default_HasCorrectValues()
        {
            var config = CoordinatedSyncConfig.Default;

            Assert.Equal(60000, config.AnchorCheckIntervalMs);
            Assert.True(config.AutoStart);
        }
    }

    public class MockBatchSyncService : IBatchSyncService
    {
        private readonly BigInteger _endBlock;
        private readonly int _blocksSynced;

        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool SyncToLatestCalled { get; private set; }

        public BatchSyncState State { get; private set; } = BatchSyncState.Idle;
        public BigInteger LocalTip => _endBlock;
        public BigInteger AnchoredTip => _endBlock;

        public event EventHandler<BatchSyncProgressEventArgs>? Progress;
        public event EventHandler<BatchImportedEventArgs>? BatchImported;
        public event EventHandler<SyncErrorEventArgs>? Error;

        public MockBatchSyncService(BigInteger endBlock, int blocksSynced)
        {
            _endBlock = endBlock;
            _blocksSynced = blocksSynced;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public Task<BatchSyncResult> SyncToLatestAsync(CancellationToken cancellationToken = default)
        {
            SyncToLatestCalled = true;
            return Task.FromResult(new BatchSyncResult
            {
                Success = true,
                EndBlock = _endBlock,
                BlocksSynced = _blocksSynced,
                BatchesSynced = 1
            });
        }

        public Task<BatchSyncResult> SyncToBlockAsync(BigInteger targetBlock, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BatchSyncResult
            {
                Success = true,
                EndBlock = targetBlock,
                BlocksSynced = (int)targetBlock + 1
            });
        }

        public Task<BatchDownloadResult> DownloadBatchAsync(BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BatchDownloadResult { Success = true });
        }
    }

    public class MockLiveSyncService : ILiveBlockSync
    {
        private readonly BigInteger _endBlock;
        private readonly int _blocksSynced;
        private readonly IFinalityTracker? _finalityTracker;

        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool SyncToLatestCalled { get; private set; }

        public BigInteger LocalTip => _endBlock;
        public BigInteger RemoteTip => _endBlock;
        public LiveSyncState State { get; private set; } = LiveSyncState.Idle;

        public event EventHandler<LiveBlockImportedEventArgs>? BlockImported;
        public event EventHandler<LiveSyncErrorEventArgs>? Error;

        public MockLiveSyncService(BigInteger endBlock, int blocksSynced, IFinalityTracker? finalityTracker = null)
        {
            _endBlock = endBlock;
            _blocksSynced = blocksSynced;
            _finalityTracker = finalityTracker;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public async Task<LiveSyncResult> SyncToLatestAsync(CancellationToken cancellationToken = default)
        {
            SyncToLatestCalled = true;

            if (_finalityTracker != null)
            {
                await _finalityTracker.MarkAsSoftAsync(_endBlock);
            }

            return new LiveSyncResult
            {
                Success = true,
                EndBlock = _endBlock,
                BlocksSynced = _blocksSynced
            };
        }

        public Task<LiveSyncResult> SyncToBlockAsync(BigInteger targetBlock, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LiveSyncResult
            {
                Success = true,
                EndBlock = targetBlock,
                BlocksSynced = (int)targetBlock + 1
            });
        }

        public Task<BigInteger> GetRemoteTipAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_endBlock);
        }

        public Task<LiveBlockData?> FetchBlockAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<LiveBlockData?>(null);
        }
    }

    public class MockAnchorService : IAnchorService
    {
        private readonly BigInteger _latestAnchoredBlock;

        public MockAnchorService(BigInteger latestAnchoredBlock)
        {
            _latestAnchoredBlock = latestAnchoredBlock;
        }

        public Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            return Task.FromResult(new AnchorInfo
            {
                BlockNumber = blockNumber,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                Status = AnchorStatus.Confirmed
            });
        }

        public Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber)
        {
            if (blockNumber <= _latestAnchoredBlock)
            {
                return Task.FromResult<AnchorInfo?>(new AnchorInfo
                {
                    BlockNumber = blockNumber,
                    StateRoot = new byte[32],
                    TransactionsRoot = new byte[32],
                    ReceiptsRoot = new byte[32],
                    Status = AnchorStatus.Confirmed
                });
            }
            return Task.FromResult<AnchorInfo?>(null);
        }

        public Task<BigInteger> GetLatestAnchoredBlockAsync()
        {
            return Task.FromResult(_latestAnchoredBlock);
        }

        public Task<bool> VerifyAnchorAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            return Task.FromResult(blockNumber <= _latestAnchoredBlock);
        }
    }
}
