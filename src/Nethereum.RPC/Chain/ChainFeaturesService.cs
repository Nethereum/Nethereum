using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.RPC.Chain
{

    public class ChainFeaturesService : IChainFeaturesService
    {
        private static ChainFeaturesService _current;
        public static ChainFeaturesService Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new ChainFeaturesService();
                }
                return _current;
            }
        }

        private ConcurrentDictionary<BigInteger, ChainFeature> _chainFeatures = new ConcurrentDictionary<BigInteger, ChainFeature>();
        public ChainFeaturesService()
        {
            InititialiseDefaultChainFeatures();
        }

        private void InititialiseDefaultChainFeatures()
        {
            UpsertChainFeatures(ChainDefaultFeaturesServicesRepository.GetDefaultChainFeatures());
        }

        public void UpsertChainFeatures(IEnumerable<ChainFeature> chainFeatures)
        {
            foreach (var chainFeature in chainFeatures)
            {
                UpsertChainFeature(chainFeature);
            }
        }

        public void UpsertChainFeature(ChainFeature chainFeature)
        {
            _chainFeatures.AddOrUpdate(chainFeature.ChainId, chainFeature, (k, v) => chainFeature);
        }

        public bool TryRemoveChainFeature(BigInteger chainId)
        {
            ChainFeature chainFeature;
            return _chainFeatures.TryRemove(chainId, out chainFeature);
        }

        public ChainFeature GetChainFeature(BigInteger chainId)
        {
            if (_chainFeatures.ContainsKey(chainId))
            {
                return _chainFeatures[chainId];
            }
            return null;
        }
    }

}
