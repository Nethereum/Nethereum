using Nethereum.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Sample.Testers
{
    public abstract class RPCRequestTester<T>: IRPCRequestTester
    {
        public async Task<object> ExecuteTestAsync(IClient client)
        {
            return await ExecuteAsync(client);
        }

        public abstract Task<T> ExecuteAsync(IClient client);

        public abstract Type GetRequestType();
    }
}