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
        public async Task ExecuteAsync(CancellationToken cancellationToken, BigInteger? startAtBlockNumberIfNotProcessed = null)
        {
            var fromBlockNumber = await GetStartBlockNumber(startAtBlockNumberIfNotProcessed);

            while (!cancellationToken.IsCancellationRequested)
            {
                var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken);
                var progress = await _blockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess);
                if (!progress.HasErrored)
                {
                    fromBlockNumber = GetNextMinimumBlockNumber(progress.BlockNumberProcessTo);
                    await UpdateLastBlockProcessed(progress.BlockNumberProcessTo);
                }
                else
                {
                    throw progress.Exception;
                }
            }
        }

        //Checks the last number in the progress repository and if bigger than the startAtBlockNumber uses that one.
        private async Task<BigInteger> GetStartBlockNumber(BigInteger? startAtBlockNumberIfNotProcessed)
        {
            var lastProcessedNumber = await _blockProgressRepository.GetLastBlockNumberProcessedAsync();

            var fromBlockNumber = GetNextMinimumBlockNumber(lastProcessedNumber);

            //Checking default start
            if (startAtBlockNumberIfNotProcessed != null && startAtBlockNumberIfNotProcessed > fromBlockNumber)
            {
                fromBlockNumber = startAtBlockNumberIfNotProcessed.Value;
            }

            return fromBlockNumber;
        }

        //Scenario I have a repository and want to start from a block number if provided (if already processed I will use the latest one) and continue until the last block number provided
        public async Task ExecuteAsync(BigInteger toBlockNumber, CancellationToken cancellationToken, BigInteger? startAtBlockNumberIfNotProcessed)
        {
            var fromBlockNumber = await GetStartBlockNumber(startAtBlockNumberIfNotProcessed);

            while (!cancellationToken.IsCancellationRequested && fromBlockNumber <= toBlockNumber)
            {
                var blockToProcess = await _lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, cancellationToken);
                if (blockToProcess > toBlockNumber) blockToProcess = toBlockNumber;

                var progress = await _blockchainProcessingOrchestrator.ProcessAsync(fromBlockNumber, blockToProcess);
                if (!progress.HasErrored)
                {
                    fromBlockNumber = GetNextMinimumBlockNumber(progress.BlockNumberProcessTo);
                    await UpdateLastBlockProcessed(progress.BlockNumberProcessTo);
                }
                else
                {
                    throw progress.Exception;
                }
            }
        }

        public BigInteger GetNextMinimumBlockNumber(BigInteger? lastProcessedNumber)
        {
            var mininumBlockNumber = lastProcessedNumber ?? 0;
            if (mininumBlockNumber > 0) mininumBlockNumber = mininumBlockNumber + 1; //start at next block
            return mininumBlockNumber;
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