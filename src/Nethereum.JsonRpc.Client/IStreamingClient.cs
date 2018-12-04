using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public interface IStreamingClient : IBaseClient
    {
        event EventHandler<RpcStreamingResponseMessageEventArgs> StreamingMessageReceived;

        event EventHandler<RpcResponseMessageEventArgs> MessageReceived;

        event EventHandler<RpcResponseErrorMessageEventArgs> Error;
    }
}