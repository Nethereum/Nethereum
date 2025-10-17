using System;
using Nethereum.Web3;

namespace Nethereum.Uniswap.Accounts
{
    /// <summary>
    /// Lightweight container exposing account-related helpers.
    /// </summary>
    public class AccountServices
    {
        public AccountServices(IWeb3 web3)
        {
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            Balances = new AccountBalanceValidator(Web3);
            Approvals = new AccountApprovalService(Web3);
        }

        public IWeb3 Web3 { get; }
        public AccountBalanceValidator Balances { get; }
        public AccountApprovalService Approvals { get; }
    }
}
