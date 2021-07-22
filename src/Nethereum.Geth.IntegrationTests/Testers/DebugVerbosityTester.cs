using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugVerbosityTester : RPCRequestTester<object>, IRPCRequestTester
    {
        public override async Task<object> ExecuteAsync(IClient client)
        {
            var debugVerbosity = new DebugVerbosity(client);
            return await debugVerbosity.SendRequestAsync(5);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugVerbosity);
        }

        [Fact]
        public async void ShouldSetTheVerbosityAndReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.Null(result);
        }
    }
}