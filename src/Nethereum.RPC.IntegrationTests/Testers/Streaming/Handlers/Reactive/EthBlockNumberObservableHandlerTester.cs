using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Reactive.Eth;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Handlers.Reactive
{
    public class EthBlockNumberObservableHandlerTester : StreamingRPCRequestTester
    {
        [Fact(DisplayName = "Should get block number")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 
                HexBigInteger actualBlockNumber = null;

                var ethBlockNumber = new EthBlockNumberObservableHandler(
                    context.StreamingClient);

                ethBlockNumber
                    .GetResponseAsObservable()
                    .Subscribe(blockNumber => actualBlockNumber = blockNumber);

                await ethBlockNumber.SendRequestAsync();

                var deadline = DateTime.Now.AddSeconds(30);

                while (true)
                {
                    if(actualBlockNumber != null) break;
                    if(DateTime.Now > deadline) throw new TimeoutException();
                }

                Assert.NotNull(actualBlockNumber);
            }
        }
    }
}
