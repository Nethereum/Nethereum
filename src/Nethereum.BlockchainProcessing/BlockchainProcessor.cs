using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainProcessing
{
    public class BlockchainProcessor
    {
        public IBlockchainProcessingOrchestrator BlockchainProcessingOrchestrator { get; protected set; }
        private IBlockProgressRepository _blockProgressRepository;
        private ILastConfirmedBlockNumberService _lastConfirmedBlockNumberService;
        private ILogger _log;

        public BlockchainProcessor(IBlockchainProcessingOrchestrator blockchainProcessingOrchestrator, IBlockProgressRepository blockProgressRepository, ILastConfirmedBlockNumberService lastConfirmedBlockNumberService,  ILogger log = null)
        {
            BlockchainProcessingOrchestrator = blockchainProcessingOrchestrator;
            _blockProgressRepository = blockProgressRepository;
            _lastConfirmedBlockNumberService = lastConfirmedBlockNumberService;
            _log = log;
            
        }

        //All scenarios have a repository (default in memory)

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until cancellation
        public async Task ExecuteAsync(CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null, int waitInterval = 0)
        {
            var fromBlockNumber = await GetStartBlockNumberAsync(startAtBlockNumberIfNotProcessed).ConfigureAwait(false);
            
            while (!cancellationToken.IsCancellationRequested)
            {
				await Task.Delay(waitInterval);
				var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken).ConfigureAwait(false);
                var progress = await BlockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken, _blockProgressRepository).ConfigureAwait(false);
                if (!progress.HasErrored)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                        await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    }
                    else
                    {
                        //updating as other implementations might not have updated internally
                        await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    }
                   
                }
                else
                {
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
				var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken).ConfigureAwait(false);
                if (blockToProcess > toBlockNumber) blockToProcess = toBlockNumber;

                var progress = await BlockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken, _blockProgressRepository).ConfigureAwait(false);
                if (!progress.HasErrored)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                        await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    }
                    else
                    {
                        //updating as other implementations might not have updated internally
                        await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    }
                    }
                else
                {
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
                _log?.LogInformation($"Last Block Processed: {lastBlock}");
            }
            else
            {
                _log?.LogInformation($"No Block Processed");
            }
        }
    }
}