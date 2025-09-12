using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services;

namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public abstract class AccountCreationViewModelBase : ObservableObject, IAccountCreationViewModel
    {
        protected readonly IWalletVaultService _vaultService;
        protected readonly NethereumWalletHostProvider _walletHostProvider;
        
        protected AccountCreationViewModelBase(
            IWalletVaultService vaultService,
            NethereumWalletHostProvider walletHostProvider)
        {
            _vaultService = vaultService;
            _walletHostProvider = walletHostProvider;
        }
        
        public string? LastError { get; protected set; }
        public Exception? LastException { get; protected set; }
        public bool HasError => !string.IsNullOrEmpty(LastError);
        public async Task<AccountCreationResult> CreateAndAddAccountAsync()
        {
            try
            {
                ClearErrors();
                
                var vault = _vaultService.GetCurrentVault();
                if (vault == null) 
                    return HandleError("Vault is not available", null);
                
                if (!CanCreateAccount)
                    return HandleError("Account data is not valid", null);
                    
                var account = CreateAccount(vault);
                vault.AddAccount(account, setAsSelected: true);
                await _vaultService.SaveAsync();
                await _walletHostProvider.RefreshAccountsAsync();
                await _walletHostProvider.SetSelectedAccountAsync(account);
                
                return AccountCreationResult.Success(account);
            }
            catch (InvalidOperationException ex)
            {
                return HandleError("Invalid operation during account creation", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return HandleError("Access denied while creating account", ex);
            }
            catch (ArgumentException ex)
            {
                return HandleError("Invalid account parameters", ex);
            }
            catch (Exception ex)
            {
                return HandleError($"Failed to create account: {ex.Message}", ex);
            }
        }
        
        private AccountCreationResult HandleError(string message, Exception? ex)
        {
            LastError = message;
            LastException = ex;
            return AccountCreationResult.Failure(message, ex);
        }
        
        private void ClearErrors()
        {
            LastError = null;
            LastException = null;
        }
        
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract string Icon { get; }
        public abstract int SortOrder { get; }
        public abstract bool IsVisible { get; }
        public abstract bool CanCreateAccount { get; }
        public abstract IWalletAccount CreateAccount(WalletVault vault);
        public abstract void Reset();
    }
}