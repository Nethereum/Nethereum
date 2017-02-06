using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using Transaction = Nethereum.Core.Transaction;

namespace Nethereum.Web3.Interceptors
{
    public class TransactionRequestToOfflineSignedTransactionInterceptor : RequestInterceptor
    {
        private readonly string account;
        private readonly string privateKey;
        private readonly TransactionSigner signer;
        private readonly Web3 web3;


        public TransactionRequestToOfflineSignedTransactionInterceptor(string account, string privateKey, Web3 web3)
        {
            this.account = account;
            this.privateKey = privateKey;
            this.web3 = web3;
            signer = new TransactionSigner();
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
            if (transaction.From != account) throw new Exception("Invalid account used for interceptor");

            var nonce = transaction.Nonce;
            if (nonce == null)
                nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(account).ConfigureAwait(false);

            var gasPrice = transaction.GasPrice;
            if (gasPrice == null)
                gasPrice = new HexBigInteger(Transaction.DEFAULT_GAS_PRICE);

            var gasLimit = transaction.Gas;
            if (gasLimit == null)
                gasLimit = new HexBigInteger(Transaction.DEFFAULT_GAS_LIMIT);

            var value = transaction.Value;
            if (value == null)
                value = new HexBigInteger(0);

            var signedTransaction = signer.SignTransaction(privateKey, transaction.To, value.Value, nonce,
                gasPrice.Value, gasLimit.Value, transaction.Data);

            var txnHash =
                await
                    web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix())
                        .ConfigureAwait(false);
            return BuildResponse(txnHash, route);
        }
    }
}