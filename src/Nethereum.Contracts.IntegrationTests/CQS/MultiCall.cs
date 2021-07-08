using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.QueryHandlers;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.XUnitEthereumClients;

namespace Nethereum.Contracts.IntegrationTests.CQS
{
    [FunctionOutput]
    public class BalanceOfOutputDTO : IFunctionOutputDTO
    {
        [Parameter("uint256", "balance", 1)]
        public BigInteger Balance { get; set; }
    }

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class MultiCallTest
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public MultiCallTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldCheckBalanceOfMultipleAccounts()
        {
            //Connecting to Ethereum mainnet using Infura
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);

            //Setting the owner https://etherscan.io/tokenholdings?a=0x8ee7d9235e01e6b42345120b5d270bdb763624c7
            var balanceOfMessage1 = new BalanceOfFunction() {Owner = "0x5d3a536e4d6dbd6114cc1ead35777bab948e3643" }; //compound
            var call1 = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(balanceOfMessage1,
                "0x6b175474e89094c44da98b954eedeac495271d0f"); //dai

            var balanceOfMessage2 = new BalanceOfFunction() {Owner = "0x6c6bc977e13df9b0de53b251522280bb72383700" }; //uni
            var call2 = new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(balanceOfMessage1,
                "0x6b175474e89094c44da98b954eedeac495271d0f"); //dai

            await web3.Eth.GetMultiQueryHandler().MultiCallAsync(call1, call2);
            Assert.True(call1.Output.Balance > 0);
            Assert.True(call2.Output.Balance > 0);
           
        }


    }

  
}