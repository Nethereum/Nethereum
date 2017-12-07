using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Trace;
using Nethereum.Parity.RPC.Trace.TraceDTOs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Tests;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.Tests.Tests.Trace
{
    public class TraceFilterTester : RPCRequestTester<JArray>
    {
        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var receiverAdddress = "0x12890d2cce102216644c59daE5baed380d84830c";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
         
            var web3 = new Web3.Web3(new Account(privateKey), client);

            var receipt = await web3.TransactionManager.TransactionReceiptService.SendRequestAsync(new TransactionInput(){From = senderAddress, To = senderAddress, Value = new HexBigInteger(Web3.Web3.Convert.ToWei(1))});
          
            var traceTransaction = new TraceFilter(client);
            //ToAddress = new []{receiverAdddress}, FromBlock = new BlockParameter(receipt.BlockNumber), Count = 1}
            return await traceTransaction.SendRequestAsync(new TraceFilterDTO(){FromAddresses = new[]{senderAddress}});
        }

        public override Type GetRequestType()
        {
            return typeof(TraceTransaction);
        }
    }
}