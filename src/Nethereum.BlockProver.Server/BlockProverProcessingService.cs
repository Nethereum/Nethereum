using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.BlockProver.Server.Metrics;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;

namespace Nethereum.BlockProver.Server
{
    public class BlockProverProcessingService
    {
        private readonly ILogger<BlockProverProcessingService> _logger;
        private readonly IWitnessStore _witnessStore;
        private readonly IBlockProver _prover;
        private readonly IBlockProgressRepository _progressRepository;
        private readonly IProofRequestQueue _requestQueue;
        private readonly BlockProverOptions _options;
        private readonly ProofCadence _cadence;
        private readonly WitnessRetentionPolicy? _retention;
        private readonly BlockProverMetrics? _metrics;

        public BlockProverProcessingService(
            ILogger<BlockProverProcessingService> logger,
            IWitnessStore witnessStore,
            IBlockProver prover,
            IBlockProgressRepository progressRepository,
            IProofRequestQueue requestQueue,
            IOptions<BlockProverOptions> options,
            ProofCadence? cadence = null,
            WitnessRetentionPolicy? retention = null,
            BlockProverMetrics? metrics = null)
        {
            _logger = logger;
            _witnessStore = witnessStore;
            _prover = prover;
            _progressRepository = progressRepository;
            _requestQueue = requestQueue;
            _options = options.Value;
            _cadence = cadence ?? ProofCadence.Continuous;
            _retention = retention;
            _metrics = metrics;
        }

        public long LastProvenBlock { get; private set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Block prover service is disabled");
                return;
            }

            var lastProcessed = await _progressRepository.GetLastBlockNumberProcessedAsync();
            if (lastProcessed.HasValue)
                LastProvenBlock = (long)lastProcessed.Value;

            _logger.LogInformation("Block prover starting, last proven block: {BlockNumber}", LastProvenBlock);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var processed = await ProcessNextAsync(cancellationToken);

                    if (!processed)
                        await Task.Delay(_options.PollIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in prove loop, will retry");
                    await Task.Delay(_options.PollIntervalMs, cancellationToken);
                }
            }

