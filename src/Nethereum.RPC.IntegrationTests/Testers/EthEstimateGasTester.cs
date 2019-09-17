using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class EthEstimateGasTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {

        public EthEstimateGasTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) :
            base(ethereumClientIntegrationFixture, TestSettings.GethLocalSettings)
        {
        }

        [Fact]
        public async void ShouldEstimateGas()
        {
            var result = await ExecuteAsync();
            Assert.True(result.Value > 0);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var ethEstimateGas = new EthEstimateGas(client);
            var contractByteCode = "0xc6888fa10000000000000000000000000000000000000000000000000000000000000045";
            var to = "0x32eb97b8ad202b072fd9066c03878892426320ed";

            var transactionInput = new CallInput();
            transactionInput.Data = contractByteCode;
            transactionInput.To = to;
            transactionInput.From = Settings.GetDefaultAccount();

            return await ethEstimateGas.SendRequestAsync(transactionInput);
        }

        public override Type GetRequestType()
        {
            return typeof (EthEstimateGas);
        }
    }
}