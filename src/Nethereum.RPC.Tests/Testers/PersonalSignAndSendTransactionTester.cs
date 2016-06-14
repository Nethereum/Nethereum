
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Sample.Testers
{
    public class PersonalSignAndSendTransactionTester : RPCRequestTester<string>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldSignAndSendTransaction()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
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
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";

            return await personalSignAndSendTransaction.SendRequestAsync(transactionInput, "password");
        }

        public override Type GetRequestType()
        {
            return typeof(PersonalSignAndSendTransaction);
        }
    }
}
        