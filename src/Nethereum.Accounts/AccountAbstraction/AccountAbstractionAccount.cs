using System;
using Nethereum.Accounts.ViewOnly;
using Nethereum.RPC.AccountSigning;

namespace Nethereum.Accounts.AccountAbstraction
{
    public class AccountAbstractionAccount : ViewOnlyAccount
    {
        public AccountAbstractionAccount(string smartAccountAddress, IAccountSigningService accountSigningService)
            : base(smartAccountAddress)
        {
            AccountSigningService = accountSigningService ?? throw new ArgumentNullException(nameof(accountSigningService));
        }
    }
}
