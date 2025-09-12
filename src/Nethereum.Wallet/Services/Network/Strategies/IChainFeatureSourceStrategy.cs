using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network.Strategies
{
    public interface IChainFeatureSourceStrategy
    {
        Task<ChainFeature?> ResolveChainAsync(BigInteger chainId);
        Task<List<ChainFeature>> GetDefaultChainsAsync();
        Task<bool> RefreshChainAsync(BigInteger chainId);
    }
}