using edjCase.JsonRpc.Client;
using RPCRequestResponseHandlers;
using System;
using System.Globalization;
using System.Threading.Tasks;

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

    public class GenericRpcRequestResponseHandlerNoParamInt
    {
        private RpcRequestResponseHandlerNoParam<String> requestResponseHandler;


        public GenericRpcRequestResponseHandlerNoParamInt(string methodName)
        {
            this.requestResponseHandler = new RpcRequestResponseHandlerNoParam<string>(methodName);
        }

        public async  Task<Int64> SendRequestAsync(RpcClient client, string id = Constants.DEFAULT_REQUEST_ID)
        {
            return (await requestResponseHandler.SendRequestAsync(client, id)).ConvertHexToInt64();
        }

    }
}
