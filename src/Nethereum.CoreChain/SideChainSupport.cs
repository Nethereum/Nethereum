using System;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;

namespace Nethereum.CoreChain
{
    public sealed class SideChainSupport
    {
        public const int DefaultMaxReorgDepth = 64;

        public SideChainSupport(
            IBlockStore canonicalBlocks,
            IRewindCoordinator rewind,
            IChainMetadataStore? metadata = null,
            bool adoptByDifficulty = false,
            int maxReorgDepth = DefaultMaxReorgDepth,
            ISideBlockStore? sideBlocks = null,
            bool acknowledgeKnownCanonicalBlocks = true)
        {
            if (canonicalBlocks == null) throw new ArgumentNullException(nameof(canonicalBlocks));
            if (maxReorgDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxReorgDepth));
            Rewind = rewind ?? throw new ArgumentNullException(nameof(rewind));
            Metadata = metadata;
            MaxReorgDepth = maxReorgDepth;
            AcknowledgeKnownCanonicalBlocks = acknowledgeKnownCanonicalBlocks;
            SideBlocks = sideBlocks ?? new InMemorySideBlockStore();
            Blocks = new CanonicalOrSideBlockStore(canonicalBlocks, SideBlocks);
            ForkChoice = adoptByDifficulty ? new DifficultyForkChoice(Blocks, maxReorgDepth) : null;
        }

        public ISideBlockStore SideBlocks { get; }

        public IBlockStore Blocks { get; }

        public IRewindCoordinator Rewind { get; }

        public IChainMetadataStore? Metadata { get; }

        public IChainForkChoice? ForkChoice { get; private set; }

        public void UseForkChoice(IChainForkChoice forkChoice)
        {
            ForkChoice = forkChoice ?? throw new ArgumentNullException(nameof(forkChoice));
        }

        public int MaxReorgDepth { get; }

        public bool AcknowledgeKnownCanonicalBlocks { get; }
    }
}
