using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.AccountDetails
{
    public class AccountDetailsRegistry : IAccountDetailsRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, Type> _componentMappings = new();

        public AccountDetailsRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Register<TViewModel, TComponent>() 
            where TViewModel : class, IAccountDetailsViewModel
            where TComponent : class
        {
            _componentMappings[typeof(TViewModel)] = typeof(TComponent);
        }

        public IEnumerable<IAccountDetailsViewModel> GetAvailableAccountDetailTypes()
        {
            return _serviceProvider.GetServices<IAccountDetailsViewModel>()
                .OrderBy(vm => vm.AccountType);
        }

        public Type? GetComponentType(IAccountDetailsViewModel viewModel)
        {
            if (viewModel == null) return null;
            
            return _componentMappings.TryGetValue(viewModel.GetType(), out var componentType) 
                ? componentType 
                : null;
        }

        public Type? GetComponentType<TViewModel>() where TViewModel : class, IAccountDetailsViewModel
        {
            return _componentMappings.TryGetValue(typeof(TViewModel), out var componentType) 
                ? componentType 
                : null;
        }

        public Type? GetViewModelType(IWalletAccount account)
        {
            if (account == null) return null;

            // Get all registered ViewModels and find one that can handle this account
            var availableViewModels = GetAvailableAccountDetailTypes();
            foreach (var viewModel in availableViewModels)
            {
                if (viewModel.CanHandle(account))
                {
                    return viewModel.GetType();
                }
            }

            return null;
        }

        public Type? GetComponentType(Type viewModelType)
        {
            if (viewModelType == null) return null;
            
            return _componentMappings.TryGetValue(viewModelType, out var componentType) 
                ? componentType 
                : null;
        }
    }
}