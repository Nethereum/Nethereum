using Nethereum.JsonRpc.Client.RpcMessages;
using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseHandlerNoParam<TResponse> : IRpcRequestHandler<TResponse>
    {
        protected RpcRequestBuilder RpcRequestBuilder { get; }

        public RpcRequestResponseHandlerNoParam(IClient client, string methodName)
        {
            RpcRequestBuilder = new RpcRequestBuilder(methodName);
            Client = client;
        }

        public string MethodName  => RpcRequestBuilder.MethodName;
        public IClient Client { get; }

        public virtual Task<TResponse> SendRequestAsync(object id)
        {
            if (Client == null) throw new NullReferenceException("RpcRequestHandler Client is null");
            return Client.SendRequestAsync<TResponse>(BuildRequest(id));
        }

        public RpcRequest BuildRequest(object id = null)
        {
            return RpcRequestBuilder.BuildRequest(id);
        }

        public virtual TResponse DecodeResponse(RpcResponseMessage rpcResponseMessage)
        {
            try
            {
                return rpcResponseMessage.GetResult<TResponse>();
            }
            catch (FormatException formatException)
            {
                throw new RpcResponseFormatException("Invalid format found in RPC response", formatException);
            }
        }
    }

}