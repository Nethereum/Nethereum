using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using System;
using System.Threading.Tasks;

namespace Nethereum.WebSocketsStreamingTest
{
    internal class ExampleReconnectStress
    {
        private readonly string _url;

        public ExampleReconnectStress(string url)
        {
            _url = url;
        }

        public async Task RunAsync(int iterations, int delayBetweenMs)
        {
            for (var i = 1; i <= iterations; i++)
            {
                Console.WriteLine($"Iteration {i}/{iterations}");
                var client = new StreamingWebSocketClient(_url);

                try
                {
                    await client.StartAsync();

                    // Minimal subscription to keep the socket active.
                    var blockHeaderSubscription = new EthNewBlockHeadersObservableSubscription(client);
                    await blockHeaderSubscription.SubscribeAsync();

                    // Call StopAsync concurrently to simulate duplicated disconnect handling.
                    var stop1 = client.StopAsync();
                    var stop2 = client.StopAsync();
                    await Task.WhenAll(stop1, stop2);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Reconnect stress error: " + ex);
                }
                finally
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Dispose error: " + ex.Message);
                    }
                }

                await Task.Delay(delayBetweenMs);
            }
        }
    }
}
