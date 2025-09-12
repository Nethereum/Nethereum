using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public class AccountTypeMetadataRegistry : IAccountTypeMetadataRegistry
    {
        private readonly IServiceProvider _serviceProvider;

        public AccountTypeMetadataRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IAccountTypeMetadata? GetMetadata(string accountType)
        {
            if (string.IsNullOrEmpty(accountType))
                return null;

            var metadataViewModels = _serviceProvider.GetServices<IAccountMetadataViewModel>();
            
            return metadataViewModels.FirstOrDefault(vm => 
                string.Equals(vm.TypeName, accountType, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<IAccountTypeMetadata> GetAllMetadata()
        {
            var metadataViewModels = _serviceProvider.GetServices<IAccountMetadataViewModel>();
            return metadataViewModels
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.DisplayName);
        }

        public IEnumerable<IAccountTypeMetadata> GetVisibleMetadata()
        {
            var metadataViewModels = _serviceProvider.GetServices<IAccountMetadataViewModel>();
            return metadataViewModels
                .Where(m => m.IsVisible)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.DisplayName);
        }

        public bool HasMetadata(string accountType)
        {
            if (string.IsNullOrEmpty(accountType))
                return false;

            var metadataViewModels = _serviceProvider.GetServices<IAccountMetadataViewModel>();
            return metadataViewModels.Any(vm => 
                string.Equals(vm.TypeName, accountType, StringComparison.OrdinalIgnoreCase));
        }
    }
}