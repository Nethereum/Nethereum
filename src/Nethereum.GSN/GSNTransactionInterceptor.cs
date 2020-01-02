using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public class GSNTransactionInterceptor : RequestInterceptor
    {
        private readonly IGSNTransactionManager _transactionManager;

        public GSNTransactionInterceptor(IGSNTransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public override async Task<object> InterceptSendRequestAsync<TResponse>(
            Func<RpcRequest, string, Task<TResponse>> interceptedSendRequestAsync,
            RpcRequest request,
            string route = null)
        {
            if (request.Method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput)request.RawParameters[0];
                return await _transactionManager.SendTransactionAsync(transaction)
                    .ConfigureAwait(false);
            }

            return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, request, route)
                .ConfigureAwait(false);
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync,
            string method,
            string route = null,
            params object[] paramList)
        {
            if (method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput)paramList[0];
                return await _transactionManager.SendTransactionAsync(transaction)
                    .ConfigureAwait(false);
            }

            return await base.InterceptSendRequestAsync(interceptedSendRequestAsync, method, route, paramList)
                .ConfigureAwait(false);
        }
    }
}
