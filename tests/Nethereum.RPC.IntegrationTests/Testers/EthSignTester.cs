using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthSignTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var ethSign = new EthSign(client);
            return await ethSign.SendRequestAsync("0x12890D2cce102216644c59daE5baed380d84830c", "Hello world");
        }

        public override Type GetRequestType()
        {
            return typeof (EthSign);
        }
    }
}