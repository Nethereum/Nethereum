using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;
using Nethereum.RPC.Tests.Testers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.IntegrationTests.Testers
{
    public class ShhAddPrivateKeyTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override Task<string> ExecuteAsync(IClient client)
        {
            var shhAddPrivateKey = new ShhAddPrivateKey(client);
            return shhAddPrivateKey.SendRequestAsync(Settings.GetDefaultPrivateKey());
        }

        public override Type GetRequestType()
        {
            return typeof(ShhAddPrivateKey);
        }
    }
}
