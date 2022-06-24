using System.Collections.Generic;

namespace Nethereum.RPC.Chain
{
    public static class ChainDefaultFeaturesServicesRepository
    {
        public static List<ChainFeature> GetDefaultChainFeatures()
        {
            return new List<ChainFeature>()
            {
                new ChainFeature(){ChainId = 1, ChainName="Mainnet", SupportEIP155 = true, SupportEIP1559 = true},
            };
        }
    }

}
