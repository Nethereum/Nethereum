using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.Metrics;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.BlockchainProcessing.BlockProcessing;

namespace Nethereum.BlockchainProcessing
{
    public class BlockchainProcessor
    {
        public IBlockchainProcessingOrchestrator BlockchainProcessingOrchestrator { get; protected set; }
        private IBlockProgressRepository _blockProgressRepository;
        private ILastConfirmedBlockNumberService _lastConfirmedBlockNumberService;
        private ILogger _log;
        private ILogProcessingObserver _observer;

        public ILogProcessingObserver Observer
        {
            get => _observer;
            set => _observer = value;
        }

        public Func<CancellationToken, Task> ChainConsistencyValidator { get; set; }
        public Func<BigInteger, Task> ChainStateUpdater { get; set; }

        public BlockchainProcessor(IBlockchainProcessingOrchestrator blockchainProcessingOrchestrator, IBlockProgressRepository blockProgressRepository, ILastConfirmedBlockNumberService lastConfirmedBlockNumberService, ILogger log = null, ILogProcessingObserver observer = null)
        {
            BlockchainProcessingOrchestrator = blockchainProcessingOrchestrator;
            _blockProgressRepository = blockProgressRepository;
            _lastConfirmedBlockNumberService = lastConfirmedBlockNumberService;
            _log = log;
            _observer = observer;
        }

