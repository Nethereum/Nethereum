using System;
using Nethereum.AccountAbstraction.Configuration;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests
{
    public class AADeploymentAddressProviderTests
    {
        private static AADeploymentAddresses CreateAddresses() => new AADeploymentAddresses(
            EntryPointAddress: "0x0000000000000000000000000000000000000001",
            NethereumAccountFactoryAddress: "0x0000000000000000000000000000000000000002",
            EcdsaValidatorAddress: "0x0000000000000000000000000000000000000003",
            VerifyingPaymasterAddress: "0x0000000000000000000000000000000000000004");

        [Fact]
        public void Set_ThenGet_ReturnsTheSameAddresses()
        {
            var provider = new AADeploymentAddressProvider();
            var addresses = CreateAddresses();

            provider.Set(31337, addresses);

            Assert.Equal(addresses, provider.Get(31337));
        }

        [Fact]
        public void Set_ThenTryGet_ReturnsTrueAndTheSameAddresses()
        {
            var provider = new AADeploymentAddressProvider();
            var addresses = CreateAddresses();

            provider.Set(31337, addresses);

            var found = provider.TryGet(31337, out var resolved);

            Assert.True(found);
            Assert.Equal(addresses, resolved);
        }

        [Fact]
        public void TryGet_UnknownChainId_ReturnsFalse()
        {
            var provider = new AADeploymentAddressProvider();

            var found = provider.TryGet(1, out var resolved);

            Assert.False(found);
            Assert.Null(resolved);
        }

        [Fact]
        public void Get_UnknownChainId_Throws()
        {
            var provider = new AADeploymentAddressProvider();

            var ex = Assert.Throws<InvalidOperationException>(() => provider.Get(1));

            Assert.Contains("1", ex.Message);
        }
    }
}
