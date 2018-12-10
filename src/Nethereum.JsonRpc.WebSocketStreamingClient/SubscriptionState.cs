namespace Nethereum.JsonRpc.WebSocketStreamingClient
{

    public enum SubscriptionState
    {
        Idle,
        Subscribing,
        Subscribed,
        Unsubscribing,
        Unsubscribed
    }
}