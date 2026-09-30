using System;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using AACoreDeployment = Nethereum.AccountAbstraction.Deployment;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Deployment
{
    public class AppChainDeploymentExtensionsTests
    {
        [Fact]
        public void ToAADeploymentAddresses_maps_the_deployment_and_leaves_the_paymaster_empty()
        {
            var deployment = new AppChainDeployment
            {
                EntryPointAddress = "0x1000000000000000000000000000000000000001",
                AccountFactoryAddress = "0x1000000000000000000000000000000000000002",
                AccountRegistryAddress = "0x1000000000000000000000000000000000000003",
                SponsoredPaymasterAddress = "0x1000000000000000000000000000000000000004",
                Modules = new AACoreDeployment.AAModuleAddresses
                {
                    EcdsaValidator = "0x1000000000000000000000000000000000000005",
                    SmartSession = "0x1000000000000000000000000000000000000006"
                }
            };

            var addresses = deployment.ToAADeploymentAddresses();

            Assert.Equal(deployment.EntryPointAddress, addresses.EntryPointAddress);
            Assert.Equal(deployment.AccountFactoryAddress, addresses.NethereumAccountFactoryAddress);
            Assert.Equal(deployment.Modules.EcdsaValidator, addresses.EcdsaValidatorAddress);
            Assert.Equal(string.Empty, addresses.VerifyingPaymasterAddress);
        }

        [Fact]
        public void ToAADeploymentAddresses_throws_when_the_ecdsa_validator_module_is_missing()
        {
            var deployment = new AppChainDeployment
            {
                EntryPointAddress = "0x1000000000000000000000000000000000000001",
                AccountFactoryAddress = "0x1000000000000000000000000000000000000002",
                Modules = new AACoreDeployment.AAModuleAddresses()
            };

            Assert.Throws<InvalidOperationException>(() => deployment.ToAADeploymentAddresses());
        }
    }
}
