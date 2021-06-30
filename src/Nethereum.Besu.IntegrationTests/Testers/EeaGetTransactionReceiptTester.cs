

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.EEA;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.Besu.RPC.EEA.DTOs;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class EeaGetTransactionReceiptTester : RPCRequestTester<EeaTransactionReceipt>, IRPCRequestTester
    {
        public override async Task<EeaTransactionReceipt> ExecuteAsync(IClient client)
        {
            var eeaGetTransactionReceipt = new EeaGetTransactionReceipt(client);
            return await eeaGetTransactionReceipt.SendRequestAsync(Settings.GetTransactionHash());
        }

        public override Type GetRequestType()
        {
            return typeof(EeaGetTransactionReceipt);
        }

        //[Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        