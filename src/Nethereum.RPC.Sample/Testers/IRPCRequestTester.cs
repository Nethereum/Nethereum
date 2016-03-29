using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Sample.Testers
{
    public interface IRPCRequestTester
    {
        Task<dynamic> ExecuteTestAsync(RpcClient client);
        Type GetRequestType();
    }
}