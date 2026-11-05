using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Nethereum.CoreChain.Prefetch;
using Nethereum.Documentation;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.CoreChain
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "IIncrementalStateRootCalculator - the pluggable state-root seam")]
    public interface IIncrementalStateRootCalculator
    {
        Task<byte[]> ComputeStateRootAsync();

        Task<byte[]> ComputeStateRootAsync(byte[] previousStateRoot);

        Task<byte[]> ComputeStateRootWithoutPersistAsync(byte[] previousStateRoot);

        Task PersistPendingStateAsync();

        void DiscardPendingState();

        Task<byte[]> ComputeFullStateRootAsync();

        ITrieNodeStore NodeStore => null;

        BlockTrieSet BeginBlockTries(byte[] parentRoot) => null;

    }
}
