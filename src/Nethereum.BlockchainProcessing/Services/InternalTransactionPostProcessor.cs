using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Orchestrator;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.BlockchainProcessing.Services
{
    public class TransactionToTrace
    {
        public string TransactionHash { get; set; }
        public string BlockNumber { get; set; }
        public string BlockHash { get; set; }
    }

    public class InternalTransactionOrchestrator : IBlockchainProcessingOrchestrator
    {
        private readonly IInternalTransactionRepository _repository;
        private readonly Func<string, Task<List<InternalTransaction>>> _traceProvider;
        private readonly Func<BigInteger, BigInteger, Task<List<TransactionToTrace>>> _getContractTransactionsInRange;
        private readonly Func<string, string, Task> _revertReasonUpdater;
        private readonly ILogger _log;

        public InternalTransactionOrchestrator(
            IInternalTransactionRepository repository,
            Func<string, Task<List<InternalTransaction>>> traceProvider,
            Func<BigInteger, BigInteger, Task<List<TransactionToTrace>>> getContractTransactionsInRange,
            ILogger log = null,
            Func<string, string, Task> revertReasonUpdater = null)
        {
            _repository = repository;
            _traceProvider = traceProvider;
            _getContractTransactionsInRange = getContractTransactionsInRange;
            _log = log;
            _revertReasonUpdater = revertReasonUpdater;
        }

        public async Task<OrchestrationProgress> ProcessAsync(
            BigInteger fromNumber,
            BigInteger toNumber,
            CancellationToken cancellationToken = default,
            IBlockProgressRepository blockProgressRepository = null)
        {
            var progress = new OrchestrationProgress();
            try
            {
                var transactions = await _getContractTransactionsInRange(fromNumber, toNumber).ConfigureAwait(false);

                foreach (var tx in transactions)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        var internalTxs = await _traceProvider(tx.TransactionHash).ConfigureAwait(false);
                        if (internalTxs == null || internalTxs.Count == 0) continue;

                        foreach (var itx in internalTxs)
                        {
                            itx.BlockNumber = long.Parse(tx.BlockNumber);
                            itx.BlockHash = tx.BlockHash;
                            itx.TransactionHash = tx.TransactionHash;
                            itx.IsCanonical = true;
                            itx.UpdateRowDates();
                            await _repository.UpsertAsync(itx).ConfigureAwait(false);
                        }

                        if (_revertReasonUpdater != null)
                        {
                            var topLevel = internalTxs[0];
                            if (!string.IsNullOrEmpty(topLevel.RevertReason))
                            {
                                await _revertReasonUpdater(tx.TransactionHash, topLevel.RevertReason).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
                        _log?.LogWarning("Failed to trace transaction {TxHash}: {Error}", tx.TransactionHash, ex.Message);
#else
                        _log?.LogInformation($"Failed to trace transaction {tx.TransactionHash}: {ex.Message}");
#endif
                    }
                }

                progress.BlockNumberProcessTo = toNumber;

                if (blockProgressRepository != null)
                {
                    await blockProgressRepository.UpsertProgressAsync(toNumber).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                progress.Exception = ex;
            }

            return progress;
        }
    }

    public class InternalTransactionPostProcessorService
    {
        public BlockchainProcessor CreateProcessor(
            IInternalTransactionRepository repository,
            Func<string, Task<List<InternalTransaction>>> traceProvider,
            Func<BigInteger, BigInteger, Task<List<TransactionToTrace>>> getContractTransactionsInRange,
            IBlockProgressRepository blockProgressRepository,
            ILastConfirmedBlockNumberService lastConfirmedBlockNumberService,
            ILogger log = null,
            Func<string, string, Task> revertReasonUpdater = null)
        {
            var orchestrator = new InternalTransactionOrchestrator(
                repository, traceProvider, getContractTransactionsInRange, log, revertReasonUpdater);

            return new BlockchainProcessor(
                orchestrator,
                blockProgressRepository,
                lastConfirmedBlockNumberService,
                log);
        }
    }
}
