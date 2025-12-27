using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Catalog
{
    public class CatalogTokenListProviderAdapter : ITokenListProvider
    {
        private readonly ITokenCatalogRepository _repository;
        private readonly bool _autoSeed;

        public CatalogTokenListProviderAdapter(
            ITokenCatalogRepository repository,
            bool autoSeed = true)
        {
            _repository = repository;
            _autoSeed = autoSeed;
        }

        public async Task<List<TokenInfo>> GetTokensAsync(long chainId)
        {
            if (_autoSeed)
            {
                var isInitialized = await _repository.IsInitializedAsync(chainId).ConfigureAwait(false);
                if (!isInitialized)
                {
                    await _repository.SeedFromEmbeddedAsync(chainId).ConfigureAwait(false);
                }
            }

            var catalogTokens = await _repository.GetAllTokensAsync(chainId).ConfigureAwait(false);

            return catalogTokens
                .Select(t => t.ToTokenInfo())
                .ToList();
        }

        public async Task<TokenInfo> GetTokenAsync(long chainId, string contractAddress)
        {
            if (string.IsNullOrEmpty(contractAddress))
                return null;

            if (_autoSeed)
            {
                var isInitialized = await _repository.IsInitializedAsync(chainId).ConfigureAwait(false);
                if (!isInitialized)
                {
                    await _repository.SeedFromEmbeddedAsync(chainId).ConfigureAwait(false);
                }
            }

            var catalogToken = await _repository.GetTokenByAddressAsync(chainId, contractAddress).ConfigureAwait(false);

            return catalogToken?.ToTokenInfo();
        }

        public async Task<bool> SupportsChainAsync(long chainId)
        {
            if (_autoSeed)
            {
                var isInitialized = await _repository.IsInitializedAsync(chainId).ConfigureAwait(false);
                if (!isInitialized)
                {
                    await _repository.SeedFromEmbeddedAsync(chainId).ConfigureAwait(false);
                }
            }

            var count = await _repository.GetTokenCountAsync(chainId).ConfigureAwait(false);
            return count > 0;
        }
    }
}
