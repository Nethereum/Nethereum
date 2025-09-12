using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.AccountDetails
{
    public class GroupDetailsRegistry : IGroupDetailsRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, Type> _componentMappings = new();

        public GroupDetailsRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Register<TViewModel, TComponent>() 
            where TViewModel : class, IGroupDetailsViewModel
            where TComponent : class
        {
            _componentMappings[typeof(TViewModel)] = typeof(TComponent);
        }

        public IEnumerable<IGroupDetailsViewModel> GetAvailableGroupDetailTypes()
        {
            return _serviceProvider.GetServices<IGroupDetailsViewModel>()
                .OrderBy(vm => vm.GroupType);
        }

        public Type? GetComponentType(IGroupDetailsViewModel viewModel)
        {
            if (viewModel == null) return null;
            
            return _componentMappings.TryGetValue(viewModel.GetType(), out var componentType) 
                ? componentType 
                : null;
        }

        public Type? GetComponentType<TViewModel>() where TViewModel : class, IGroupDetailsViewModel
        {
            return _componentMappings.TryGetValue(typeof(TViewModel), out var componentType) 
                ? componentType 
                : null;
        }

        public Type? GetViewModelType(string groupId, IReadOnlyList<IWalletAccount> groupAccounts)
        {
            if (string.IsNullOrEmpty(groupId) || groupAccounts == null || !groupAccounts.Any()) 
                return null;

            // Get all registered ViewModels and find one that can handle this group
            var availableViewModels = GetAvailableGroupDetailTypes();
            foreach (var viewModel in availableViewModels)
            {
                if (viewModel.CanHandle(groupId, groupAccounts))
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