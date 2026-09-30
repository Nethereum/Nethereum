using Nethereum.AccountAbstraction.Paymasters;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Extensions
{
    public static class Web3PaymasterExtensions
    {
        public static async Task<VerifyingPaymasterManager> GetVerifyingPaymasterAsync(this IWeb3 web3, string address, EthECKey? signerKey = null)
        {
            return await VerifyingPaymasterManager.LoadAsync(web3, address, signerKey);
        }

        public static async Task<DepositPaymasterManager> GetDepositPaymasterAsync(this IWeb3 web3, string address)
        {
            return await DepositPaymasterManager.LoadAsync(web3, address);
        }
    }
}
