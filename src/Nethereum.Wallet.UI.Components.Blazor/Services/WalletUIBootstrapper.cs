using System;
using Nethereum.Wallet.UI.Components.Blazor.Extensions;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public sealed class WalletUIBootstrapper
    {
        private readonly IServiceProvider _serviceProvider;
        private bool _initialized;

        public WalletUIBootstrapper(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            // Populate account creation/details registries within the current scope.
            _serviceProvider.InitializeAccountTypes();
            _initialized = true;
        }
    }
}
