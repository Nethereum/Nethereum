using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client.Streaming
{
    public interface IStreamingClient
    {
        bool IsRunning { get; }
        bool IsStarted { get; }
        bool AddSubscription(string subscriptionId, IRpcStreamingResponseHandler handler);
        bool RemoveSubscription(string subscriptionId);
        Task SendRequestAsync(RpcRequest request, IRpcStreamingResponseHandler requestResponseHandler, string route = null);
        Task Start();
    }
}