using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.BlockchainStore.EFCore.Repositories;
using Nethereum.RPC.DebugNode;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.DebugNode.Tracers;
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
            var efCoreRepo = (InternalTransactionRepository)internalTxRepo;

            var debugTrace = new DebugTraceTransaction(web3.Client);
            var tracingOptions = new TracingOptions
            {
                TracerInfo = new CallTracerInfo(onlyTopCall: false, withLog: false)
            };

            System.Func<string, Task<List<InternalTransaction>>> traceProvider = async (txHash) =>
            {
                var callTrace = await debugTrace.SendRequestAsync<CallTracerResponse>(txHash, tracingOptions)
                    .ConfigureAwait(false);

                if (callTrace == null)
                    return new List<InternalTransaction>();

                return InternalTransactionMapping.FlattenCallTrace(
                    txHash,
                    callTrace.Type,
                    callTrace.From,
                    callTrace.To,
                    callTrace.Value?.Value.ToString() ?? "0",
                    callTrace.Gas?.Value.ToString() ?? "0",
                    callTrace.GasUsed?.Value.ToString() ?? "0",
                    callTrace.Input,
                    callTrace.Output,
                    callTrace.Error,
                    ConvertCalls(callTrace.Calls),
                    revertReason: callTrace.RevertReason);
            };

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

            Func<string, string, Task> revertReasonUpdater = async (txHash, revertReason) =>
            {
                using (var ctx = _dbContextFactory.CreateContext())
                {
                    var tx = await ctx.Transactions
                        .FirstOrDefaultAsync(t => t.Hash == txHash)
                        .ConfigureAwait(false);
                    if (tx != null && string.IsNullOrEmpty(tx.RevertReason))
                    {
                        tx.RevertReason = revertReason;
                        ctx.Transactions.Update(tx);
                        await ctx.SaveChangesAsync().ConfigureAwait(false);
                    }
                }
            };

            var service = new InternalTransactionPostProcessorService();
            var processor = service.CreateProcessor(
                internalTxRepo,
                traceProvider,
                efCoreRepo.GetContractTransactionsInRangeAsync,
                progressRepo,
                lastConfirmedBlockNumberService,
                _logger,
                revertReasonUpdater);

            if (_options.ToBlock != null)
            {
                await processor.ExecuteAsync(_options.ToBlock.Value, cancellationToken, _options.FromBlock);
            }
            else
            {
                await processor.ExecuteAsync(cancellationToken, _options.FromBlock);
            }
        }

        private static List<CallTraceEntry> ConvertCalls(List<CallTracerResponse> calls)
        {
            if (calls == null) return null;
            return calls.Select(c => new CallTraceEntry
            {
                Type = c.Type,
                From = c.From,
                To = c.To,
                Value = c.Value?.Value.ToString() ?? "0",
                Gas = c.Gas?.Value.ToString() ?? "0",
                GasUsed = c.GasUsed?.Value.ToString() ?? "0",
                Input = c.Input,
                Output = c.Output,
                Error = c.Error,
                RevertReason = c.RevertReason,
                Calls = ConvertCalls(c.Calls)
            }).ToList();
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