        //All scenarios have a repository (default in memory)

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until cancellation
        public async Task ExecuteAsync(CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null, int waitInterval = 0)
        {
            var fromBlockNumber = await GetStartBlockNumberAsync(startAtBlockNumberIfNotProcessed).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
				await Task.Delay(waitInterval);

				if (ChainConsistencyValidator != null)
				{
				    try
				    {
				        await ChainConsistencyValidator(cancellationToken).ConfigureAwait(false);
				    }
				    catch (ReorgDetectedException reorg)
				    {
				        await HandleReorgAsync(reorg).ConfigureAwait(false);
				        fromBlockNumber = reorg.RewindToBlockNumber;
				        continue;
				    }
				    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				    {
				        break;
				    }
				    catch (Exception ex)
				    {
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
				        _log?.LogWarning(ex, "Chain consistency validation failed, will retry next iteration.");
#else
				        _log?.LogInformation($"Chain consistency validation failed: {ex.Message}, will retry next iteration.");
#endif
				        _observer?.OnError("ConsistencyValidation:" + ex.GetType().Name);
				        continue;
				    }
				}

				var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken).ConfigureAwait(false);
                _observer?.SetChainHead(blockToProcess);
                var progress = await BlockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken, _blockProgressRepository).ConfigureAwait(false);
                if (!progress.HasErrored)
                {
                    if (progress.BlockNumberProcessTo == null)
                    {
                        continue;
                    }

                    fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                    await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    if (ChainStateUpdater != null)
                        await ChainStateUpdater(progress.BlockNumberProcessTo.Value).ConfigureAwait(false);
                }
                else
                {
                    if (progress.Exception is ReorgDetectedException reorg)
                    {
                        await HandleReorgAsync(reorg).ConfigureAwait(false);
                        fromBlockNumber = reorg.RewindToBlockNumber;
                        continue;
                    }

                    _observer?.OnError(progress.Exception?.GetType().Name ?? "unknown");
                    await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    throw progress.Exception;
                }
            }
        }

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until the last block number provided
        public async Task ExecuteAsync(BigInteger toBlockNumber, CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null, int waitInterval = 0)
        {
            var fromBlockNumber = await GetStartBlockNumberAsync(startAtBlockNumberIfNotProcessed).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested && fromBlockNumber <= toBlockNumber)
            {
				await Task.Delay(waitInterval);

				if (ChainConsistencyValidator != null)
				{
				    try
				    {
				        await ChainConsistencyValidator(cancellationToken).ConfigureAwait(false);
				    }
				    catch (ReorgDetectedException reorg)
				    {
				        await HandleReorgAsync(reorg).ConfigureAwait(false);
				        fromBlockNumber = reorg.RewindToBlockNumber;
				        continue;
				    }
				    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				    {
				        break;
				    }
				    catch (Exception ex)
				    {
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
				        _log?.LogWarning(ex, "Chain consistency validation failed, will retry next iteration.");
#else
				        _log?.LogInformation($"Chain consistency validation failed: {ex.Message}, will retry next iteration.");
#endif
				        _observer?.OnError("ConsistencyValidation:" + ex.GetType().Name);
				        continue;
				    }
				}

				var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken).ConfigureAwait(false);
                if (blockToProcess > toBlockNumber) blockToProcess = toBlockNumber;
                _observer?.SetChainHead(blockToProcess);

                var progress = await BlockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken, _blockProgressRepository).ConfigureAwait(false);
                if (!progress.HasErrored)
                {
                    if (progress.BlockNumberProcessTo == null)
                    {
                        continue;
                    }

                    fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                    await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    if (ChainStateUpdater != null)
                        await ChainStateUpdater(progress.BlockNumberProcessTo.Value).ConfigureAwait(false);
                }
                else
                {
                    if (progress.Exception is ReorgDetectedException reorg)
                    {
                        await HandleReorgAsync(reorg).ConfigureAwait(false);
                        fromBlockNumber = reorg.RewindToBlockNumber;
                        continue;
                    }

                    _observer?.OnError(progress.Exception?.GetType().Name ?? "unknown");
                    await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    throw progress.Exception;
                }
            }
        }

        //Checks the last number in the progress repository and if bigger than the startAtBlockNumber uses that one.
        private async Task<BigInteger> GetStartBlockNumberAsync(BigInteger? startAtBlockNumberIfNotProcessed)
        {
            var lastProcessedNumber = await _blockProgressRepository.GetLastBlockNumberProcessedAsync().ConfigureAwait(false);

            if(lastProcessedNumber == null) //nothing previously processed
            {
                //return requested starting point else block 0 
                return startAtBlockNumberIfNotProcessed ?? 0;
            }

            //we have previously processed - assume we want the next block
            var fromBlockNumber = lastProcessedNumber.Value + 1;

            //check that the next block is not behind what has been requested
            if (startAtBlockNumberIfNotProcessed != null && startAtBlockNumberIfNotProcessed > fromBlockNumber)
            {
                fromBlockNumber = startAtBlockNumberIfNotProcessed.Value;
            }

            return fromBlockNumber;
        }

        private async Task UpdateLastBlockProcessedAsync(BigInteger? lastBlock)
        {
            if (lastBlock != null)
            {
                await _blockProgressRepository.UpsertProgressAsync(lastBlock.Value).ConfigureAwait(false);
                _observer?.OnBlockProgressUpdated(lastBlock.Value);
                _log?.LogInformation($"Last Block Processed: {lastBlock}");
            }
            else
            {
                _log?.LogInformation($"No Block Processed");
            }
        }

        private async Task HandleReorgAsync(ReorgDetectedException reorg)
        {
            var rewindTo = reorg.RewindToBlockNumber;
            var progressValue = rewindTo > 0 ? rewindTo - 1 : 0;

            await _blockProgressRepository.UpsertProgressAsync(progressValue).ConfigureAwait(false);
            _observer?.OnReorgDetected(reorg.RewindToBlockNumber, reorg.LastCanonicalBlockNumber);
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
            _log?.LogWarning(
                "Reorg detected. Rewinding to block {RewindTo}. Last canonical #{LastCanonicalNumber} ({LastCanonicalHash}).",
                reorg.RewindToBlockNumber,
                reorg.LastCanonicalBlockNumber,
                reorg.LastCanonicalBlockHash);
#else
            _log?.LogInformation(
                $"Reorg detected. Rewinding to block {reorg.RewindToBlockNumber}. Last canonical #{reorg.LastCanonicalBlockNumber} ({reorg.LastCanonicalBlockHash}).");
#endif
        }
    }
}
