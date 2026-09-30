using AACoreDeployment = Nethereum.AccountAbstraction.Deployment;

namespace Nethereum.AccountAbstraction.AppChain.Deployment
{
    public class AppChainDeployment
    {
        public string EntryPointAddress { get; set; }
        public string AccountRegistryAddress { get; set; }
        public string SponsoredPaymasterAddress { get; set; }
        public string AccountFactoryAddress { get; set; }
        public AACoreDeployment.AAModuleAddresses Modules { get; set; }
    }
}
