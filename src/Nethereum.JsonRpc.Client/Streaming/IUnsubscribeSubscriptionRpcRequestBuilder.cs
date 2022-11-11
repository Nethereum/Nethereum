namespace Nethereum.JsonRpc.Client.Streaming
{
    public interface IUnsubscribeSubscriptionRpcRequestBuilder
    {
        RpcRequest BuildRequest(string subscriptionId, object requestId = null);
    }
}