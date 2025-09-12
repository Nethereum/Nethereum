using System.Collections.Generic;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network
{
    public interface IChainFeatureDefaultsProvider
    {
        IEnumerable<ChainFeature> GetDefaultChainFeatures();
    }
}