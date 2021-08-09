using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.JsonRpc.UnityClient
{
    public class EthTransferUnityRequest : UnityRequest<string>
    {
        private readonly TransactionSignedUnityRequest _transactionSignedUnityRequest;
        public bool UseLegacyAsDefault
        {
            get => _transactionSignedUnityRequest.UseLegacyAsDefault;
            set => _transactionSignedUnityRequest.UseLegacyAsDefault = value;
        }

        public EthTransferUnityRequest(string url, string privateKey, BigInteger? chainId, Dictionary<string, string> requestHeaders = null)
        {
            _transactionSignedUnityRequest = new TransactionSignedUnityRequest(url, privateKey, chainId, requestHeaders);
        }

        public IEnumerator TransferEther(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, BigInteger? nonce = null)
        {
            var transactionInput =
                EtherTransferTransactionInputBuilder.CreateTransactionInput(null, toAddress, etherAmount,
                    gasPriceGwei, gas, nonce);
            yield return _transactionSignedUnityRequest.SignAndSendTransaction(transactionInput);

            if (_transactionSignedUnityRequest.Exception == null)
            {

                this.Result = _transactionSignedUnityRequest.Result;
            }
            else
            {
                this.Exception = _transactionSignedUnityRequest.Exception;
            }
        }


        public IEnumerator TransferEther(string toAddress, decimal etherAmount, BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas, BigInteger? gas = null, BigInteger? nonce = null)
        {
            var transactionInput =
                EtherTransferTransactionInputBuilder.CreateTransactionInput(null, toAddress, etherAmount,
                    maxPriorityFeePerGas, maxFeePerGas, gas, nonce);
            yield return _transactionSignedUnityRequest.SignAndSendTransaction(transactionInput);

            if (_transactionSignedUnityRequest.Exception == null)
            {

                this.Result = _transactionSignedUnityRequest.Result;
            }
            else
            {
                this.Exception = _transactionSignedUnityRequest.Exception;
            }
        }

    }
}