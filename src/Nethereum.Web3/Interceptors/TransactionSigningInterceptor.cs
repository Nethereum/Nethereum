using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Transactions;
using System;
using System.Threading.Tasks;

namespace Nethereum.Web3.Interceptors
{
    public class TransactionRequestToOfflineSignedTransactionInterceptor : RequestInterceptor
    {
        private readonly string account;
        private readonly SignedTransactionManager signer;

        public TransactionRequestToOfflineSignedTransactionInterceptor(string account, string privateKey, Web3 web3)
        {
            this.account = account;
            signer = new SignedTransactionManager(web3.Client, privateKey, account);
        }

        public override async Task<object> InterceptSendRequestAsync<TResponse>(
            Func<RpcRequest, string, Task<TResponse>> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {
            if (request.Method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput) ((object[])request.RawParameters)[0];
                return await SignAndSendTransaction(transaction);
            }
            return base.InterceptSendRequestAsync(interceptedSendRequestAsync, request, route);
        }

        public override async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList)
        {
            if (method == "eth_sendTransaction")
            {
                var transaction = (TransactionInput) paramList[0];
                return await SignAndSendTransaction(transaction).ConfigureAwait(false);
            }
            return base.InterceptSendRequestAsync(interceptedSendRequestAsync, method, route, paramList);
        }

        private async Task<string> SignAndSendTransaction(TransactionInput transaction)
        {
            if (transaction.From.EnsureHexPrefix().ToLower() != account.EnsureHexPrefix().ToLower())
                throw new Exception("Invalid account used for interceptor");
            return await signer.SendTransactionAsync(transaction).ConfigureAwait(false);
        }

    }
}