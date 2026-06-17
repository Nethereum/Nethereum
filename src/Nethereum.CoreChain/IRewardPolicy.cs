using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Strategy for crediting <c>header.Coinbase</c> (and any uncle miners)
    /// at end-of-block. Injected into <see cref="BlockExecutor"/> at
    /// construction so the rewards step is data-driven per chain:
    /// <see cref="EthereumProofOfWorkRewardPolicy"/> mints PoW miner rewards
    /// for pre-Merge mainnet (Frontier → Gray Glacier); every other chain
    /// (post-Merge mainnet, AppChain sequencer, DevChain) uses
    /// <see cref="NoRewardPolicy"/>.
    /// </summary>
    public interface IRewardPolicy
    {
        /// <summary>
        /// Credit miner + uncle inclusion + uncle rewards into
        /// <paramref name="stateStore"/>. Returns the total wei credited to
        /// the block's miner so the caller can surface it on
        /// <see cref="BlockExecutionResult.MinerRewardCredited"/> for
        /// metrics / debugging. Implementations should be no-ops when the
        /// effective reward at the resolved fork is zero.
        /// </summary>
        Task<BigInteger> ApplyAsync(
            BlockHeader header,
            IList<BlockHeader> uncles,
            IStateStore stateStore,
            HardforkName fork,
            CancellationToken ct);
    }
}
