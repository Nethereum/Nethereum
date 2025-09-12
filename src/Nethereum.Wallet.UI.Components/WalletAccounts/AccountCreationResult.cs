using System;
using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public class AccountCreationResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }
        public Exception? Exception { get; private set; }
        public IWalletAccount? Account { get; private set; }
        
        private AccountCreationResult() { }
        
        public static AccountCreationResult Success(IWalletAccount account) => new() 
        { 
            IsSuccess = true, 
            Account = account 
        };
        
        public static AccountCreationResult Failure(string error, Exception? ex = null) => new() 
        { 
            IsSuccess = false, 
            ErrorMessage = error, 
            Exception = ex 
        };
    }
}