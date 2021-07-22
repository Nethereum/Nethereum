using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.DTOs;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Quorum.RPC.Interceptors
{
    public class PrivateForInterceptor : RequestInterceptor
    {
        private readonly List<string> _privateFor;
        private readonly string _privateFrom;

        public PrivateForInterceptor(List<string> privateFor, string privateFrom)
        {
            this._privateFor = privateFor;
            this._privateFrom = privateFrom;
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {
            if (_privateFor != null && _privateFor.Count > 0)
            {
                if (request.Method == "eth_sendTransaction")
                {
                    var transaction = (TransactionInput) request.RawParameters[0];
                    var privateTransaction =
                        new PrivateTransactionInput(transaction, _privateFor.ToArray(), _privateFrom);
                    return await interceptedSendRequestAsync(
                        new RpcRequest(request.Id, request.Method, privateTransaction), route).ConfigureAwait(false);
                }

                if (request.Method == "eth_sendRawTransaction")
                {
                    var rawTrasaction = request.RawParameters[0];

                    return await interceptedSendRequestAsync(
                        new RpcRequest(request.Id, "eth_sendRawPrivateTransaction", rawTrasaction,
                            new PrivateRawTransaction(_privateFor.ToArray())), route).ConfigureAwait(false);
                }
            }

            return await interceptedSendRequestAsync(request, route).ConfigureAwait(false);
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList)
        {
            if (_privateFor != null && _privateFor.Count > 0)
            {
                if (method == "eth_sendTransaction")
                {
                    var transaction = (TransactionInput) paramList[0];
                    var privateTransaction =
                        new PrivateTransactionInput(transaction, _privateFor.ToArray(), _privateFrom);
                    paramList[0] = privateTransaction;
                    return await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
                }

                if (method == "eth_sendRawTransaction")
                {
                    var rawTrasaction = paramList[0];
                    return await interceptedSendRequestAsync("eth_sendRawPrivateTransaction", route,
                            new object[] {rawTrasaction, new PrivateRawTransaction(_privateFor.ToArray())})
                        .ConfigureAwait(false);
                }
            }

            return await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
        }

    }
}