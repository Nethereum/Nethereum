using Nethereum.JsonRpc.Client.Streaming;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.RPC.Tests.Testers.Streaming
{
    public abstract class StreamingRPCRequestTester
    {
        public TestSettings Settings { get; set; }

        protected StreamingRPCRequestTester()
        {
            Settings = new TestSettings(TestSettingsCategory.hostedTestNet);
        }

        protected virtual async Task<StreamingWebSocketClientTestContext> CreateAndStartStreamingClientAsync()
        {
            var context = new StreamingWebSocketClientTestContext(Settings);
            await context.StreamingClient.StartAsync();
            return context;
        }

        public abstract Task ExecuteAsync();

        protected async Task WaitForFirstMessage<T>(IRpcStreamingSubscriptionHandler subscription, ConcurrentBag<T> receivedMessages, TimeSpan timeout)
        {
            var deadline = DateTime.Now.Add(timeout);

            while (Wait(subscription, receivedMessages, deadline))
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private bool Wait<T>(IRpcStreamingSubscriptionHandler subscription, ConcurrentBag<T> receivedMessages, DateTime deadline)
        {
            if(receivedMessages.Any()) return false;

            if (DateTime.Now > deadline)
                throw new TimeoutException("Timeout exceeded");
            
            var state = subscription.SubscriptionState;
            var subscriptionIsActive = IsActive(state);

            if(subscriptionIsActive) return true;

            throw new Exception($"No messages found and subscription is no longer active. Subscription State: {state}");
        }

        protected virtual bool IsActive(SubscriptionState state)
        {
            return state == SubscriptionState.Subscribed || 
                state == SubscriptionState.Subscribing || 
                state == SubscriptionState.Unsubscribing;
        }

        private readonly object unsubscribeLock = new object();

        protected virtual void TryUnsubscribe<T>(RpcStreamingSubscriptionHandler<T> subscription)
        {
            lock(unsubscribeLock)
            { 
                try
                {
                    if(subscription.SubscriptionState == SubscriptionState.Subscribed)
                    { 
                        subscription.UnsubscribeAsync().Wait();
                    }
                }
                catch
                {
                    //ignore
                }
            }
        }

    }
}