using Nethereum.JsonRpc.Client.RpcMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{

    public class RpcRequestResponseHandler<TResponse> : IRpcRequestHandler<TResponse>
    {
        protected RpcRequestBuilder RpcRequestBuilder { get; }

        public RpcRequestResponseHandler(IClient client, string methodName)
        {
            RpcRequestBuilder = new RpcRequestBuilder(methodName);
            Client = client;
        }

        public string MethodName => RpcRequestBuilder.MethodName;

        public IClient Client { get; }

        protected Task<TResponse> SendRequestAsync(object id, params object[] paramList)
        {
            var request = BuildRequest(id, paramList);
            if(Client == null) throw new NullReferenceException("RpcRequestHandler Client is null");
            return Client.SendRequestAsync<TResponse>(request);
        }

        public RpcRequest BuildRequest(object id, params object[] paramList)
        {
            return RpcRequestBuilder.BuildRequest(id, paramList);
        }

        public virtual TResponse DecodeResponse(RpcResponseMessage rpcResponseMessage)
        {
            try
            {
                return Client.DecodeResult<TResponse>(rpcResponseMessage);
            }
            catch (FormatException formatException)
            {
                throw new RpcResponseFormatException("Invalid format found in RPC response", formatException);
            }
        }
    }
}