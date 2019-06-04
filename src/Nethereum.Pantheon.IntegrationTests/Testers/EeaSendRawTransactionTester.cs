

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.EEA;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Nethereum.Signer;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class EeaSendRawTransactionTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var eeaSendRawTransaction = new EeaSendRawTransaction(client);
            var signedTransaction = "0x";  //TODO
            return await eeaSendRawTransaction.SendRequestAsync(signedTransaction);
        }

        public override Type GetRequestType()
        {
            return typeof(EeaSendRawTransaction);
        }

        //[Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        