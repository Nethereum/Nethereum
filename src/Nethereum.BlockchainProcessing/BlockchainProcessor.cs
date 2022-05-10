using Common.Logging;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing
{
    public class BlockchainProcessor
    {
        protected IBlockchainProcessingOrchestrator BlockchainProcessingOrchestrator { get; set; }
        private IBlockProgressRepository _blockProgressRepository;
        private ILastConfirmedBlockNumberService _lastConfirmedBlockNumberService;
        private ILog _log;

        public BlockchainProcessor(IBlockchainProcessingOrchestrator blockchainProcessingOrchestrator, IBlockProgressRepository blockProgressRepository, ILastConfirmedBlockNumberService lastConfirmedBlockNumberService, ILog log = null)
        {
            BlockchainProcessingOrchestrator = blockchainProcessingOrchestrator;
            _blockProgressRepository = blockProgressRepository;
            _lastConfirmedBlockNumberService = lastConfirmedBlockNumberService;
            _log = log;

        }

        //All scenarios have a repository (default in memory)

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until cancellation
        public async Task ExecuteAsync(CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null)
        {
            var fromBlockNumber = await GetStartBlockNumberAsync(startAtBlockNumberIfNotProcessed).ConfigureAwait(false);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken).ConfigureAwait(false);

                    //Since we're out of a possible long process, check if we have a cancellation request
                    cancellationToken.ThrowIfCancellationRequested();

                    var progress = await BlockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken, _blockProgressRepository).ConfigureAwait(false);

                    //We need to check this as if cancellation was requested prior to `progress.BlockNumberProcessTo` being set, its value would still be null causing an exception
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (!progress.HasErrored)
                        {
                            fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                            await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                        }
                        else
                        {
                            await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                            throw progress.Exception;
                        }
                    }

                }
            }
            catch (OperationCanceledException)
            {
                //If an OperationCanceledException was not triggered by the cancellation of the token, bubble it up,
                //Otherwise, return
                if (!cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }
        }

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until the last block number provided
        public async Task ExecuteAsync(BigInteger toBlockNumber, CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null)
        {
            var fromBlockNumber = await GetStartBlockNumberAsync(startAtBlockNumberIfNotProcessed).ConfigureAwait(false);

            try
            {
                while (!cancellationToken.IsCancellationRequested && fromBlockNumber <= toBlockNumber)
                {
                    var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken).ConfigureAwait(false);
                    if (blockToProcess > toBlockNumber) blockToProcess = toBlockNumber;

                    //Since we're out of a possible long process, check if we have a cancellation request
                    cancellationToken.ThrowIfCancellationRequested();

                    var progress = await BlockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken, _blockProgressRepository).ConfigureAwait(false);

                    //We need to check this as if cancellation was requested prior to `progress.BlockNumberProcessTo` being set, its value would still be null causing an exception
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (!progress.HasErrored)
                        {
                            fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                            await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                        }
                        else
                        {
                            await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                            throw progress.Exception;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //If an OperationCanceledException was not triggered by the cancellation of the token, bubble it up,
                //Otherwise, return
                if (!cancellationToken.IsCancellationRequested)
                {
                    throw;
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
                _log?.Info($"Last Block Processed: {lastBlock}");
            }
            else
            {
                _log?.Info($"No Block Processed");
            }
        }
    }
}