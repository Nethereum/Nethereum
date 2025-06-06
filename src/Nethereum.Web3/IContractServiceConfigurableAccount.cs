using Nethereum.RPC.Accounts;


namespace Nethereum.Web3
{
    public interface IContractServiceConfigurableAccount : IAccount
    {
        void ConfigureContractHandler<T>(T contractService) where T : ContractWeb3ServiceBase;
    }
}
