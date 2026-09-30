using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Client
{
    public static class AAClientExtensions
    {
        public static TService UseAccountAbstraction<TService>(
            this TService service, NethereumSmartAccount account, IAAClient client)
            where TService : ContractWeb3ServiceBase
        {
            client.Configure(service, account);
            return service;
        }

        public static TService UseAccountAbstraction<TService>(
            this TService service, SmartAccount account, IAAClient client)
            where TService : ContractWeb3ServiceBase
        {
            client.Configure(service, account);
            return service;
        }
    }
}
