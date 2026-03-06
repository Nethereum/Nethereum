using Nethereum.AccountAbstraction.AppChain.Configuration;

namespace Nethereum.AccountAbstraction.AppChain.Deployment
{
    public class AppChainDeployment
    {
        public string EntryPointAddress { get; set; }
        public string AccountRegistryAddress { get; set; }
        public string SponsoredPaymasterAddress { get; set; }
        public string AccountFactoryAddress { get; set; }
        public ModuleAddresses Modules { get; set; }
    }
}
