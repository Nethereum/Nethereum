using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public sealed record DeployedStack(
        AADeploymentAddresses Addresses,
        IAAClient Client,
        TestCounterService TestCounter,
        BookingRegistryService BookingRegistry,
        PaymasterConfig? Paymaster,
        WebAuthnAccountConfig WebAuthnConfig,
        Eip7702AccountConfig Eip7702Config,
        SocialRecoveryAccountConfig SocialRecoveryConfig,
        PoliciesAccountConfig PoliciesConfig,
        OwnableExecutorService OwnableExecutor,
        SmartSessionService SmartSession,
        string NethereumAccountImplementationAddress,
        bool EntryPointWasFreshlyDeployed);
}
