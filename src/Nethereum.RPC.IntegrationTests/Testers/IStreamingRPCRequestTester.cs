using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Tests.Testers
{
    //TODO:Subscriptions
    public interface IStreamingRPCRequestTester
    {
        Task ExecuteTestAsync(IStreamingClient client);
        Type GetRequestType();
    }
}