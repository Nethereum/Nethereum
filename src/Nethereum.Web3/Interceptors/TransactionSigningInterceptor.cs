using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3.Transactions;
using Newtonsoft.Json.Linq;
using Transaction = Nethereum.Signer.Transaction;

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
                return await SignAndSendTransaction(transaction, route);
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
                return await SignAndSendTransaction(transaction, route);
            }

            return await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
        }

        private async Task<RpcResponse> SignAndSendTransaction(TransactionInput transaction, string route)
        {
            if (transaction.From.EnsureHexPrefix().ToLower() != account.EnsureHexPrefix().ToLower()) throw new Exception("Invalid account used for interceptor");
            var txnHash = await signer.SendTransactionAsync(transaction).ConfigureAwait(false);
            return BuildResponse(txnHash, route);
        }
    }
}