            _logger.LogInformation("Block prover stopping, last proven block: {BlockNumber}", LastProvenBlock);
        }

        private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
        {
            var pending = await _requestQueue.GetPendingAsync();
            _metrics?.UpdateQueueDepth(pending.Count);

            var request = await _requestQueue.DequeueAsync();
            if (request != null)
            {
                var success = await ProveBlockWithRetryAsync(request.BlockNumber, cancellationToken);
                if (success)
                    await _requestQueue.CompleteAsync(request.BlockNumber);
                else
                    await _requestQueue.FailAsync(request.BlockNumber,
                        $"Failed after {_options.MaxRetries} attempts");
                return success;
            }

            return await AutoEnqueueFromCadenceAsync(cancellationToken);
        }

        private async Task<bool> AutoEnqueueFromCadenceAsync(CancellationToken cancellationToken)
        {
            if (_cadence.Mode == ProofCadenceMode.Off || _cadence.Mode == ProofCadenceMode.OnDemand)
                return false;

            var unproven = await _witnessStore.GetUnprovenBlockNumbersAsync();
            if (unproven.Count == 0) return false;

            var anyProcessed = false;
            foreach (var blockNumber in unproven)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (!_cadence.ShouldProve((long)blockNumber))
                    continue;

                var existing = await _requestQueue.GetStatusAsync((long)blockNumber);
                if (existing != null && existing.Status == ProofRequestStatus.Failed)
                    continue;

                await _requestQueue.EnqueueAsync((long)blockNumber);
                var request = await _requestQueue.DequeueAsync();
                if (request == null) continue;

                var success = await ProveBlockWithRetryAsync(request.BlockNumber, cancellationToken);
                if (success)
                {
                    await _requestQueue.CompleteAsync(request.BlockNumber);
                    anyProcessed = true;
                }
                else
                {
                    await _requestQueue.FailAsync(request.BlockNumber,
                        $"Failed after {_options.MaxRetries} attempts");
                }
            }

            return anyProcessed;
        }

        private async Task<bool> ProveBlockWithRetryAsync(long blockNumber, CancellationToken cancellationToken)
        {
            var witness = await _witnessStore.GetWitnessAsync(blockNumber);
            if (witness == null || witness.Length == 0)
            {
                _logger.LogWarning("No witness for block {BlockNumber}, skipping", blockNumber);
                return false;
            }

            var sw = Stopwatch.StartNew();

            for (int attempt = 0; attempt < _options.MaxRetries; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                try
                {
                    _logger.LogInformation("Proving block {BlockNumber} (attempt {Attempt})",
                        blockNumber, attempt + 1);

                    var proof = await _prover.ProveBlockAsync(
                        witness, null, null, blockNumber);

                    if (proof.ProofBytes == null || proof.ProofBytes.Length == 0)
                    {
                        _logger.LogError("Block {BlockNumber}: proof bytes are null/empty. Mode={Mode}",
                            blockNumber, proof.ProverMode);
                        _metrics?.RecordProofFailed(blockNumber, "empty proof");
                        return false;
                    }

                    if (proof.ProverComputedStateRoot != null && !proof.StateRootVerified)
                    {
                        _logger.LogError("Block {BlockNumber}: state root MISMATCH. Prover={ProverRoot}, Expected={Expected}",
                            blockNumber,
                            Convert.ToHexString(proof.ProverComputedStateRoot),
                            proof.PostStateRoot != null ? Convert.ToHexString(proof.PostStateRoot) : "null");
                        _metrics?.RecordProofFailed(blockNumber, "state root mismatch");
                        return false;
                    }

                    if (proof.ProverComputedBlockHash != null && !proof.BlockHashVerified)
                    {
                        _logger.LogError("Block {BlockNumber}: block hash NOT VERIFIED. ProverHash={Hash}",
                            blockNumber, Convert.ToHexString(proof.ProverComputedBlockHash));
                        _metrics?.RecordProofFailed(blockNumber, "block hash mismatch");
                        return false;
                    }

                    sw.Stop();
                    await _witnessStore.StoreProofAsync(blockNumber, proof);
                    await _progressRepository.UpsertProgressAsync(blockNumber);
                    LastProvenBlock = blockNumber;

                    var elfHashShort = proof.ElfHash != null
                        ? Convert.ToHexString(proof.ElfHash).Substring(0, 16) : null;

                    _logger.LogInformation("Block {BlockNumber} proved in {Duration:F2}s, mode={Mode}, elfHash={ElfHash}",
                        blockNumber, sw.Elapsed.TotalSeconds, proof.ProverMode, elfHashShort ?? "none");

                    _metrics?.RecordProofCompleted(blockNumber, sw.Elapsed.TotalSeconds,
                        proof.ProverMode, elfHashShort);

                    if (_retention != null)
                        await _witnessStore.PurgeWitnessesAsync(_retention, blockNumber);

                    return true;
                }
                catch (Exception ex)
                {
                    _metrics?.RecordRetry(blockNumber, attempt + 1);
                    _logger.LogWarning(ex, "Prove attempt {Attempt} failed for block {BlockNumber}",
                        attempt + 1, blockNumber);

                    if (attempt < _options.MaxRetries - 1)
                    {
                        var delay = _options.RetryDelayMs * (int)Math.Pow(2, attempt);
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            sw.Stop();
            _metrics?.RecordProofFailed(blockNumber, "max retries exceeded");
            _logger.LogError("Failed to prove block {BlockNumber} after {MaxRetries} attempts",
                blockNumber, _options.MaxRetries);
            return false;
        }

        public async Task RequestProofAsync(long blockNumber)
        {
            await _requestQueue.EnqueueAsync(blockNumber);
            _logger.LogInformation("Proof requested for block {BlockNumber}", blockNumber);
        }

        public async Task<ProofRequest?> GetRequestStatusAsync(long blockNumber)
        {
            return await _requestQueue.GetStatusAsync(blockNumber);
        }
    }
}
