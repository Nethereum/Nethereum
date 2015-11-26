using edjCase.JsonRpc.Client;
using System;

namespace Ethereum.RPC.Sample
{
    public interface IRPCRequestTester
    {
        dynamic ExecuteTest(RpcClient client);
        Type GetRequestType();
    }
}
