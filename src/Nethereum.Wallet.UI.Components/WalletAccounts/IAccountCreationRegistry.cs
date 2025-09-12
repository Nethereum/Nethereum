using System;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public interface IAccountCreationRegistry
    {
        void Register<TViewModel, TComponent>() 
            where TViewModel : class, IAccountCreationViewModel
            where TComponent : class;
        IEnumerable<IAccountCreationViewModel> GetAvailableAccountTypes();
        Type? GetComponentType(IAccountCreationViewModel viewModel);
        Type? GetComponentType<TViewModel>() where TViewModel : class, IAccountCreationViewModel;
    }
}