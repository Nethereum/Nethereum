using edjCase.JsonRpc.Client;
using RPCRequestResponseHandlers;
using System.Globalization;
using System.Threading.Tasks;
using Ethereum.RPC.Util;

namespace Ethereum.RPC
{
    public class GenericRpcRequestResponseHandlerNoParam<TResponse> : RpcRequestResponseHandlerNoParam<TResponse>
    {
        public GenericRpcRequestResponseHandlerNoParam(string methodName) : base(methodName)
        {

        }

        public override Task<TResponse> SendRequestAsync(RpcClient client, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return base.SendRequestAsync(client, id);
        }
    }
}
