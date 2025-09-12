using Nethereum.RPC.Chain;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet
{
    public interface IWalletConfigurationService
    {
        ChainFeature? ActiveChain { get; }
        Task AddOrUpdateChainAsync(ChainFeature chainFeature);
        ChainFeature? GetChain(BigInteger chainId);
        Task<bool> SetActiveChainAsync(BigInteger chainId);
        Task<List<ChainFeature>> GetAvailableChainsAsync();
    }

}
