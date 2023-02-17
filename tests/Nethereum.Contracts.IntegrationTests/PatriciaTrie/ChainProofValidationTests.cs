using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Patricia
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ChainProofValidationTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ChainProofValidationTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldValidateBalanceOfEOA()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
           
            var accountProof = await web3.Eth.ChainProofValidation.GetAndValidateAccountProof("0xde0b295669a9fd93d5f28d9ec85e40f4cb697bae", null, null, new BlockParameter(16483161));
            Assert.NotNull(accountProof);
        }



        [Fact]
        public async void ShouldValidateStorage()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
   
            var value = await web3.Eth.ChainProofValidation.GetAndValidateValueFromStorage("0x5E4e65926BA27467555EB562121fac00D24E9dD2", "0x0", null, new BlockParameter(16648900));
            var libAddressManager = "0xdE1FCfB0851916CA5101820A69b13a4E276bd81F";
            //de1fcfb0851916ca5101820a69b13a4e276bd81f
            //libAddressManager is at slot 0 as the contract inherits from Lib_AddressResolver
            Assert.True(value.ToHex().IsTheSameHex(libAddressManager));
        }

        [Fact]
        public async void ShouldValidateTransactions()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var transactions = await web3.Eth.ChainProofValidation.GetAndValidateTransactions(new BlockParameter(16503723));
            Assert.Equal(103, transactions.Length);

        }

       
    }
}
