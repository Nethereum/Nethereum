using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.RPC.Tests.Testers
{
    //TODO:Subscriptions
    public interface IStreamingRPCRequestTester
    {
        Task ExecuteTestAsync(IStreamingClient client);
        Type GetRequestType();
    }
}