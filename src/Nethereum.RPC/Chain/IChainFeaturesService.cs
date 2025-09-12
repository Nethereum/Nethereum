using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.RPC.Chain
{
    public interface IChainFeaturesService
    {
        ChainFeature GetChainFeature(BigInteger chainId);
        void UpsertChainFeature(ChainFeature chainFeature);
        void UpsertChainFeatures(IEnumerable<ChainFeature> chainFeatures);
        bool TryRemoveChainFeature(BigInteger chainId);
    }
}