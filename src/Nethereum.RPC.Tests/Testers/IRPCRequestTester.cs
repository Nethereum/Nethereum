using Nethereum.JsonRpc.Client;
using System;
using System.Runtime.Remoting.Channels;
using System.Threading.Tasks;

namespace Nethereum.RPC.Sample.Testers
{
    public interface IRPCRequestTester
    {
        Task<object> ExecuteTestAsync(IClient client);
        Type GetRequestType();
    }
}