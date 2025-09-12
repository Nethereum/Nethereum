using System;
using System.Collections.Generic;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.AccountDetails
{
    public interface IAccountDetailsRegistry
    {
        void Register<TViewModel, TComponent>() 
            where TViewModel : class, IAccountDetailsViewModel
            where TComponent : class;
        IEnumerable<IAccountDetailsViewModel> GetAvailableAccountDetailTypes();
        Type? GetComponentType(IAccountDetailsViewModel viewModel);
        Type? GetComponentType<TViewModel>() where TViewModel : class, IAccountDetailsViewModel;
        Type? GetViewModelType(IWalletAccount account);
        Type? GetComponentType(Type viewModelType);
    }
}