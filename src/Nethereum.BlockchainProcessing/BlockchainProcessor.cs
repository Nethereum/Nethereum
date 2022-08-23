using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainProcessing
{
    public class BlockchainProcessor
    {
        protected IBlockchainProcessingOrchestrator BlockchainProcessingOrchestrator { get; set; }
        private IBlockProgressRepository _blockProgressRepository;
        private ILastConfirmedBlockNumberService _lastConfirmedBlockNumberService;
        private ILog _log;

        public BlockchainProcessor(IBlockchainProcessingOrchestrator blockchainProcessingOrchestrator, IBlockProgressRepository blockProgressRepository, ILastConfirmedBlockNumberService lastConfirmedBlockNumberService,  ILog log = null)
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
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken).ConfigureAwait(false);
                var progress = await BlockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken, _blockProgressRepository).ConfigureAwait(false);
                if (!progress.HasErrored)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                        await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    }
                    //else will stop the loop and progress has been updated already on process
                }
                else
                {
                    await UpdateLastBlockProcessedAsync(progress.BlockNumberProcessTo).ConfigureAwait(false);
                    throw progress.Exception;
                }
            }
        }

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until the last block number provided
        public async Task ExecuteAsync(BigInteger toBlockNumber, CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null)
        {
            var fromBlockNumber = await GetStartBlockNumberAsync(startAtBlockNumberIfNotProcessed).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested && fromBlockNumber <= toBlockNumber)
            {
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
                    //else will stop the loop and progress has been updated already on process
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
                _log?.Info($"Last Block Processed: {lastBlock}");
            }
            else
            {
                _log?.Info($"No Block Processed");
            }
        }
    }
}