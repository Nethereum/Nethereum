using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain
{
    public class FollowerChainNode : ChainNodeBase, IAsyncDisposable, IDisposable, Services.IHistoricalProofCapable
    {
        private readonly IFollowerService _follower;
        private readonly IChainStoreBundle _bundle;
        private readonly IBlockSource _source;
        private readonly Func<IChainStoreBundle, IBlockExecutor> _executorFactory;
        private readonly IValidationPolicy _policy;
        private readonly ICanonicalStateRootSource? _canonical;
        private readonly FollowerOptions _options;
        private readonly ChainConfig _chainConfig;
        private bool _disposed;

        public FollowerChainNode(
            IChainStoreBundle bundle,
            IBlockSource source,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            FollowerOptions options,
            ChainConfig chainConfig,
            HardforkConfig hardforkConfig,
            TransactionProcessor txProcessor,
            ITransactionVerificationAndRecovery txVerifier,
            IFollowerService? follower = null,
            ICanonicalStateRootSource? canonical = null,
            IFilterStore? filterStore = null,
            IStateReader? nodeDataService = null,
            IBlobStore? blobStore = null,
            IChainActivations? activations = null,
            Func<HardforkName, HardforkConfig>? hardforkConfigFactory = null,
            ILogger? logger = null)
            : base(
                blockStore: bundle?.Blocks ?? throw new ArgumentNullException(nameof(bundle)),
                transactionStore: bundle.Transactions,
                receiptStore: bundle.Receipts,
                logStore: bundle.Logs,
                stateStore: bundle.State,
                filterStore: filterStore ?? new InMemoryFilterStore(),
                transactionProcessor: txProcessor ?? throw new ArgumentNullException(nameof(txProcessor)),
                txVerifier: txVerifier ?? throw new ArgumentNullException(nameof(txVerifier)),
                blockAccessListStore: bundle.BlockAccessLists,
                nodeDataService: nodeDataService,
                trieNodeStore: bundle.TrieNodes,
                blobStore: blobStore,
                uncleStore: bundle.Uncles,
                hardforkConfig: hardforkConfig ?? throw new ArgumentNullException(nameof(hardforkConfig)),
                activations: activations,
                hardforkConfigFactory: hardforkConfigFactory,
                logger: logger)
        {
            _bundle = bundle;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _executorFactory = executorFactory ?? throw new ArgumentNullException(nameof(executorFactory));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _chainConfig = chainConfig ?? throw new ArgumentNullException(nameof(chainConfig));
            _follower = follower ?? new FollowerService();
            _canonical = canonical;
        }

        public override ChainConfig Config => _chainConfig;

        private Services.IProofService _latestPathProofService;

        public override Services.IProofService ProofService
        {
            get
            {
                var latestStore = (_bundle as Services.ILatestProofServingBundle)?.LatestProofNodeStore;
                if (latestStore == null) return base.ProofService;
                return _latestPathProofService ??= new Services.ProofService(_bundle.State, latestStore);
            }
        }

        public Sync.ITransactionSubmissionService TransactionSubmission { get; set; }

        public override Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction transaction)
        {
            if (TransactionSubmission != null)
                return TransactionSubmission.SubmitAsync(transaction);

            return Task.FromResult(new TransactionExecutionResult
            {
                Success = false,
                RevertReason = "Mainnet follower is read-only — no transaction submission."
            });
        }

        public override Task<List<ISignedTransaction>> GetPendingTransactionsAsync()
        {
            return Task.FromResult(new List<ISignedTransaction>());
        }

        public Task<FollowerRunResult> RunAsync(CancellationToken ct, ILogger logger = null)
            => RunAsync(_options, ct, logger);

        public Task<FollowerRunResult> RunAsync(FollowerOptions options, CancellationToken ct, ILogger logger = null)
        {
            return _follower.RunAsync(
                _source,
                bundleFactory: () => _bundle,
                executorFactory: _executorFactory,
                policy: _policy,
                canonical: _canonical,
                options: options ?? _options,
                ct: ct,
                logger: logger ?? _logger);
        }

        public ChainConfig ChainConfig => _chainConfig;
        public IChainStoreBundle Bundle => _bundle;
        public IBlockSource BlockSource => _source;

        private Services.IHistoricalProofCapable HistoricalProof
            => (_bundle as Services.IHistoricalProofServingBundle)?.NodeServing;

        public bool CanServeProofAsOf(ulong blockNumber, ulong head)
            => HistoricalProof?.CanServeProofAsOf(blockNumber, head) ?? false;

        public Services.IProofService ProofServiceAsOf(ulong blockNumber)
        {
            var capability = HistoricalProof
                ?? throw new System.InvalidOperationException(
                    "This node cannot serve historical proofs (no retained node-history window). " +
                    "Guard with CanServeProofAsOfAsync before calling ProofServiceAsOf.");
            return capability.ProofServiceAsOf(blockNumber);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            if (_source is IAsyncDisposable srcAsync)
            {
                try { await srcAsync.DisposeAsync(); } catch { }
            }
            else if (_source is IDisposable srcSync)
            {
                try { srcSync.Dispose(); } catch { }
            }
            try { await _bundle.DisposeAsync(); } catch { }
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
