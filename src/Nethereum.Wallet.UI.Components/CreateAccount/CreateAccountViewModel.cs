using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using Nethereum.Wallet.Hosting;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.CreateAccount
{
    public partial class CreateAccountViewModel : ObservableObject
    {
        private readonly IWalletVaultService _walletVaultService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IEnumerable<IAccountCreationViewModel> _availableCreationViewModels;

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private IAccountCreationViewModel? _selectedCreationViewModel;

        public ObservableCollection<IAccountCreationViewModel> AvailableCreationViewModels { get; } = new();

        public CreateAccountViewModel(
            IWalletVaultService walletVaultService,
            NethereumWalletHostProvider walletHostProvider,
            IEnumerable<IAccountCreationViewModel> availableCreationViewModels)
        {
            _walletVaultService = walletVaultService;
            _walletHostProvider = walletHostProvider;
            _availableCreationViewModels = availableCreationViewModels;

            foreach (var creationViewModel in _availableCreationViewModels)
            {
                AvailableCreationViewModels.Add(creationViewModel);
            }
            
            SelectedCreationViewModel = AvailableCreationViewModels.FirstOrDefault();
        }

        [RelayCommand]
        public async Task CreateAccountAsync()
        {
            if (SelectedCreationViewModel?.CanCreateAccount != true) return;

            IsBusy = true;
            try
            {
                var vault = _walletVaultService.GetCurrentVault();
                if (vault == null) return;

                var newAccount = SelectedCreationViewModel.CreateAccount(vault);
                
                vault.AddAccount(newAccount);
                await _walletVaultService.SaveAsync();
                
                await _walletHostProvider.RefreshAccountsAsync();
                await _walletHostProvider.SetSelectedAccountAsync(newAccount);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}