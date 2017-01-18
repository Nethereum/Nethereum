using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Quorum.RPC.Interceptors
{
    public class PrivateForInterceptor : RequestInterceptor
    {
        private readonly List<string> privateFor;
        private readonly string privateFrom;

        public PrivateForInterceptor(List<string> privateFor, string privateFrom)
        {
            this.privateFor = privateFor;
            this.privateFrom = privateFrom;
        }

        public RpcResponse BuildResponse(object results, string route = null)
        {
            var token = JToken.FromObject(results);
            return new RpcResponse(route, token);
        }

        public override async Task<RpcResponse> InterceptSendRequestAsync(
            Func<RpcRequest, string, Task<RpcResponse>> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {
            if (request.Method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput) request.ParameterList[0];
                var privateTransaction = new PrivateTransactionInput(transaction, privateFor.ToArray(), privateFrom);
                request.ParameterList[0] = privateTransaction;
                return await interceptedSendRequestAsync(request, route).ConfigureAwait(false);
            }
            return await interceptedSendRequestAsync(request, route).ConfigureAwait(false);
        }

        public override async Task<RpcResponse> InterceptSendRequestAsync(
            Func<string, string, object[], Task<RpcResponse>> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList)
        {
            if (method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput) paramList[0];
                var privateTransaction = new PrivateTransactionInput(transaction, privateFor.ToArray(), privateFrom);
                paramList[0] = privateTransaction;
                return await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
            }

            return await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
        }

    }
}