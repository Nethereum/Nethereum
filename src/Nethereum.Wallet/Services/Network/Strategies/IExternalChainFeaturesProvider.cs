using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network.Strategies
{
    public interface IExternalChainFeaturesProvider
    {
        Task<ChainFeature?> GetExternalChainAsync(BigInteger chainId);
        Task<IReadOnlyList<ChainFeature>> GetExternalChainsAsync(IEnumerable<BigInteger> chainIds);
        Task<bool> RefreshAsync(BigInteger chainId); // optional refresh semantics (returns true if updated)
    }
}