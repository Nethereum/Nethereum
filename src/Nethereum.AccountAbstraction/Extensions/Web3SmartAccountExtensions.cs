using Nethereum.AccountAbstraction.Builders;
using Nethereum.AccountAbstraction.Paymasters;
using Nethereum.AccountAbstraction.Services;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Extensions
{
    public static class Web3SmartAccountExtensions
    {
        public static SmartAccountBuilder CreateSmartAccount(this IWeb3 web3)
        {
            return new SmartAccountBuilder(web3);
        }

        public static async Task<SmartAccountService> GetSmartAccountAsync(this IWeb3 web3, string address)
        {
            return await SmartAccountService.LoadAsync(web3, address);
        }

        public static async Task<SmartAccountFactoryService> GetSmartAccountFactoryAsync(this IWeb3 web3, string address)
        {
            return await SmartAccountFactoryService.LoadAsync(web3, address);
        }

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
