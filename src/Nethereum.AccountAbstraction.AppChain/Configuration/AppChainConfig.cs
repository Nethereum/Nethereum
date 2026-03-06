using System.Numerics;

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
        public ModuleAddresses? ModuleAddresses { get; set; }
    }

    public class ModuleAddresses
    {
        public string? ECDSAValidator { get; set; }
        public string? SmartSession { get; set; }
        public string? SocialRecovery { get; set; }
    }
}
