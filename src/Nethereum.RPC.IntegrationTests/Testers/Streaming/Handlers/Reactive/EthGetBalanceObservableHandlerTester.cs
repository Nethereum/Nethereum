using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.RPC.Tests.Testers.Streaming.Handlers.Reactive
{
    public class EthGetBalanceObservableHandlerTester : StreamingRPCRequestTester
    {
        [Fact(DisplayName = "Should get balance")]
        public override async Task ExecuteAsync()
        {
            using(var context = await CreateAndStartStreamingClientAsync())
            { 
                HexBigInteger actualBalance = null;

                var ethBlockNumber = new EthGetBalanceObservableHandler(context.StreamingClient);

                ethBlockNumber
                    .GetResponseAsObservable()
                    .Subscribe(balance => actualBalance = balance);

                await ethBlockNumber.SendRequestAsync(
                    Settings.GetContractAddress(),
                    BlockParameter.CreateLatest());

                var deadline = DateTime.Now.AddSeconds(30);

                while (true)
                {
                    if(actualBalance != null) break;
                    if(DateTime.Now > deadline) throw new TimeoutException();
                }

                Assert.NotNull(actualBalance);

            }
        }
    }
}
