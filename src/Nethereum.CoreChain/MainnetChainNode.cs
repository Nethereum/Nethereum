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
    /// <summary>
    /// Follower-only <see cref="IChainNode"/> for any DevP2P-followed chain.
    /// Binds an <see cref="IChainStoreBundle"/> + <see cref="IBlockSource"/>
    /// + <see cref="IFollowerService"/>; never produces blocks, never holds a
    /// mempool.
    ///
    /// <para>Lifecycle: bundle-scoped. The journal-rewind path keeps the same
    /// node instance. On <see cref="FollowerExitReason.SnapshotRestoreRequested"/>
    /// the caller disposes this node, reopens the bundle, constructs a fresh
    /// node, re-invokes <see cref="RunAsync"/>.</para>
    /// </summary>
    public class MainnetChainNode : ChainNodeBase, IAsyncDisposable, IDisposable
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

        public MainnetChainNode(
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
                nodeDataService: nodeDataService,
                trieNodeStore: bundle.TrieNodes,
                blobStore: blobStore,
                hardforkConfig: hardforkConfig ?? throw new ArgumentNullException(nameof(hardforkConfig)),
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

        /// <summary>Follower-only: rejects all transaction submissions.</summary>
        public override Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction transaction)
        {
            return Task.FromResult(new TransactionExecutionResult
            {
                Success = false,
                RevertReason = "Mainnet follower is read-only — no transaction submission."
            });
        }

        /// <summary>Follower-only: no mempool, returns an empty list.</summary>
        public override Task<List<ISignedTransaction>> GetPendingTransactionsAsync()
        {
            return Task.FromResult(new List<ISignedTransaction>());
        }

        /// <summary>
        /// Drives the follower loop. Returns when the source completes, the
        /// policy issues a fatal verdict, a snapshot restore is requested, or
        /// the rewind budget is exhausted.
        /// </summary>
        public Task<FollowerRunResult> RunAsync(CancellationToken ct, ILogger logger = null)
        {
            return _follower.RunAsync(
                _source,
                bundleFactory: () => _bundle,
                executorFactory: _executorFactory,
                policy: _policy,
                canonical: _canonical,
                options: _options,
                ct: ct,
                logger: logger ?? _logger);
        }

        public ChainConfig ChainConfig => _chainConfig;
        public IChainStoreBundle Bundle => _bundle;
        public IBlockSource BlockSource => _source;

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
