using Nethereum.RPC.Chain;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet
{
    public class InMemoryWalletConfigurationService : IWalletConfigurationService
    {
        private readonly Dictionary<BigInteger, ChainFeature> _chainsByChainId = new();
        private ChainFeature? _activeChain;
        
        public bool UseRandomRpcSelection { get; set; }

        public ChainFeature? ActiveChain => _activeChain;

        public ChainFeature? GetChain(BigInteger chainId)
        {
            if (_chainsByChainId.TryGetValue(chainId, out var configs))
            {
                return configs;
            }
            return null;
        }


        public Task AddOrUpdateChainAsync(ChainFeature chainFeature)
        {
            if (!_chainsByChainId.TryGetValue(chainFeature.ChainId, out var configs))
            {
                _chainsByChainId[chainFeature.ChainId] = chainFeature;
            }
            
            return Task.CompletedTask;
        }

        public Task<bool> SetActiveChainAsync(BigInteger chainId)
        {
            if (_chainsByChainId.TryGetValue(chainId, out var chainFeature))
            {
                _activeChain = chainFeature;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

}
