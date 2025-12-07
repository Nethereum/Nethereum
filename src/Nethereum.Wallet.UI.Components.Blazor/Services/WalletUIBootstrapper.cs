using System;
using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Blazor.Extensions;
using Nethereum.Wallet.UI.Components.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public sealed class WalletUIBootstrapper
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IWalletUIRegistryContributor> _registryContributors;
        private bool _initialized;

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
            _initialized = true;
        }
    }
}
