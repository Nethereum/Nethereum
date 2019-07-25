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
        private IBlockchainProcessingOrchestrator _blockchainProcessingOrchestrator;
        private IBlockProgressRepository _blockProgressRepository;
        private ILastConfirmedBlockNumberService _lastConfirmedBlockNumberService;
        private ILog _log;

        public BlockchainProcessor(IBlockchainProcessingOrchestrator blockchainProcessingOrchestrator, IBlockProgressRepository blockProgressRepository, ILastConfirmedBlockNumberService lastConfirmedBlockNumberService,  ILog log = null)
        {
            _blockchainProcessingOrchestrator = blockchainProcessingOrchestrator;
            _blockProgressRepository = blockProgressRepository;
            _lastConfirmedBlockNumberService = lastConfirmedBlockNumberService;
            _log = log;
            
        }

        //All scenarios have a repository (default in memory)

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until cancellation
        public async Task ExecuteAsync(CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null)
        {
            var fromBlockNumber = await GetStartBlockNumber(startAtBlockNumberIfNotProcessed);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken);
                var progress = await _blockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken);
                if (!progress.HasErrored)
                {
                    fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                    await UpdateLastBlockProcessed(progress.BlockNumberProcessTo);
                }
                else
                {
                    await UpdateLastBlockProcessed(progress.BlockNumberProcessTo);
                    throw progress.Exception;
                }
            }
        }

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until the last block number provided
        public async Task ExecuteAsync(BigInteger toBlockNumber, CancellationToken cancellationToken = default(CancellationToken), BigInteger? startAtBlockNumberIfNotProcessed = null)
        {
            var fromBlockNumber = await GetStartBlockNumber(startAtBlockNumberIfNotProcessed);

            while (!cancellationToken.IsCancellationRequested && fromBlockNumber <= toBlockNumber)
            {
                var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken);
                if (blockToProcess > toBlockNumber) blockToProcess = toBlockNumber;

                var progress = await _blockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess, cancellationToken);
                if (!progress.HasErrored)
                {
                    fromBlockNumber = progress.BlockNumberProcessTo.Value + 1;
                    await UpdateLastBlockProcessed(progress.BlockNumberProcessTo);
                }
                else
                {
                    await UpdateLastBlockProcessed(progress.BlockNumberProcessTo);
                    throw progress.Exception;
                }
            }
        }

        //Checks the last number in the progress repository and if bigger than the startAtBlockNumber uses that one.
        private async Task<BigInteger> GetStartBlockNumber(BigInteger? startAtBlockNumberIfNotProcessed)
        {
            var lastProcessedNumber = await _blockProgressRepository.GetLastBlockNumberProcessedAsync();

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

        public BigInteger GetNextMinimumBlockNumber(BigInteger? lastProcessedNumber)
        {
            return 1 + (lastProcessedNumber ?? 0);
        }

        private async Task UpdateLastBlockProcessed(BigInteger? lastBlock)
        {
            if (lastBlock != null)
            {
                await _blockProgressRepository.UpsertProgressAsync(lastBlock.Value);
                _log?.Info($"Last Block Processed: {lastBlock}");
            }
            else
            {
                _log?.Info($"No Block Processed");
            }
        }
    }
}