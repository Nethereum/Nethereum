using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainStorage.Processors
{
    public class InternalTransactionProcessingService
    {
        private readonly ILogger<InternalTransactionProcessingService> _logger;
        private readonly IBlockchainDbContextFactory _dbContextFactory;
        private readonly BlockchainProcessingOptions _options;

        public InternalTransactionProcessingService(
            ILogger<InternalTransactionProcessingService> logger,
            IBlockchainDbContextFactory dbContextFactory,
            IOptions<BlockchainProcessingOptions> options)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;
            _options = options.Value;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.BlockchainUrl))
            {
                throw new System.InvalidOperationException("Missing BlockchainUrl configuration value.");
            }

            var web3 = new Web3.Web3(_options.BlockchainUrl);
            var repoFactory = new BlockchainStoreRepositoryFactory(_dbContextFactory);

            var internalTxRepo = repoFactory.CreateInternalTransactionRepository();
            var transactionRepo = repoFactory.CreateTransactionRepository();

            IInternalTransactionSource source;
            if (_options.UseLocalEvmReplayForInternalTransactions)
            {
                var hardforkConfig = Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance
                    .Get(Nethereum.EVM.HardforkNames.Parse(_options.Hardfork));
                source = new EvmReplayInternalTransactionSource(web3.Eth, hardforkConfig);
                _logger.LogInformation("Internal transaction processor using local EVM replay (hardfork: {Hardfork}).", _options.Hardfork);
            }
            else
            {
                source = new DebugTraceInternalTransactionSource(web3.Client);
            }

            IBlockProgressRepository rawInternalProgressRepo = repoFactory.CreateInternalTransactionBlockProgressRepository();
            IBlockProgressRepository progressRepo = rawInternalProgressRepo;
            IBlockProgressRepository mainBlockProgressRepo = repoFactory.CreateBlockProgressRepository();

            var reorgBuffer = _options.ReorgBuffer;
            if (reorgBuffer > 0)
            {
                progressRepo = new ReorgBufferedBlockProgressRepository(progressRepo, reorgBuffer);
                _logger.LogInformation("Internal transaction processor using reorg buffer of {Buffer} blocks.", reorgBuffer);
            }

            var chainBlockNumberService = new LastConfirmedBlockNumberService(
                web3.Eth.Blocks.GetBlockNumber,
                _options.MinimumBlockConfirmations ?? LastConfirmedBlockNumberService.DEFAULT_BLOCK_CONFIRMATIONS,
                _logger);

            var lastConfirmedBlockNumberService = new BlockProgressCappedLastConfirmedBlockNumberService(
                chainBlockNumberService, mainBlockProgressRepo, rawInternalProgressRepo, _logger);

            var service = new InternalTransactionPostProcessorService();
            var processor = service.CreateProcessor(
                internalTxRepo,
                source.ProduceAsync,
                internalTxRepo.GetContractTransactionsInRangeAsync,
                progressRepo,
                lastConfirmedBlockNumberService,
                _logger,
                transactionRepo.UpdateRevertReasonAsync);

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

    internal class BlockProgressCappedLastConfirmedBlockNumberService : ILastConfirmedBlockNumberService
    {
        private readonly ILastConfirmedBlockNumberService _inner;
        private readonly IBlockProgressRepository _mainBlockProgress;
        private readonly IBlockProgressRepository _internalProgressRepository;
        private readonly ILogger _logger;

        public BlockProgressCappedLastConfirmedBlockNumberService(
            ILastConfirmedBlockNumberService inner,
            IBlockProgressRepository mainBlockProgress,
            IBlockProgressRepository internalProgressRepository = null,
            ILogger logger = null)
        {
            _inner = inner;
            _mainBlockProgress = mainBlockProgress;
            _internalProgressRepository = internalProgressRepository;
            _logger = logger;
        }

        public async Task<BigInteger> GetLastConfirmedBlockNumberAsync(
            BigInteger? waitForConfirmedBlockNumber, CancellationToken cancellationToken)
        {
            var chainBlock = await _inner.GetLastConfirmedBlockNumberAsync(
                waitForConfirmedBlockNumber, cancellationToken).ConfigureAwait(false);

            var mainProgress = await _mainBlockProgress.GetLastBlockNumberProcessedAsync()
                .ConfigureAwait(false);

            if (mainProgress == null || mainProgress.Value < 0)
                return BigInteger.Zero;

            if (_internalProgressRepository != null)
            {
                var internalProgress = await _internalProgressRepository.GetLastBlockNumberProcessedAsync()
                    .ConfigureAwait(false);

                if (internalProgress != null && mainProgress.Value < internalProgress.Value)
                {
                    _logger?.LogWarning(
                        "Main processor rewound to block {MainProgress} but internal TX processor was at {InternalProgress}. Rewinding internal progress to match.",
                        mainProgress.Value, internalProgress.Value);
                    await _internalProgressRepository.UpsertProgressAsync(mainProgress.Value)
                        .ConfigureAwait(false);
                }
            }

            var capped = BigInteger.Min(chainBlock, mainProgress.Value);
            return capped;
        }
    }
}
