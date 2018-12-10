namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public interface IRpcStreamingSubscriptionHandler : IRpcStreamingResponseHandler
    {
        string SubscriptionId { get; }
        SubscriptionState SubscriptionState { get; }
    }
}