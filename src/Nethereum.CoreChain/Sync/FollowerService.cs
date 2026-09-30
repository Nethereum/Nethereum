using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    public sealed class FollowerService : IFollowerService
    {
        private readonly BackwardWalkerDelegate _walker;
        private readonly AncestorResolverDelegate _ancestorResolver;
        private readonly BodyRepairDelegate _bodyRepair;
        private readonly IChainForkChoice _forkChoice;
        private readonly IMempoolReorgReconciler _mempoolReconciler;


        public FollowerService() : this(null, null, null, null) { }

        public FollowerService(BackwardWalkerDelegate walker) : this(walker, null, null, null) { }

        public FollowerService(BackwardWalkerDelegate walker, AncestorResolverDelegate ancestorResolver)
            : this(walker, ancestorResolver, null, null) { }

        public FollowerService(
            BackwardWalkerDelegate walker,
            AncestorResolverDelegate ancestorResolver,
            BodyRepairDelegate bodyRepair)
            : this(walker, ancestorResolver, bodyRepair, null) { }

        public FollowerService(IChainForkChoice forkChoice, IMempoolReorgReconciler mempoolReconciler = null)
            : this(null, null, null, forkChoice, mempoolReconciler) { }

        public FollowerService(
            BackwardWalkerDelegate walker,
            AncestorResolverDelegate ancestorResolver,
            BodyRepairDelegate bodyRepair,
            IChainForkChoice forkChoice,
            IMempoolReorgReconciler mempoolReconciler = null)
        {
            _walker = walker;
            _ancestorResolver = ancestorResolver;
            _bodyRepair = bodyRepair;
            _forkChoice = forkChoice;
            _mempoolReconciler = mempoolReconciler;
        }

        public async Task<FollowerRunResult> RunAsync(
            IBlockSource source,
            Func<IChainStoreBundle> bundleFactory,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            CancellationToken ct,
            ILogger logger = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (bundleFactory is null) throw new ArgumentNullException(nameof(bundleFactory));
            if (executorFactory is null) throw new ArgumentNullException(nameof(executorFactory));
            if (policy is null) throw new ArgumentNullException(nameof(policy));
            if (options is null) throw new ArgumentNullException(nameof(options));
            logger ??= NullLogger.Instance;

            var bundle = bundleFactory();
            var executor = executorFactory(bundle);

            ulong lastCommittedBlock = bundle.Metadata.GetLastBlock();
            byte[] lastCommittedHash = bundle.Metadata.GetLastBlockHash() ?? new byte[32];
            ulong currentStart = options.StartBlock;

            var snapState = bundle.Metadata.GetSnapSyncState();
            if (snapState is not null && snapState.Phase == SnapPhase.Complete)
            {
                var freshLastBlock = bundle.Metadata.GetLastBlock();
                if (snapState.PivotBlockNumber > freshLastBlock)
                {
                    lastCommittedBlock = snapState.PivotBlockNumber;
                    lastCommittedHash = snapState.PivotBlockHash ?? lastCommittedHash;
                    if (currentStart <= snapState.PivotBlockNumber)
                    {
                        currentStart = snapState.PivotBlockNumber + 1;
                    }
                    logger.LogInformation(
                        "FollowerService: snap pivot detected at block {Pivot}; treating as executed and starting at {Start}",
                        snapState.PivotBlockNumber, currentStart);
                }
                else if (freshLastBlock > lastCommittedBlock)
                {
                    lastCommittedBlock = freshLastBlock;
                    lastCommittedHash = bundle.Metadata.GetLastBlockHash() ?? lastCommittedHash;
                }
            }

            if ((_walker != null || options.ExternalHeaderFollow) && canonical != null)
            {
                logger.LogInformation(
                    "follower.path active=tip-driven walker_wired={WalkerWired} external_header_follow={External} canonical_wired=true",
                    _walker != null, options.ExternalHeaderFollow);
                return await new TipFollowerEngine(_walker, _ancestorResolver, _bodyRepair).RunAsync(
                    bundle, executor, executorFactory, canonical, options,
                    lastCommittedBlock, lastCommittedHash,
                    ct, logger).ConfigureAwait(false);
            }

            logger.LogInformation(
                "follower.path active=legacy-stream walker_null={WalkerNull} canonical_null={CanonicalNull}",
                _walker == null, canonical == null);

            return await new StreamFollowerEngine().RunAsync(
                source, bundle, executor, executorFactory, policy, canonical, options,
                lastCommittedBlock, lastCommittedHash, currentStart, ct, logger,
                _forkChoice, _mempoolReconciler).ConfigureAwait(false);
        }








    }
}
