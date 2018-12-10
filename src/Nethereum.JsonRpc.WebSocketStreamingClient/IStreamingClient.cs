using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
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