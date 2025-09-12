using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Core.Registry;

namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public class AccountCreationRegistry : IAccountCreationRegistry
    {
        private readonly IComponentRegistry _componentRegistry;
        private readonly IServiceProvider _serviceProvider;

        public AccountCreationRegistry(
            IComponentRegistry componentRegistry, 
            IServiceProvider serviceProvider)
        {
            _componentRegistry = componentRegistry;
            _serviceProvider = serviceProvider;
        }

        public void Register<TViewModel, TComponent>() 
            where TViewModel : class, IAccountCreationViewModel
            where TComponent : class
        {
            _componentRegistry.Register<TViewModel, TComponent>();
        }

        public IEnumerable<IAccountCreationViewModel> GetAvailableAccountTypes()
        {
            // Get all IAccountCreationViewModel instances from DI, filter by visibility, ordered by SortOrder
            return _serviceProvider.GetServices<IAccountCreationViewModel>()
                                  .Where(vm => vm.IsVisible)
                                  .OrderBy(vm => vm.SortOrder);
        }

        public Type? GetComponentType(IAccountCreationViewModel viewModel)
        {
            return _componentRegistry.GetComponentType(viewModel.GetType());
        }

        public Type? GetComponentType<TViewModel>() where TViewModel : class, IAccountCreationViewModel
        {
            return _componentRegistry.GetComponentType<TViewModel>();
        }
    }
}