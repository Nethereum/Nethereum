using edjCase.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Ethereum.RPC.Sample
{
    public interface IRPCRequestTester
    {
        Task<dynamic> ExecuteTestAsync(RpcClient client);
        Type GetRequestType();
    }
}
