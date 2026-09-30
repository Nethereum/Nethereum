using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.AppChain.Anchoring.Postgres.Entities;
using Nethereum.AppChain.Anchoring.Postgres.Metrics;
using Nethereum.AppChain.Anchoring.Postgres.Repositories;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public class AnchorIndexProcessingService
    {
        private readonly AnchorIndexDbContext _context;
        private readonly IAnchorRecordRepository _anchorRepository;
        private readonly IChainRegistrationRepository _chainRepository;
        private readonly AnchorIndexProgressRepository _progressRepository;
        private readonly ILogger<AnchorIndexProcessingService> _logger;

        public AnchorIndexProcessingService(
            AnchorIndexDbContext context,
            IAnchorRecordRepository anchorRepository,
            IChainRegistrationRepository chainRepository,
            AnchorIndexProgressRepository progressRepository,
            ILogger<AnchorIndexProcessingService> logger)
        {
            _context = context;
            _anchorRepository = anchorRepository;
            _chainRepository = chainRepository;
            _progressRepository = progressRepository;
            _logger = logger;
        }

        public AnchorIndexingMetrics Metrics { get; set; }

        public string RpcUrl { get; set; } = "";
        public string AnchorContractAddress { get; set; } = "";
        public long StartAtBlockNumberIfNotProcessed { get; set; }
        public int NumberOfBlocksPerRequest { get; set; } = 1000;
        public int PollIntervalMs { get; set; } = 5000;
        public uint MinimumBlockConfirmations { get; set; }
        public int ReorgBuffer { get; set; }
        public List<long> ChainIdFilter { get; set; } = new();

        private HashSet<long>? _chainIdFilterSet;

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _chainIdFilterSet = ChainIdFilter.Count > 0
                ? new HashSet<long>(ChainIdFilter)
                : null;

            _logger.LogInformation(
                "Anchor indexer starting: contract={Contract}, rpc={Rpc}, chainFilter={Filter}",
                AnchorContractAddress, RpcUrl,
                _chainIdFilterSet != null ? string.Join(",", _chainIdFilterSet) : "all");

            var web3 = new Nethereum.Web3.Web3(RpcUrl);
            var retryDelay = TimeSpan.FromSeconds(5);
            var maxRetryDelay = TimeSpan.FromMinutes(5);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RunIndexingLoopAsync(web3, cancellationToken).ConfigureAwait(false);
                    retryDelay = TimeSpan.FromSeconds(5);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Metrics?.RecordError(ex.GetType().Name);
                    _logger.LogError(ex, "Anchor indexer error, retrying in {Delay}s", retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                    retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, maxRetryDelay.Ticks));
                }
            }
        }

        private async Task RunIndexingLoopAsync(Nethereum.Web3.IWeb3 web3, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var lastProcessed = await _progressRepository.GetLastProcessedBlockAsync(StartAtBlockNumberIfNotProcessed)
                    .ConfigureAwait(false);
                var fromBlock = lastProcessed + 1;

                var headBlock = (long)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()
                    .ConfigureAwait(false)).Value;
                Metrics?.SetChainHead(headBlock);
                var safeHead = headBlock - MinimumBlockConfirmations - ReorgBuffer;

                if (headBlock > 0 && lastProcessed > headBlock)
                {
                    _logger.LogWarning(
                        "Chain reorg or restart detected: lastProcessed={Last} > headBlock={Head}. Resetting index.",
                        lastProcessed, headBlock);
                    Metrics?.RecordReset();
                    await _progressRepository.ResetAllAsync().ConfigureAwait(false);
                    fromBlock = 1;
                    safeHead = headBlock;
                }

                if (fromBlock > safeHead)
                {
                    await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
                    continue;
                }

                var toBlock = Math.Min(fromBlock + NumberOfBlocksPerRequest - 1, safeHead);

                _logger.LogDebug("Indexing blocks {From}-{To} (head={Head}, lag={Lag})",
                    fromBlock, toBlock, headBlock, headBlock - toBlock);

                var sw = Stopwatch.StartNew();
                var anchorCount = await IndexAnchorSubmittedEventsAsync(web3, fromBlock, toBlock)
                    .ConfigureAwait(false);
                var chainCount = await IndexAppChainRegisteredEventsAsync(web3, fromBlock, toBlock)
                    .ConfigureAwait(false);
                sw.Stop();

                await _progressRepository.UpsertAsync(toBlock).ConfigureAwait(false);

                _context.ChangeTracker.Clear();

                Metrics?.RecordBatch(fromBlock, toBlock, anchorCount, chainCount, sw.Elapsed.TotalSeconds);

                if (anchorCount > 0 || chainCount > 0)
                    _logger.LogInformation("Blocks {From}-{To}: {Anchors} anchors, {Chains} registrations in {Elapsed:F3}s",
                        fromBlock, toBlock, anchorCount, chainCount, sw.Elapsed.TotalSeconds);

                if (toBlock >= safeHead)
                    await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
            }
        }

        private bool PassesChainFilter(long chainId)
            => _chainIdFilterSet == null || _chainIdFilterSet.Contains(chainId);

        private async Task<long> GetBlockTimestampAsync(Nethereum.Web3.IWeb3 web3, long blockNumber,
            Dictionary<long, long> cache)
        {
            if (cache.TryGetValue(blockNumber, out var cached))
                return cached;

            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber)).ConfigureAwait(false);
            var ts = block != null ? (long)block.Timestamp.Value : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cache[blockNumber] = ts;
            return ts;
        }

        private async Task<int> IndexAnchorSubmittedEventsAsync(
            Nethereum.Web3.IWeb3 web3, long fromBlock, long toBlock)
        {
            var eventHandler = web3.Eth.GetEvent<AnchorSubmittedEventDTO>(AnchorContractAddress);
            var filter = eventHandler.CreateFilterInput(
                new BlockParameter(new HexBigInteger(fromBlock)),
                new BlockParameter(new HexBigInteger(toBlock)));

            var logs = await eventHandler.GetAllChangesAsync(filter).ConfigureAwait(false);
            var count = 0;
            var tsCache = new Dictionary<long, long>();

            foreach (var log in logs)
            {
                var chainId = (long)log.Event.ChainId;
                if (!PassesChainFilter(chainId)) continue;

                var logBlockNumber = (long)log.Log.BlockNumber.Value;
                var anchor = log.Event.Anchor;
                await _anchorRepository.UpsertAsync(new AnchorRecord
                {
                    ChainId = chainId,
                    StartBlock = (long)log.Event.StartBlock,
                    EndBlock = (long)log.Event.EndBlock,
                    ProofSystem = anchor.ProofSystem,
                    AnchorVersion = anchor.AnchorVersion,
                    EndBlockHash = anchor.EndBlockHash,
                    PostStateRoot = anchor.PostStateRoot,
                    BlockHashesRoot = anchor.BlockHashesRoot,
                    ManifestHash = anchor.ManifestHash,
                    PreviousAnchorHash = anchor.PreviousAnchorHash,
                    TransactionHash = log.Log.TransactionHash,
                    MainchainBlockNumber = logBlockNumber,
                    Timestamp = await GetBlockTimestampAsync(web3, logBlockNumber, tsCache).ConfigureAwait(false)
                }).ConfigureAwait(false);
                count++;
            }

            return count;
        }

        private async Task<int> IndexAppChainRegisteredEventsAsync(
            Nethereum.Web3.IWeb3 web3, long fromBlock, long toBlock)
        {
            var eventHandler = web3.Eth.GetEvent<AppChainRegisteredEventDTO>(AnchorContractAddress);
            var filter = eventHandler.CreateFilterInput(
                new BlockParameter(new HexBigInteger(fromBlock)),
                new BlockParameter(new HexBigInteger(toBlock)));

            var logs = await eventHandler.GetAllChangesAsync(filter).ConfigureAwait(false);
            var count = 0;
            var tsCache = new Dictionary<long, long>();

            foreach (var log in logs)
            {
                var chainId = (long)log.Event.ChainId;
                if (!PassesChainFilter(chainId)) continue;

                var logBlockNumber = (long)log.Log.BlockNumber.Value;
                await _chainRepository.UpsertAsync(new ChainRegistration
                {
                    ChainId = chainId,
                    GenesisHash = log.Event.GenesisHash,
                    MinimumProofSystem = log.Event.MinimumProofSystem,
                    MinimumAnchorVersion = log.Event.MinimumAnchorVersion,
                    AuthorityAddress = log.Event.Authority,
                    TransactionHash = log.Log.TransactionHash,
                    BlockNumber = logBlockNumber,
                    Timestamp = await GetBlockTimestampAsync(web3, logBlockNumber, tsCache).ConfigureAwait(false)
                }).ConfigureAwait(false);
                count++;
            }

            return count;
        }
    }
}
