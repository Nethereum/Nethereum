using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using System;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{

    internal class ExampleLogsUniswapSwapsSubscription
    {
        private readonly string url;
        StreamingWebSocketClient client;

        public ExampleLogsUniswapSwapsSubscription(string url)
        {
            this.url = url;
        }
        public async Task SubscribeAndRunAsync()
        {
            if (client == null)
            {
                client = new StreamingWebSocketClient(url);
                client.Error += Client_Error;
            }

            var token0 = "DAI";
            var token1 = "ETH";
            var pairContractAddress = "0xa478c2975ab1ea89e8196811f51a7b7ade33eb11";

       
                Console.WriteLine($"Uniswap trades for {token0} and {token1}");
                try
                {
                    var eventSubscription = new EthLogsObservableSubscription(client);
                    eventSubscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
                    {
                        var swap = log.DecodeEvent<SwapEventDTO>();

                        var amount0Out = Util.UnitConversion.Convert.FromWei(swap.Event.Amount0Out);
                        var amount1In = Util.UnitConversion.Convert.FromWei(swap.Event.Amount1In);


                        var amount0In = Util.UnitConversion.Convert.FromWei(swap.Event.Amount0In);
                        var amount1Out = Util.UnitConversion.Convert.FromWei(swap.Event.Amount1Out);


                        if (swap.Event.Amount0In == 0 && swap.Event.Amount1Out == 0)
                        {

                            var price = amount0Out / amount1In;
                            var quantity = amount1In;

                            Console.WriteLine($"Sell {token1} Price: {price.ToString("F4")} Quantity: {quantity.ToString("F4")}, From: {swap.Event.To}  Block: {swap.Log.BlockNumber}");
                        }
                        else
                        {

                            var price = amount0In / amount1Out;
                            var quantity = amount1Out;
                            Console.WriteLine($"Buy {token1} Price: {price.ToString("F4")} Quantity: {quantity.ToString("F4")}, From: {swap.Event.To}  Block: {swap.Log.BlockNumber}");

                        }
                    }

                    );

                    eventSubscription.GetSubscribeResponseAsObservable().Subscribe(id => Console.WriteLine($"Subscribed with id: {id}"));

                    var filterAuction = Event<SwapEventDTO>.GetEventABI().CreateFilterInput(pairContractAddress);

                    await client.StartAsync();

                    await eventSubscription.SubscribeAsync(filterAuction);

                    Console.ReadLine();

                    await eventSubscription.UnsubscribeAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            
        

        private async void Client_Error(object sender, Exception ex)
        {
            Console.WriteLine("Client Error restarting...");
            // ((StreamingWebSocketClient)sender).Error -= Client_Error;
            ((StreamingWebSocketClient)sender).StopAsync().Wait();
            //Restart everything
            await SubscribeAndRunAsync();
        }
    }


    [Event("Sync")]
    class PairSyncEventDTO : IEventDTO
    {
        [Parameter("uint112", "reserve0")]
        public virtual BigInteger Reserve0 { get; set; }

        [Parameter("uint112", "reserve1", 2)]
        public virtual BigInteger Reserve1 { get; set; }
    }




    public partial class GetPairFunction : GetPairFunctionBase { }

    [Function("getPair", "address")]
    public class GetPairFunctionBase : FunctionMessage
    {
        [Parameter("address", "tokenA", 1)]
        public virtual string TokenA { get; set; }
        [Parameter("address", "tokenB", 2)]
        public virtual string TokenB { get; set; }
    }


    public partial class SwapEventDTO : SwapEventDTOBase { }

    [Event("Swap")]
    public class SwapEventDTOBase : IEventDTO
    {
        [Parameter("address", "sender", 1, true)]
        public virtual string Sender { get; set; }
        [Parameter("uint256", "amount0In", 2, false)]
        public virtual BigInteger Amount0In { get; set; }
        [Parameter("uint256", "amount1In", 3, false)]
        public virtual BigInteger Amount1In { get; set; }
        [Parameter("uint256", "amount0Out", 4, false)]
        public virtual BigInteger Amount0Out { get; set; }
        [Parameter("uint256", "amount1Out", 5, false)]
        public virtual BigInteger Amount1Out { get; set; }
        [Parameter("address", "to", 6, true)]
        public virtual string To { get; set; }
    }
}
