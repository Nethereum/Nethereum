using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sync;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Composes a block source (ISequencerRpcClient) with execution and indexing
    /// strategies into a runnable follower. Used by all 4 target compositions:
    /// mainnet archive, indexer, light client, AppChain follower.
    /// </summary>
    public class DevP2PFollowerService
    {
        private readonly ISequencerRpcClient _source;
        private readonly IExecutionStrategy _execution;
        private readonly IIndexingStrategy _indexing;
        private readonly ILogger<DevP2PFollowerService>? _logger;

        public DevP2PFollowerService(
            ISequencerRpcClient source,
            IExecutionStrategy execution,
            IIndexingStrategy indexing,
            ILogger<DevP2PFollowerService>? logger = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _execution = execution ?? throw new ArgumentNullException(nameof(execution));
            _indexing = indexing ?? throw new ArgumentNullException(nameof(indexing));
            _logger = logger;
        }

        public event EventHandler<BlockImportedEvent>? BlockImported;
        public event EventHandler<BlockSkippedEvent>? BlockSkipped;

        public BigInteger LastImportedBlock { get; private set; } = -1;

        public async Task<FollowerSyncResult> SyncRangeAsync(
            BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default)
        {
            var result = new FollowerSyncResult { StartBlock = fromBlock };
            for (var blockNumber = fromBlock; blockNumber <= toBlock; blockNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var data = await _source.GetBlockWithReceiptsAsync(blockNumber, cancellationToken);
                if (data == null)
                {
                    result.ErrorMessage = $"Block {blockNumber} not available from source";
                    result.Success = false;
                    return result;
                }

                var execResult = await _execution.ExecuteAsync(data, cancellationToken);
                if (!execResult.Success)
                {
                    BlockSkipped?.Invoke(this, new BlockSkippedEvent(blockNumber, execResult.ErrorMessage ?? "execution failed"));
                    result.ErrorMessage = $"Block {blockNumber}: {execResult.ErrorMessage}";
                    result.Success = false;
                    return result;
                }

                await _indexing.IndexAsync(data, execResult, cancellationToken);

                LastImportedBlock = blockNumber;
                result.BlocksImported++;
                result.EndBlock = blockNumber;
                BlockImported?.Invoke(this, new BlockImportedEvent(data, execResult));
            }

            result.Success = true;
            return result;
        }

        public async Task<FollowerSyncResult> SyncToTipAsync(CancellationToken cancellationToken = default)
        {
            var tip = await _source.GetBlockNumberAsync(cancellationToken);
            var from = LastImportedBlock < 0 ? BigInteger.One : LastImportedBlock + 1;
            return await SyncRangeAsync(from, tip, cancellationToken);
        }
    }

    public class FollowerSyncResult
    {
        public bool Success { get; set; }
        public BigInteger StartBlock { get; set; }
        public BigInteger EndBlock { get; set; }
        public int BlocksImported { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BlockImportedEvent : EventArgs
    {
        public LiveBlockData Block { get; }
        public ExecutionStrategyResult Execution { get; }

        public BlockImportedEvent(LiveBlockData block, ExecutionStrategyResult execution)
        {
            Block = block;
            Execution = execution;
        }
    }

    public class BlockSkippedEvent : EventArgs
    {
        public BigInteger BlockNumber { get; }
        public string Reason { get; }

        public BlockSkippedEvent(BigInteger blockNumber, string reason)
        {
            BlockNumber = blockNumber;
            Reason = reason;
        }
    }
}
