using System.Numerics;
using Nethereum.AccountAbstraction.Deployment;

namespace Nethereum.AccountAbstraction.AppChain.Configuration
{
    public class AppChainConfig
    {
        public string EntryPointAddress { get; set; } = EntryPointAddresses.V09;
        public string Owner { get; set; }
        public decimal InitialPaymasterDeposit { get; set; } = 10m;
        public string[] Admins { get; set; } = Array.Empty<string>();
        public string? AccountFactoryAddress { get; set; }
        public DefaultModulesConfig DefaultModules { get; set; } = new();
        public BigInteger ChainId { get; set; }
    }

    public class DefaultModulesConfig
    {
        public bool InstallOwnerValidator { get; set; } = true;
        public bool InstallSessionKeys { get; set; } = true;
        public bool InstallSocialRecovery { get; set; } = true;

        public AAModuleAddresses? ModuleAddresses { get; set; }
    }
}
