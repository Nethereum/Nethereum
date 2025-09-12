using System;
using System.Collections.Generic;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.AccountDetails
{
    public interface IGroupDetailsRegistry
    {
        void Register<TViewModel, TComponent>() 
            where TViewModel : class, IGroupDetailsViewModel
            where TComponent : class;
        IEnumerable<IGroupDetailsViewModel> GetAvailableGroupDetailTypes();
        Type? GetComponentType(IGroupDetailsViewModel viewModel);
        Type? GetComponentType<TViewModel>() where TViewModel : class, IGroupDetailsViewModel;
        Type? GetViewModelType(string groupId, IReadOnlyList<IWalletAccount> groupAccounts);
        Type? GetComponentType(Type viewModelType);
    }
    public interface IGroupDetailsViewModel
    {
        string GroupType { get; }
        string DisplayName { get; }
        bool CanHandle(string groupId, IReadOnlyList<IWalletAccount> groupAccounts);
    }
}