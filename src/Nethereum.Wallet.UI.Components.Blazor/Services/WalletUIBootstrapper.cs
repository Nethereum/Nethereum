using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Tokens;
using Nethereum.Wallet.UI.Components.Blazor.Extensions;
using Nethereum.Wallet.UI.Components.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public sealed class WalletUIBootstrapper
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IWalletUIRegistryContributor> _registryContributors;
        private bool _initialized;
        private static bool _tokenCacheInitialized;

        public WalletUIBootstrapper(
            IServiceProvider serviceProvider,
            IEnumerable<IWalletUIRegistryContributor>? registryContributors = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _registryContributors = registryContributors ?? Array.Empty<IWalletUIRegistryContributor>();
        }

        public void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            // Populate account creation/details registries within the current scope.
            _serviceProvider.InitializeAccountTypes();
            foreach (var contributor in _registryContributors)
            {
                contributor.Configure(_serviceProvider);
            }

            // Start background token cache preloading (fire-and-forget)
            if (!_tokenCacheInitialized)
            {
                _tokenCacheInitialized = true;
                _ = PreloadTokenCacheAsync();
            }

            _initialized = true;
        }

        private async Task PreloadTokenCacheAsync()
        {
            try
            {
                var tokenService = _serviceProvider.GetService<ITokenManagementService>();
                if (tokenService == null) return;

                var chainService = _serviceProvider.GetService<IChainManagementService>();
                if (chainService != null)
                {
                    var allChains = await chainService.GetAllChainsAsync();
                    var mainnetChainIds = allChains
                        .Where(c => !c.IsTestnet)
                        .Select(c => (long)c.ChainId)
                        .ToList();

                    if (mainnetChainIds.Any())
                    {
                        await tokenService.InitializeCacheAsync(mainnetChainIds);
                    }
                }
            }
            catch
            {
                // Silently ignore cache preload failures - tokens will be loaded on demand
            }
        }
    }
}
