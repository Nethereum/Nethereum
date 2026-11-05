using Nethereum.Documentation;
using System;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "SideChainOptions - how a stack treats blocks that do not extend the head")]
    public sealed class SideChainOptions
    {
        public bool AdoptByDifficulty { get; init; }

        public bool AcknowledgeKnownCanonicalBlocks { get; init; } = true;

        public bool PersistMetadata { get; init; } = true;
    }
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "ExecutionStackOptions - how a node role shapes its execution stack")]
    public sealed class ExecutionStackOptions
    {
        public IRewardPolicy? RewardPolicy { get; init; }

        public Func<BlockHeader, string>? AuthorResolver { get; init; }

        public bool SealedByClique { get; init; }

        public bool FeeFreeBaseFee { get; init; }

        public SideChainOptions? SideChain { get; init; }

        public IFlushCadence? FlushCadence { get; init; }

        public int PrefetchWorkers { get; init; }

        public const int DefaultStateRootWorkers = 4;

        public int StateRootWorkers { get; init; } = DefaultStateRootWorkers;

        public StatePrefetcher? Prefetcher { get; init; }

        public ITransactionVerificationAndRecovery? TransactionVerifier { get; init; }

        public ILoggerFactory? LoggerFactory { get; init; }

        public IStateStore? StateOverride { get; init; }

        public Func<IStateStore, ITrieNodeStore, IIncrementalStateRootCalculator>? StateRootCalculatorFactory { get; init; }
    }
}
