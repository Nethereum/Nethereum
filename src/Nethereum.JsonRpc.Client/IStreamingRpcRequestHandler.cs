using System;

namespace Nethereum.JsonRpc.Client
{
    public interface IStreamingRpcRequestHandler<TEventArgs> 
        where TEventArgs : EventArgs
    {
        string MethodName { get; }
        IStreamingClient Client { get; }
        event EventHandler<TEventArgs> MessageRecieved;
    }
}