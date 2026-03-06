using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.BlockProcessing;
using Nethereum.BlockchainProcessing.BlockStorage;
using Nethereum.BlockchainProcessing.Metrics;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainStorage.Processors
{
    public class BlockchainProcessingService
    {
        private readonly ILogger<BlockchainProcessingService> _logger;
        private readonly IBlockchainDbContextFactory _dbContextFactory;
        private readonly BlockchainProcessingOptions _options;
        private readonly ILogProcessingObserver _observer;
        private readonly INonCanonicalTokenTransferLogRepository _nonCanonicalTokenTransferLogRepository;
        private readonly ITokenBalanceRepository _tokenBalanceRepository;
        private readonly INFTInventoryRepository _nftInventoryRepository;

        public BlockchainProcessingService(
            ILogger<BlockchainProcessingService> logger,
            IBlockchainDbContextFactory dbContextFactory,
            IOptions<BlockchainProcessingOptions> options,
            ILogProcessingObserver observer = null,
            INonCanonicalTokenTransferLogRepository nonCanonicalTokenTransferLogRepository = null,
            ITokenBalanceRepository tokenBalanceRepository = null,
            INFTInventoryRepository nftInventoryRepository = null)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;
            _options = options.Value;
            _observer = observer;
            _nonCanonicalTokenTransferLogRepository = nonCanonicalTokenTransferLogRepository;
            _tokenBalanceRepository = tokenBalanceRepository;
            _nftInventoryRepository = nftInventoryRepository;

            if (_nonCanonicalTokenTransferLogRepository == null)
            {
                _logger.LogWarning("INonCanonicalTokenTransferLogRepository not registered. Token transfer logs will NOT be marked non-canonical during reorgs.");
            }
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.BlockchainUrl))
            {
                throw new InvalidOperationException("Missing BlockchainUrl configuration value.");
            }

            var web3 = new Web3.Web3(_options.BlockchainUrl);
            var repoFactory = new BlockchainStoreRepositoryFactory(_dbContextFactory);
            await ChainStateValidationService
                .EnsureChainIdMatchesAsync(web3.Eth, repoFactory, cancellationToken)
                .ConfigureAwait(false);

            var steps = new BlockStorageProcessingSteps(repoFactory);

            var orchestrator = new BlockCrawlOrchestrator(web3.Eth, steps);
            orchestrator.ContractCreatedCrawlerStep.RetrieveCode = true;
            orchestrator.ChainStateRepository = repoFactory.CreateChainStateRepository();
            var blockRepository = repoFactory.CreateBlockRepository();
            var transactionRepository = repoFactory.CreateTransactionRepository();
            var transactionLogRepository = repoFactory.CreateTransactionLogRepository();
            orchestrator.NonCanonicalBlockRepository = blockRepository as INonCanonicalBlockRepository;
            orchestrator.NonCanonicalTransactionRepository = transactionRepository as INonCanonicalTransactionRepository;
            orchestrator.NonCanonicalTransactionLogRepository = transactionLogRepository as INonCanonicalTransactionLogRepository;

            var internalTransactionRepository = repoFactory.CreateInternalTransactionRepository();
            orchestrator.NonCanonicalInternalTransactionRepository = internalTransactionRepository as INonCanonicalInternalTransactionRepository;

            orchestrator.ReorgHandler = repoFactory.CreateReorgHandler();
            orchestrator.NonCanonicalTokenTransferLogRepository = _nonCanonicalTokenTransferLogRepository;
            orchestrator.TokenBalanceRepository = _tokenBalanceRepository;
            orchestrator.NFTInventoryRepository = _nftInventoryRepository;

            orchestrator.ReorgBuffer = _options.ReorgBuffer;
            orchestrator.UseBatchReceipts = _options.UseBatchReceipts;

            var lastConfirmedBlockNumberService = new LastConfirmedBlockNumberService(
                web3.Eth.Blocks.GetBlockNumber,
                _options.MinimumBlockConfirmations ?? LastConfirmedBlockNumberService.DEFAULT_BLOCK_CONFIRMATIONS,
                _logger);

            IBlockProgressRepository progressRepo = repoFactory.CreateBlockProgressRepository();

            var processor = new BlockchainProcessor(
                orchestrator,
                progressRepo,
                lastConfirmedBlockNumberService,
                _logger,
                _observer);

            processor.ChainConsistencyValidator = orchestrator.ValidateChainConsistencyAsync;

            if (_options.ToBlock != null)
            {
                await processor.ExecuteAsync(_options.ToBlock.Value, cancellationToken, _options.FromBlock);
            }
            else
            {
                await processor.ExecuteAsync(cancellationToken, _options.FromBlock);
            }
        }
    }
}
