using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Personal;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class PersonalSignAndSendTransactionTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public PersonalSignAndSendTransactionTester(
            EthereumClientIntegrationFixture ethereumClientIntegrationFixture) :
            base(ethereumClientIntegrationFixture, TestSettings.GethLocalSettings)
        {
        }

        [Fact]
        public async void ShouldSignAndSendTransaction()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var personalSignAndSendTransaction = new PersonalSignAndSendTransaction(client);
            //contract test { function multiply(uint a) returns(uint d) { return a * 7; } }
            var contractByteCode =
                "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";

            //As the input the compiled contract is the Data, together with our address
            var transactionInput = new TransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.From = Settings.GetDefaultAccount();

            return await personalSignAndSendTransaction.SendRequestAsync(transactionInput, Settings.GetDefaultAccountPassword());
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalSignAndSendTransaction);
        }
    }
}
        